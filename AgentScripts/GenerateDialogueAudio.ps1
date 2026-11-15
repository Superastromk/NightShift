# Regenerate only spoken dialogue from GenerateAudio.cs with the existing offline SAPI bridge.
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File AgentScripts/GenerateDialogueAudio.ps1
[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$TtsScript = "C:/Users/Owner/AppData/Roaming/kimi-desktop/daimon-share/daimon/runtime/kimi-code/home/plugins/managed/unity/skills/unity-content-sourcing/scripts/tts-sapi.ps1"
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}
$sourcePath = Join-Path $PSScriptRoot "GenerateAudio.cs"
$audioDir = Join-Path $ProjectRoot "Assets/Audio"
if (-not (Test-Path -LiteralPath $TtsScript -PathType Leaf)) {
    throw "Offline SAPI bridge not found: $TtsScript"
}
if (-not (Test-Path -LiteralPath $audioDir -PathType Container)) {
    throw "Audio directory not found: $audioDir"
}

# The C# table is the single source of truth for clip IDs, text, rate and volume.
$source = [IO.File]::ReadAllText($sourcePath)
$pattern = 'VoiceLine\("(?<id>[^"]+)",\s*"(?<text>(?:\\.|[^"\\])*)",\s*(?<rate>-?\d+),\s*(?<volume>\d+),\s*(?<fallback>[0-9.]+)f\);'
$lines = [regex]::Matches($source, $pattern)
if ($lines.Count -ne 16) {
    throw "Expected 16 VoiceLine entries in GenerateAudio.cs; found $($lines.Count)."
}

# SAPI pads its recordings. Keep a short natural margin, but do not let silence
# consume the fixed subtitle and scare pauses in the runtime sequences.
function Trim-SapiWave {
    param([Parameter(Mandatory = $true)][string]$Path)

    $bytes = [IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 44 -or [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne "RIFF" -or
        [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -ne "WAVE") {
        throw "Not a RIFF/WAVE file: $Path"
    }

    $cursor = 12
    $format = 0
    $channels = 0
    $sampleRate = 0
    $bits = 0
    $align = 0
    $dataOffset = -1
    $dataLength = 0
    while ($cursor + 8 -le $bytes.Length) {
        $chunk = [Text.Encoding]::ASCII.GetString($bytes, $cursor, 4)
        $chunkLength = [BitConverter]::ToInt32($bytes, $cursor + 4)
        if ($chunkLength -lt 0 -or $cursor + 8 + $chunkLength -gt $bytes.Length) {
            throw "Invalid WAV chunk in $Path"
        }
        if ($chunk -eq "fmt " -and $chunkLength -ge 16) {
            $format = [BitConverter]::ToInt16($bytes, $cursor + 8)
            $channels = [BitConverter]::ToInt16($bytes, $cursor + 10)
            $sampleRate = [BitConverter]::ToInt32($bytes, $cursor + 12)
            $align = [BitConverter]::ToInt16($bytes, $cursor + 20)
            $bits = [BitConverter]::ToInt16($bytes, $cursor + 22)
        }
        if ($chunk -eq "data") {
            $dataOffset = $cursor + 8
            $dataLength = $chunkLength
            break
        }
        $cursor += 8 + $chunkLength + ($chunkLength -band 1)
    }
    if ($format -ne 1 -or $bits -ne 16 -or $channels -lt 1 -or $sampleRate -lt 1 -or
        $align -ne $channels * 2 -or $dataOffset -lt 0 -or $dataLength -lt $align) {
        throw "Expected nonempty 16-bit PCM audio: $Path"
    }

    $frameCount = [int]($dataLength / $align)
    $first = -1
    $last = -1
    for ($frame = 0; $frame -lt $frameCount; $frame++) {
        $frameOffset = $dataOffset + $frame * $align
        for ($channel = 0; $channel -lt $channels; $channel++) {
            $sample = [BitConverter]::ToInt16($bytes, $frameOffset + $channel * 2)
            if ([Math]::Abs([int]$sample) -gt 160) {
                if ($first -lt 0) { $first = $frame }
                $last = $frame
            }
        }
    }
    if ($first -lt 0) { throw "SAPI produced silence: $Path" }

    $start = [Math]::Max(0, $first - [int]($sampleRate * 0.06))
    $end = [Math]::Min($frameCount, $last + [int]($sampleRate * 0.12) + 1)
    $newDataLength = ($end - $start) * $align
    $stream = New-Object IO.MemoryStream
    $writer = New-Object IO.BinaryWriter($stream)
    try {
        $writer.Write([Text.Encoding]::ASCII.GetBytes("RIFF"))
        $writer.Write([int](36 + $newDataLength))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("WAVE"))
        $writer.Write([Text.Encoding]::ASCII.GetBytes("fmt "))
        $writer.Write([int]16)
        $writer.Write([int16]1)
        $writer.Write([int16]$channels)
        $writer.Write([int]$sampleRate)
        $writer.Write([int]($sampleRate * $align))
        $writer.Write([int16]$align)
        $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes("data"))
        $writer.Write([int]$newDataLength)
        $writer.Write($bytes, $dataOffset + $start * $align, $newDataLength)
        $writer.Flush()
        [IO.File]::WriteAllBytes($Path, $stream.ToArray())
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

foreach ($line in $lines) {
    $id = $line.Groups["id"].Value
    if ($id -notmatch '^[A-Za-z0-9_]+$') { throw "Unsafe clip ID: $id" }
    $text = [regex]::Unescape($line.Groups["text"].Value)
    $rate = [int]$line.Groups["rate"].Value
    $volume = [int]$line.Groups["volume"].Value
    $output = Join-Path $audioDir "$id.wav"
    $temporary = Join-Path $audioDir "$id.new.wav"
    Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue

    try {
        & $TtsScript -Text $text -Out $temporary -Rate $rate -Volume $volume
        if (-not (Test-Path -LiteralPath $temporary -PathType Leaf)) {
            throw "SAPI produced no WAV for $id."
        }
        Trim-SapiWave -Path $temporary
        $info = Get-Item -LiteralPath $temporary
        if ($info.Length -le 2000) { throw "SAPI WAV for $id is empty or too short." }
        $bytes = [IO.File]::ReadAllBytes($temporary)
        if ($bytes.Length -lt 44 -or [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -ne "RIFF" -or
            [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -ne "WAVE") {
            throw "SAPI output for $id is not a RIFF/WAVE file."
        }
        Move-Item -LiteralPath $temporary -Destination $output -Force
        Write-Output "$id`t$($info.Length) bytes`t$text"
    }
    finally {
        Remove-Item -LiteralPath $temporary -Force -ErrorAction SilentlyContinue
    }
}
