using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorTools
{
    public static class GenerateAudio
    {
        const int SR=44100;
        const string AudioFolder="Assets/Audio";
        const string TtsPs1 = "C:/Users/Owner/AppData/Roaming/kimi-desktop/daimon-share/daimon/runtime/kimi-code/home/plugins/managed/unity/skills/unity-content-sourcing/scripts/tts-sapi.ps1";

        
        public static Dictionary<string, string> ClipMap { get; } = new Dictionary<string, string>();

        static readonly System.Text.StringBuilder _report = new System.Text.StringBuilder();

        public static string BuildAll()
        {
            _report.Length = 0;
            ClipMap.Clear();
            EnsureFolder("Assets", "Audio");

            
            {
                var d=Buf(60f);
                var r=new System.Random(1201);
                float brown=0f;
                for (int i=0; i < d.Length; i++) d[i] += BrownStep(r, ref brown) * 0.32f;
                AddSine(d, 120f, 0f, 60f, 0.045f, 0.5f, 0.5f); 
                AddSine(d, 240f, 0f, 60f, 0.015f, 0.5f, 0.5f);
                Thump(d, 9.5f, 52f, 0.45f, 0.22f);             
                Thump(d, 27.3f, 47f, 0.5f, 0.18f);
                Thump(d, 46.8f, 58f, 0.55f, 0.25f);
                WrapCrossfade(d, 1.0f);                        
                Normalize(d, 0.8f);
                Save(d, "ambience_office");
            }
            
            {
                var d = Buf(0.8f);
                var r = new System.Random(77);
                AddNoise(d,0f,0.12f,0.55f,0.002f,0.08f,0.4f,r);          
                AddGliss(d,196f,108f,0f,0.75f,0.5f,0.004f,0.15f);        
                AddGliss(d,220f,121f,0f,0.75f,0.45f,0.004f,0.15f);
                AddGliss(d,233f,128f,0f,0.75f,0.45f,0.004f,0.15f);
                AddGliss(d,311f,171f,0.02f,0.7f,0.3f,0.004f,0.15f);
                Normalize(d,0.9f);
                Save(d, "sting_death");
            }
            {
                var d = Buf(0.5f);
                var r = new System.Random(78);
                AddNoise(d, 0f, 0.5f, 0.22f, 0.25f, 0.2f, 0.9f, r);               
                AddGliss(d, 300f, 520f, 0f, 0.5f, 0.12f, 0.2f, 0.25f);            
                Normalize(d, 0.7f);
                Save(d, "sting_scare_soft");
            }
            {
                var d = Buf(0.9f);
                var r = new System.Random(79);
                AddNoise(d, 0f, 0.16f, 0.6f, 0.002f, 0.1f, 0.5f, r);              
                AddSaw(d, 880f, 95f, 0f, 0.9f, 0.55f, 0.003f, 0.2f, 26f, 14f);    
                AddSaw(d, 1245f, 134f, 0.01f, 0.85f, 0.3f, 0.003f, 0.2f, 31f, 22f);
                AddGliss(d, 1760f, 190f, 0f, 0.88f, 0.18f, 0.003f, 0.2f);         
                Normalize(d, 0.95f);
                Save(d, "sting_scare_hard");
            }
            
            float[] riser;
            {
                riser=Buf(2.5f);
                AddGliss(riser, 110f, 220f, 0f, 2.5f, 0.34f, 0.35f, 0.35f);       
                AddGliss(riser, 220f, 440f, 0.10f, 2.4f, 0.30f, 0.35f, 0.35f);    
                AddGliss(riser, 440f, 880f, 0.20f, 2.3f, 0.26f, 0.35f, 0.35f);    
                AddNoise(riser, 0f, 2.5f, 0.10f, 1.2f, 1.2f, 0.6f, new System.Random(80));
                Normalize(riser, 0.85f);
                Save(riser, "rewind_riser");                                      
                var rev = new float[riser.Length];                                
                for (int i = 0; i < riser.Length; i++) rev[i] = riser[riser.Length - 1 - i];
                Save(rev, "rewind_riser_rev");
            }
            {
                var d = Buf(0.4f);
                var r = new System.Random(81);
                AddNoise(d, 0f, 0.4f, 0.5f, 0.55f, 0.02f, 0.25f, r);              
                AddSine(d, 48f, 0.02f, 0.38f, 0.9f, 0.002f, 0.3f);                
                AddSine(d, 96f, 0.02f, 0.3f, 0.3f, 0.002f, 0.25f);
                Normalize(d, 0.95f);
                Save(d, "rewind_impact");
            }
            
            {
                var d = Buf(0.12f);
                AddNoise(d, 0f, 0.12f, 0.7f, 0.002f, 0.09f, 0.06f, new System.Random(82)); 
                Normalize(d, 0.7f);
                Save(d, "footstep");
            }
            {
                var d=Buf(3f);                                                  
                var r=new System.Random(83);
                float lp=0f;
                for (int i=0; i < d.Length; i++)
                {
                    float t=i / (float)d.Length;
                    float inOut=t < 0.45f ? t / 0.45f : 1f - (t - 0.45f) / 0.55f; 
                    float cutoff=Mathf.Lerp(0.02f, t < 0.45f ? 0.12f : 0.05f, inOut);
                    lp += cutoff * ((float)r.NextDouble() * 2f - 1f - lp);
                    float env=Mathf.Pow(Mathf.Clamp01(inOut), 1.4f);
                    d[i] += lp * 4.5f * env;
                }
                Normalize(d, 0.75f);
                Save(d, "monster_breath");
            }
            {
                var d = Buf(1.2f);                                                
                AddSine(d,440f,0f,1.2f,0.38f,0.004f,1.1f);
                AddSine(d,480f,0f,1.2f,0.38f,0.004f,1.1f);
                AddSine(d,880f,0f,1.2f,0.10f,0.004f,1.0f);
                AddSine(d,960f,0f,1.2f,0.08f,0.004f,1.0f);
                Normalize(d,0.8f);
                Save(d, "phone_ring");
            }
            {
                var d = Buf(2f);
                var r = new System.Random(84);
                AddGliss(d, 320f, 700f, 0f, 0.8f, 0.10f, 0.05f, 0.1f);            
                AddGliss(d, 700f, 350f, 1.1f, 0.7f, 0.08f, 0.1f, 0.15f);
                for (float bt = 0.25f; bt < 1.9f; bt += 0.18f)                    
                    AddSquare(d, 1400f + (float)r.NextDouble() * 300f, bt, 0.06f, 0.16f, 0.004f, 0.03f);
                Normalize(d, 0.75f);
                Save(d, "printer");
            }
            {
                var d = Buf(0.7f);                                                
                AddSine(d, 880f, 0f, 0.7f, 0.45f, 0.004f, 0.55f);
                AddSine(d, 1320f, 0f, 0.7f, 0.30f, 0.004f, 0.5f);
                AddSine(d, 1760f, 0f, 0.7f, 0.10f, 0.004f, 0.45f);
                Normalize(d, 0.8f);
                Save(d, "elevator_ding");
            }
            {
                var d=Buf(0.05f);                                               
                AddSine(d, 1800f, 0f, 0.05f, 0.55f, 0.001f, 0.045f);
                AddNoise(d, 0f, 0.012f, 0.25f, 0.001f, 0.01f, 1f, new System.Random(85));
                Normalize(d, 0.8f);
                Save(d, "ui_click");
            }

VoiceLine("voice_meeting", "You keep showing up to my meetings.", -4, 100, 3.0f);
            VoiceLine("voice_im_behind_you", "I'm standing right behind you.",-2,62,2.0f);
            VoiceLine("voice_first_day", "Sorry about that. First day.", -1, 92, 2.0f);
            VoiceLine("voice_got_a_minute", "Got a minute? Just one.",4,72,2.4f);
            VoiceLine("sys_power", "A power fault has been logged. Emergency lighting is offline. Please continue your shift.", 4, 88, 6.5f);
            VoiceLine("intercom_act2", "Temporal Recovery is an employee-only service. Please ensure any further deaths are strictly necessary.",-2,88,6.0f);
            VoiceLine("intercom_act3", "That concludes your shift. Take the nearest elevator and keep your eyes forward.", -3, 90, 5.0f);
            VoiceLine("voice_going_down", "Going down, are we?",3,90,1.8f);
            VoiceLine("voice_voicemail", "It's me. Well, you. Don't get in that elevator.", 3, 88, 3.8f);
            VoiceLine("voice_took_elevator", "You got in, didn't you? You never listen.",3,88,3.0f);
            VoiceLine("voice_same_time", "Same shift tomorrow. Don't be late.", -3, 90, 2.5f);
            VoiceLine("sys_recovery_1", "Your death has been reversed. Your workstation is expecting you.",-2,90,4.0f);
            VoiceLine("sys_recovery_2", "A second death has been reversed. Please treat this as your final warning.", -2, 90, 4.5f);
            VoiceLine("sys_recovery_3", "Third death recorded. Repeated mortality has been noted in your personnel file.",-2,90,4.5f);
            VoiceLine("sys_recovery_4", "Each additional death will now be charged to your annual leave.", -2, 90, 4.0f);
            VoiceLine("sys_recovery_5", "Death reversed. You are familiar with the procedure.",-2,90,3.5f);

            
            string jsonPath = Path.Combine(Application.dataPath, "Audio/clipmap.json");
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            int idx = 0;
            foreach (var kv in ClipMap)
            {
                sb.Append($"  \"{kv.Key}\": \"{kv.Value}\"");
                sb.Append(++idx < ClipMap.Count ? ",\n" : "\n");
            }
            sb.Append("}\n");
            File.WriteAllText(jsonPath, sb.ToString());
            AssetDatabase.ImportAsset(AudioFolder + "/clipmap.json");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return _report.ToString();
        }

static void VoiceLine(string id, string text, int rate, int volume, float fallbackSeconds)
        {
            string assetPath=$"{AudioFolder}/{id}.wav";
            string absOut = Path.Combine(Application.dataPath,$"Audio/{id}.wav");
            bool ok = TrySapi(text, absOut, rate, volume);
            AudioClip clip = null;
            if (ok)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            }
            if (clip == null)
            {
                if (ok) _report.AppendLine($"{id}: TTS wav failed to import; used synth fallback");
                var d = Murmur(text, fallbackSeconds);
                Normalize(d, 0.8f);
                Save(d, id);
            }
            else
            {
                ClipMap[id] = assetPath;
                _report.AppendLine($"{id}: {clip.length:F2}s (SAPI TTS)");
            }
        }

        static bool TrySapi(string text, string absOut, int rate, int volume)
        {
            try
            {
                if (!File.Exists(TtsPs1)) return false;
                var psi=new ProcessStartInfo
                {
                    FileName="powershell",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                };
                psi.ArgumentList.Add("-NoProfile");
                psi.ArgumentList.Add("-ExecutionPolicy"); psi.ArgumentList.Add("Bypass");
                psi.ArgumentList.Add("-File"); psi.ArgumentList.Add(TtsPs1);
                psi.ArgumentList.Add("-Text"); psi.ArgumentList.Add(text);
                psi.ArgumentList.Add("-Out"); psi.ArgumentList.Add(absOut);
                psi.ArgumentList.Add("-Rate"); psi.ArgumentList.Add(rate.ToString());
                psi.ArgumentList.Add("-Volume"); psi.ArgumentList.Add(volume.ToString());
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    if (!p.WaitForExit(45000))
                    {
                        try { p.Kill(); } catch { }
                        return false;
                    }
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    bool ok = p.ExitCode == 0 && File.Exists(absOut) && new FileInfo(absOut).Length > 2000;
                    if (!ok) UnityEngine.Debug.LogWarning($"TEL WARN TTS failed ({idOf(text)}): {stderr.Trim()} {stdout.Trim()}");
                    return ok;
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning("TEL WARN TTS exception: " + e.Message);
                return false;
            }
        }

        static string idOf(string text) => text.Length > 12 ? text.Substring(0, 12) + "..." : text;

        
        static float[] Murmur(string seedText, float seconds)
        {
            var d=Buf(seconds);
            var r=new System.Random(seedText.GetHashCode());
            int nSyl=Mathf.Clamp(seedText.Split(' ').Length + 1,3,6);
            float t = 0.08f;
            for (int s = 0; s < nSyl && t < seconds - 0.25f; s++)
            {
                float sylDur = 0.16f + (float)r.NextDouble() * 0.12f;
                float f0 = 82f + (float)r.NextDouble() * 45f;
                float f1 = 380f + (float)r.NextDouble() * 320f;
                float f2 = 1100f + (float)r.NextDouble() * 900f;
                int start = (int)(t * SR),n = Mathf.Min((int)(sylDur * SR),d.Length - start);
                var src = new float[n];
                double ph = 0.0;
                for (int i = 0; i < n; i++)
                {
                    float ft = i / (float)n;
                    float freq = f0 * (1f + 0.08f * Mathf.Sin(ft * Mathf.PI)); 
                    ph += 2.0 * Math.PI * freq / SR;
                    double saw = 2.0 * (ph / (2.0 * Math.PI) - Math.Floor(ph / (2.0 * Math.PI))) - 1.0;
                    float env = Mathf.Sin(ft * Mathf.PI);                       
                    src[i] = (float)saw * env * 0.6f;
                }
                Resonate(src,d,start,f1,0.965f);
                Resonate(src,d,start,f2,0.94f);
                for (int i = 0; i < n; i++) d[start + i] += src[i] * 0.10f;      
                t += sylDur + 0.05f + (float)r.NextDouble() * 0.06f;
            }
            return d;
        }

        
        static void Resonate(float[] src,float[] dst,int start,float freq,float r)
        {
            double a1 = 2.0 * r * Math.Cos(2.0 * Math.PI * freq / SR);
            double a2 = -r * r;
            double y1 = 0.0,y2 = 0.0;
            for (int i = 0; i < src.Length && start + i < dst.Length; i++)
            {
                double y = src[i] + a1 * y1 + a2 * y2;
                dst[start + i] += (float)y * 0.06f;
                y2 = y1; y1 = y;
            }
        }

static float[] Buf(float seconds) => new float[Mathf.Max(1,(int)(SR * seconds))];

        static void AddSine(float[] d,float freq,float t0,float dur,float amp,float attackSec,float releaseSec)
        {
            int start = Mathf.Clamp((int)(t0 * SR),0,d.Length - 1);
            int n = Mathf.Min((int)(dur * SR),d.Length - start);
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                ph += 2.0 * Math.PI * freq / SR;
                d[start + i] += (float)Math.Sin(ph) * amp * AREnv(i,n,attackSec,releaseSec);
            }
        }

        static void AddGliss(float[] d,float f0,float f1,float t0,float dur,float amp,float attackSec,float releaseSec)
        {
            int start = Mathf.Clamp((int)(t0 * SR),0,d.Length - 1);
            int n = Mathf.Min((int)(dur * SR),d.Length - start);
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float ft = i / (float)n;
                double freq = Mathf.Lerp(f0,f1,ft);
                ph += 2.0 * Math.PI * freq / SR;
                d[start + i] += (float)Math.Sin(ph) * amp * AREnv(i,n,attackSec,releaseSec);
            }
        }

        static void AddSaw(float[] d,float f0,float f1,float t0,float dur,float amp,float attackSec,float releaseSec,float vibHz,float vibDepth)
        {
            int start = Mathf.Clamp((int)(t0 * SR),0,d.Length - 1);
            int n = Mathf.Min((int)(dur * SR),d.Length - start);
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                float ft = i / (float)n;
                double freq = Mathf.Lerp(f0,f1,ft) + vibDepth * Math.Sin(2.0 * Math.PI * vibHz * i / SR);
                ph += 2.0 * Math.PI * freq / SR;
                double saw = 2.0 * (ph / (2.0 * Math.PI) - Math.Floor(ph / (2.0 * Math.PI))) - 1.0;
                d[start + i] += (float)saw * amp * AREnv(i,n,attackSec,releaseSec);
            }
        }

        static void AddSquare(float[] d,float freq,float t0,float dur,float amp,float attackSec,float releaseSec)
        {
            int start = Mathf.Clamp((int)(t0 * SR),0,d.Length - 1);
            int n = Mathf.Min((int)(dur * SR),d.Length - start);
            double ph = 0.0;
            for (int i = 0; i < n; i++)
            {
                ph += 2.0 * Math.PI * freq / SR;
                float sq = Math.Sin(ph) >= 0.0 ? 1f : -1f;
                d[start + i] += sq * amp * AREnv(i,n,attackSec,releaseSec);
            }
        }

        
        static void AddNoise(float[] d,float t0,float dur,float amp,float attackSec,float releaseSec,float lp,System.Random r)
        {
            int start = Mathf.Clamp((int)(t0 * SR),0,d.Length - 1);
            int n = Mathf.Min((int)(dur * SR),d.Length - start);
            float state = 0f;
            for (int i = 0; i < n; i++)
            {
                state += lp * ((float)r.NextDouble() * 2f - 1f - state);
                d[start + i] += state * 1.8f * amp * AREnv(i,n,attackSec,releaseSec);
            }
        }

        static void Thump(float[] d,float t0,float freq,float dur,float amp)
        {
            int start = Mathf.Clamp((int)(t0 * SR),0,d.Length - 1);
            int n = Mathf.Min((int)(dur * SR),d.Length - start);
            double ph = 0.0;
            var r = new System.Random((int)(t0 * 1000f));
            for (int i = 0; i < n; i++)
            {
                float ft = i / (float)n;
                ph += 2.0 * Math.PI * freq / SR;
                float env = Mathf.Exp(-4f * ft) * Mathf.Min(1f,ft * 40f);
                d[start + i] += ((float)Math.Sin(ph) + (float)(r.NextDouble() * 2.0 - 1.0) * 0.4f) * amp * env;
            }
        }

        static float AREnv(int i,int n,float attackSec,float releaseSec)
        {
            float a = attackSec * SR,rel = releaseSec * SR;
            float env = 1f;
            if (a > 0f && i < a) env = i / a;
            float fromEnd = n - 1 - i;
            if (rel > 0f && fromEnd < rel) env *= Mathf.Clamp01(fromEnd / rel);
            return env;
        }

        static float BrownStep(System.Random r,ref float state)
        {
            state = (state + 0.02f * ((float)r.NextDouble() * 2f - 1f)) / 1.02f;
            return state * 3.5f;
        }

        
        static void WrapCrossfade(float[] d,float fadeSec)
        {
            int f = Mathf.Min((int)(fadeSec * SR),d.Length / 2);
            for (int i = 0; i < f; i++)
            {
                float t = i / (float)f;
                d[d.Length - f + i] = Mathf.Lerp(d[i],d[d.Length - f + i],t);
            }
        }

        static void Normalize(float[] d,float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max,Mathf.Abs(d[i]));
            float g = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i] * g,-1f,1f);
        }

static void Save(float[] samples,string clipId)
        {
            
            
            string path = $"{AudioFolder}/{clipId}.wav";
            WriteWav(path, samples);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            ClipMap[clipId] = path;
            _report.AppendLine($"{clipId}: {(clip != null ? clip.length : 0f):F2}s");
        }

        static void WriteWav(string path, float[] samples)
        {
            int n = samples.Length;
            using (var fs = new System.IO.FileStream(path, System.IO.FileMode.Create))
            using (var w = new System.IO.BinaryWriter(fs))
            {
                void Chunk(string id, int size)
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes(id));
                    w.Write(size);
                }
                int dataBytes = n * 2;
                Chunk("RIFF", 36 + dataBytes);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                Chunk("fmt ", 16);
                w.Write((short)1);          
                w.Write((short)1);          
                w.Write(SR);
                w.Write(SR * 2);            
                w.Write((short)2);          
                w.Write((short)16);         
                Chunk("data", dataBytes);
                for (int i = 0; i < n; i++)
                {
                    float s = Mathf.Clamp(samples[i], -1f, 1f);
                    w.Write((short)(s < 0 ? s * 0x8000 : s * 0x7FFF));
                }
            }
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
