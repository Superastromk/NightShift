using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NightShift.Core
{

public static class GameSettings
    {
        
        public static float Master = 0.9f,Ambience = 0.8f,Sfx = 1f,Voice = 1f;
        
        public static float Sensitivity = 1f;   
        public static bool InvertY = false;
        public static bool HoldToCrouch = false;
        
        public static float Fov = 70f;          
        public static bool HeadBob = true;
        public static bool Subtitles = true;
        public static bool ScreenShake = true;
        public static bool ShowFps = false;
        
        public static int Quality = 1;          
        public static float RenderScale = 1f;   
        public static bool Fullscreen = true;
        public static bool VSync = true;
        public static int FpsCap = 0;           
        public static float Brightness = 0f;    
        public static bool FilmGrain = true;

        public static readonly int[] FpsCaps = { -1,30,60,120 };
        public static readonly string[] FpsCapNames = { "Unlimited", "30", "60", "120" };
        public static readonly string[] QualityNames = { "Low", "Medium", "High" };

        public static event Action Changed;
        public static bool Loaded { get; private set; }

        const string K="ns_";

        public static void Load()
        {
            if (Loaded) return;
            Loaded = true;
            if (!PlayerPrefs.HasKey(K + "quality")) AutoDetect();
            Master = PlayerPrefs.GetFloat(K + "master", Master);
            Ambience = PlayerPrefs.GetFloat(K + "ambience", Ambience);
            Sfx=PlayerPrefs.GetFloat(K + "sfx",Sfx);
            Voice = PlayerPrefs.GetFloat(K + "voice", Voice);
            Sensitivity = PlayerPrefs.GetFloat(K + "sens", Sensitivity);
            InvertY = PlayerPrefs.GetInt(K + "inverty", 0) == 1;
            HoldToCrouch=PlayerPrefs.GetInt(K + "holdcrouch",0) == 1;
            Fov = PlayerPrefs.GetFloat(K + "fov", Fov);
            HeadBob = PlayerPrefs.GetInt(K + "bob", 1) == 1;
            Subtitles = PlayerPrefs.GetInt(K + "subs", 1) == 1;
            ScreenShake=PlayerPrefs.GetInt(K + "shake",1) == 1;
            ShowFps = PlayerPrefs.GetInt(K + "fps", 0) == 1;
            Quality = Mathf.Clamp(PlayerPrefs.GetInt(K + "quality", Quality), 0, 2);
            RenderScale = Mathf.Clamp(PlayerPrefs.GetFloat(K + "scale", RenderScale), 0.5f, 1f);
            Fullscreen=PlayerPrefs.GetInt(K + "fullscreen",Fullscreen ? 1 : 0) == 1;
            VSync = PlayerPrefs.GetInt(K + "vsync", VSync ? 1 : 0) == 1;
            FpsCap = Mathf.Clamp(PlayerPrefs.GetInt(K + "fpscap", FpsCap), 0, FpsCaps.Length - 1);
            Brightness = PlayerPrefs.GetFloat(K + "bright", Brightness);
            FilmGrain=PlayerPrefs.GetInt(K + "grain",1) == 1;

            
            if (PlayerPrefs.GetInt(K + "detect", 0) < DetectVersion)
            {
                AutoDetect();
                PlayerPrefs.SetInt(K + "detect", DetectVersion);
                Save();
            }
        }

        const int DetectVersion = 2;

        public static void Save()
        {
            PlayerPrefs.SetFloat(K + "master", Master);
            PlayerPrefs.SetFloat(K + "ambience",Ambience);
            PlayerPrefs.SetFloat(K + "sfx", Sfx);
            PlayerPrefs.SetFloat(K + "voice", Voice);
            PlayerPrefs.SetFloat(K + "sens", Sensitivity);
            PlayerPrefs.SetInt(K + "inverty",InvertY ? 1 : 0);
            PlayerPrefs.SetInt(K + "holdcrouch", HoldToCrouch ? 1 : 0);
            PlayerPrefs.SetFloat(K + "fov", Fov);
            PlayerPrefs.SetInt(K + "bob", HeadBob ? 1 : 0);
            PlayerPrefs.SetInt(K + "subs",Subtitles ? 1 : 0);
            PlayerPrefs.SetInt(K + "shake", ScreenShake ? 1 : 0);
            PlayerPrefs.SetInt(K + "fps", ShowFps ? 1 : 0);
            PlayerPrefs.SetInt(K + "quality", Quality);
            PlayerPrefs.SetFloat(K + "scale",RenderScale);
            PlayerPrefs.SetInt(K + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(K + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(K + "fpscap", FpsCap);
            PlayerPrefs.SetFloat(K + "bright",Brightness);
            PlayerPrefs.SetInt(K + "grain", FilmGrain ? 1 : 0);
            PlayerPrefs.Save();
        }

        
        public static void AutoDetect()
        {
            int vram = SystemInfo.graphicsMemorySize;       
            int cores = SystemInfo.processorCount;
            int ram = SystemInfo.systemMemorySize;
            bool integrated = IsIntegratedGpu(SystemInfo.graphicsDeviceName);
            if (cores <= 4 || ram < 7000 || vram < 2000 || SystemInfo.graphicsShaderLevel < 45) { Quality = 0; RenderScale = 0.75f; }
            else if (integrated) { Quality = 1; RenderScale = 0.8f; }
            else if (vram < 5000 || cores < 8) { Quality = 1; RenderScale = 1f; }
            else { Quality = 2; RenderScale = 1f; }
            Debug.Log($"TEL SETTINGS autodetect quality={Quality} scale={RenderScale:0.00} gpu='{SystemInfo.graphicsDeviceName}' vram={vram} cores={cores} ram={ram}");
        }

static bool IsIntegratedGpu(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string n = name.ToUpperInvariant();
            if (n.Contains("RTX") || n.Contains("GTX") || n.Contains(" RX ") || n.Contains("RADEON PRO") || n.Contains("ARC A")) return false;
            if (n.Contains("INTEL") || n.Contains("UHD") || n.Contains("IRIS") || n.Contains("VEGA")) return true;
            
            if (n.Contains("RADEON") && (n.Contains("(TM) GRAPHICS") || System.Text.RegularExpressions.Regex.IsMatch(n, @"\b\d{3}M\b"))) return true;
            return SystemInfo.graphicsMemorySize < 3000;
        }

        public static void ResetDefaults()
        {
            Master = 0.9f; Ambience = 0.8f; Sfx = 1f; Voice = 1f;
            Sensitivity = 1f; InvertY = false; HoldToCrouch = false;
            Fov = 70f; HeadBob = true; Subtitles = true; ScreenShake = true; ShowFps = false;
            Fullscreen = true; VSync = true; FpsCap = 0; Brightness = 0f; FilmGrain = true;
            AutoDetect();
        }

        
        public static void Apply(bool applyDisplay = true)
        {
            AudioListener.volume = Mathf.Clamp01(Master);

            int levels = QualitySettings.names.Length;
            int q = Mathf.Clamp(Quality, 0, Mathf.Max(0, levels - 1));
            if (QualitySettings.GetQualityLevel() != q) QualitySettings.SetQualityLevel(q, true);
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) urp.renderScale = Mathf.Clamp(RenderScale, 0.5f, 1f);

            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FpsCaps[Mathf.Clamp(FpsCap, 0, FpsCaps.Length - 1)];

            if (applyDisplay && !Application.isEditor)
            {
                var mode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                if (Screen.fullScreenMode != mode)
                {
                    if (Fullscreen) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, mode);
                    else Screen.SetResolution(1280, 720, mode);
                }
            }
            Changed?.Invoke();
        }
    }
}
