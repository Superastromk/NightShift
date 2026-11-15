using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using NightShift.Core;
using NightShift.UI;
using TMPro;

namespace NightShift.EditorTools
{
    public static class BuildUIRoot
    {
        
        static readonly Dictionary<string, GameObject> Named = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        static readonly System.Text.StringBuilder _report = new System.Text.StringBuilder();

        public static string Build()
        {
            _report.Length = 0;
            Named.Clear();

            
            var old = GameObject.Find("UIRoot");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var rootGo = new GameObject("UIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas=rootGo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=100;
            var scaler=rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920f, 1080f);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight=0.5f;
            Name(rootGo);

            
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                _report.AppendLine("EventSystem: created (InputSystemUIInputModule)");
            }

            EnsureFolder("Assets", "UI");

            
            var title=MakePanel(rootGo.transform, "TitlePanel",true);
            var titleText = MakeText(title.transform, "TitleText", "THE NIGHT SHIFT", 84, Color.white, TextAlignmentOptions.Center);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, 190f);
            titleText.rectTransform.sizeDelta = new Vector2(1600f, 140f);
            titleText.characterSpacing = 6f;
            Name(titleText.gameObject);
            var sub = MakeText(title.transform, "SubtitleText", "You get to go back. It doesn't.",30,new Color(0.62f,0.66f,0.72f),TextAlignmentOptions.Center);
            sub.fontStyle = FontStyles.Italic;
            sub.rectTransform.anchoredPosition = new Vector2(0f,105f);
            sub.rectTransform.sizeDelta = new Vector2(1400f,60f);
            Name(sub.gameObject);
            var startBtn = MakeButton(title.transform, "StartShiftButton", "START SHIFT", new Vector2(0f, -60f), new Vector2(340f, 72f));
            Name(startBtn.gameObject); Named["StartButton"]=startBtn.gameObject;
            var quitBtn=MakeButton(title.transform, "QuitButton", "QUIT", new Vector2(0f, -160f), new Vector2(340f, 72f));
            Name(quitBtn.gameObject);

            
            var pause = MakePanel(rootGo.transform, "PausePanel", false);
            var pausedText = MakeText(pause.transform, "PausedText", "PAUSED",72,Color.white,TextAlignmentOptions.Center);
            pausedText.rectTransform.anchoredPosition = new Vector2(0f,130f);
            pausedText.rectTransform.sizeDelta = new Vector2(1200f,110f);
            Name(pausedText.gameObject);
            var resumeBtn = MakeButton(pause.transform, "ResumeButton", "RESUME", new Vector2(0f, -20f), new Vector2(340f, 72f));
            Name(resumeBtn.gameObject);
            var quitToTitleBtn = MakeButton(pause.transform, "QuitToTitleButton", "QUIT TO TITLE",new Vector2(0f,-120f),new Vector2(340f,72f));
            Name(quitToTitleBtn.gameObject); Named["QuitButton"] = quitToTitleBtn.gameObject;

            
            var hud = MakePanel(rootGo.transform, "HUD", true);
            Named["HUDRoot"]=hud; Named["HUDPanel"] = hud;

            var objText = MakeText(hud.transform, "ObjectiveText", "", 28, new Color32(0xD8, 0xDE, 0xE9, 0xFF), TextAlignmentOptions.BottomLeft);
            var objRt = objText.rectTransform;
            objRt.anchorMin = new Vector2(0f, 0f); objRt.anchorMax = new Vector2(0f, 0f);
            objRt.pivot = new Vector2(0f, 0f);
            objRt.anchoredPosition = new Vector2(40f, 40f);
            objRt.sizeDelta = new Vector2(900f, 90f);
            Name(objText.gameObject);

            var tlText = MakeText(hud.transform, "TimelineText", "TIMELINE 00",26,new Color32(0xD8,0xDE,0xE9,0xFF),TextAlignmentOptions.BottomRight);
            var tlRt = tlText.rectTransform;
            tlRt.anchorMin = new Vector2(1f,0f); tlRt.anchorMax = new Vector2(1f,0f);
            tlRt.pivot = new Vector2(1f,0f);
            tlRt.anchoredPosition = new Vector2(-40f,40f);
            tlRt.sizeDelta = new Vector2(400f,60f);
            Name(tlText.gameObject);

            var promptBgGo = MakeImage(hud.transform, "PromptBackground", new Color(0f, 0f, 0f, 0.55f));
            var promptBgRt = (RectTransform)promptBgGo.transform;
            promptBgRt.anchorMin = promptBgRt.anchorMax = new Vector2(0.5f, 0.5f);
            promptBgRt.pivot = new Vector2(0.5f, 0.5f);
            promptBgRt.anchoredPosition = new Vector2(0f, -110f);
            promptBgRt.sizeDelta = new Vector2(1040f, 76f);
            Name(promptBgGo);
            var promptText = MakeText(hud.transform, "PromptText", "", 30, Color.white, TextAlignmentOptions.Center);
            var promptRt=promptText.rectTransform;
            promptRt.anchorMin=promptRt.anchorMax=new Vector2(0.5f, 0.5f);
            promptRt.pivot=new Vector2(0.5f, 0.5f);
            promptRt.anchoredPosition=new Vector2(0f, -110f);
            promptRt.sizeDelta=new Vector2(1000f, 60f);
            Name(promptText.gameObject);

            var compGo=new GameObject("ComposureBar",typeof(RectTransform));
            compGo.transform.SetParent(hud.transform,false);
            var compRt = (RectTransform)compGo.transform;
            compRt.anchorMin = compRt.anchorMax = new Vector2(0.5f,0f);
            compRt.pivot = new Vector2(0.5f,0.5f);
            compRt.anchoredPosition = new Vector2(0f,26f);
            compRt.sizeDelta = new Vector2(420f,8f);
            Name(compGo);
            var compBg = MakeImage(compGo.transform, "ComposureBarBackground", new Color(0.07f, 0.07f, 0.09f, 0.9f));
            Stretch(compBg);
            var compFillGo = MakeImage(compGo.transform, "ComposureBarFill", Hex(0xB03030));
            Stretch(compFillGo);
            var compFill = compFillGo.GetComponent<Image>();
            compFill.type = Image.Type.Filled;
            compFill.fillMethod = Image.FillMethod.Horizontal;
            compFill.fillOrigin = 0;
            compFill.fillAmount = 1f;
            Named["ComposureFill"]=compFillGo;

            var vignetteSprite=EnsureVignetteSprite();
            var vignetteGo=MakeImage(hud.transform, "MonsterVignette",new Color(0f,0f,0f,0f));
            var vignetteImg = vignetteGo.GetComponent<Image>();
            if (vignetteSprite != null) vignetteImg.sprite = vignetteSprite;
            Stretch(vignetteGo);
            vignetteImg.raycastTarget = false;
            Name(vignetteGo); Named["Vignette"] = vignetteGo;

            
            var overlayGo = MakeImage(rootGo.transform, "RewindOverlay", new Color(1f, 1f, 1f, 0f));
            Stretch(overlayGo);
            overlayGo.GetComponent<Image>().raycastTarget = false;
            Name(overlayGo); Named["RewindFlash"]=overlayGo;

            
            var ending=MakePanel(rootGo.transform, "EndingPanel",false);
            var endTitle = MakeText(ending.transform, "EndingTitleText", "YOUR LAST SHIFT", 64, Color.white, TextAlignmentOptions.Center);
            endTitle.rectTransform.anchoredPosition = new Vector2(0f, 150f);
            endTitle.rectTransform.sizeDelta = new Vector2(1600f, 120f);
            endTitle.characterSpacing = 4f;
            Name(endTitle.gameObject);
            var endBody = MakeText(ending.transform, "EndingTimelineText", "TIMELINE 00",34,new Color(0.85f,0.85f,0.88f),TextAlignmentOptions.Center);
            endBody.rectTransform.anchoredPosition = new Vector2(0f,30f);
            endBody.rectTransform.sizeDelta = new Vector2(1000f,60f);
            Name(endBody.gameObject);
            Named["EndingBody"] = endBody.gameObject; Named["EndingText"] = endBody.gameObject; Named["TimelineCountText"]=endBody.gameObject;
            var endQuitBtn=MakeButton(ending.transform, "QuitButton", "QUIT", new Vector2(0f, -110f), new Vector2(340f, 72f));
            Name(endQuitBtn.gameObject);

            
            var gs = GameObject.Find("GameSystems");
            if (gs == null)
            {
                var gm = UnityEngine.Object.FindFirstObjectByType<GameManager>();
                gs = gm != null ? gm.gameObject : new GameObject("GameSystems");
                _report.AppendLine("GameSystems: " + (gm != null ? "reused existing" : "created"));
            }
            var ui = gs.GetComponent<UIManager>() ?? gs.AddComponent<UIManager>();
            Name(startBtn.gameObject); 

            
            int wired = WireRefs(ui, out var unwired);
            _report.AppendLine($"UIManager fields wired: {wired}");
            foreach (var u in unwired) _report.AppendLine("UIManager unwired: " + u);

            
            Bind(startBtn,ui, "StartGame", "StartShift", "OnStartClicked", "OnStartShift", "HandleStart");
            Bind(resumeBtn, ui, "Resume", "ResumeGame", "OnResumeClicked", "OnResume", "HandleResume");
            Bind(quitBtn, ui, "Quit", "QuitGame", "OnQuitClicked", "OnQuit", "HandleQuit", "QuitToTitle");
            Bind(quitToTitleBtn,ui, "QuitToTitle", "QuitToTitleScreen", "OnQuitToTitle", "Quit");
            Bind(endQuitBtn,ui, "Quit", "QuitGame", "OnQuitClicked", "OnQuit", "QuitToTitle");

            EditorUtility.SetDirty(rootGo);
            EditorUtility.SetDirty(gs);
            return _report.ToString();
        }

static int WireRefs(UIManager ui, out List<string> unwired)
        {
            int wired = 0;
            unwired = new List<string>();
            foreach (var f in typeof(UIManager).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                bool serialized = f.IsPublic || f.GetCustomAttribute<UnityEngine.SerializeField>() != null;
                if (!serialized) continue;
                if (f.IsNotSerialized && !f.IsPublic) continue;
                if (f.IsLiteral || f.IsStatic) continue;

                if (Named.TryGetValue(f.Name, out var go))
                {
                    object val;
                    if (f.FieldType == typeof(GameObject)) val = go;
                    else if (f.FieldType.IsSubclassOf(typeof(Component)) || f.FieldType == typeof(Component))
                    {
                        val = go.GetComponent(f.FieldType);
                        if (val == null && f.FieldType.IsAssignableFrom(typeof(Image))) val = go.GetComponent<Image>();
                    }
                    else if (f.FieldType.IsInterface)
                    {
                        val = go.GetComponent(f.FieldType);
                    }
                    else continue;

                    if (val != null && f.FieldType.IsInstanceOfType(val))
                    {
                        f.SetValue(ui, val);
                        wired++;
                        continue;
                    }
                    unwired.Add($"{f.Name} (no compatible component on {go.name})");
                    continue;
                }

                if (typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType) && f.IsPublic)
                {
                    var cur = f.GetValue(ui) as UnityEngine.Object;
                    if (cur == null) unwired.Add(f.Name + " (no matching object)");
                }
            }
            return wired;
        }

        static void Bind(Button b, UIManager ui, params string[] methodNames)
        {
            foreach (var m in typeof(UIManager).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.ReturnType != typeof(void) || m.GetParameters().Length != 0) continue;
                foreach (var want in methodNames)
                {
                    if (!string.Equals(m.Name, want, StringComparison.OrdinalIgnoreCase)) continue;
                    var act=(UnityAction)Delegate.CreateDelegate(typeof(UnityAction), ui, m);
                    UnityEventTools.AddPersistentListener(b.onClick, act);
                    EditorUtility.SetDirty(b);
                    _report.AppendLine($"{b.gameObject.name}.onClick -> UIManager.{m.Name}");
                    return;
                }
            }
            _report.AppendLine($"{b.gameObject.name}.onClick: UNBOUND (no matching UIManager method; wire to GameManager.SetState at runtime)");
        }

static void Name(GameObject go) { Named[go.name] = go; }

        static GameObject MakePanel(Transform parent, string name, bool visible)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            Stretch(go);
            var cg = go.GetComponent<CanvasGroup>();
            cg.alpha = visible ? 1f : 0f;
            cg.interactable = visible;
            cg.blocksRaycasts = visible;
            Name(go);
            _report.AppendLine("panel: " + name + (visible ? " (visible)" : " (hidden)"));
            return go;
        }

        static GameObject MakeImage(Transform parent,string name,Color color)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(parent,false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            Name(go);
            return go;
        }

        static TextMeshProUGUI MakeText(Transform parent,string name,string content,int fontSize,Color color,TextAlignmentOptions align)
        {
            var go = new GameObject(name,typeof(RectTransform));
            go.transform.SetParent(parent,false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = content;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = align;
            var fa = TMP_Settings.defaultFontAsset;
            if (fa != null) t.font = fa;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent,string name,string label,Vector2 anchoredPos,Vector2 size)
        {
            var go = new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(parent,false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f,0.5f);
            rt.pivot = new Vector2(0.5f,0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f,0.13f,0.16f,0.92f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.32f,0.35f,0.42f);
            cb.pressedColor = new Color(0.5f,0.2f,0.2f);
            cb.fadeDuration = 0.08f;
            btn.colors = cb;

            var txtGo = new GameObject("Text", typeof(RectTransform));
            txtGo.transform.SetParent(go.transform, false);
            Stretch(txtGo);
            var t = txtGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 28;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.fontStyle = FontStyles.Bold;
            var fa = TMP_Settings.defaultFontAsset;
            if (fa != null) t.font = fa;
            t.raycastTarget = false;
            return btn;
        }

        static void Stretch(GameObject go)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Color Hex(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        
        static Sprite EnsureVignetteSprite()
        {
            const string path = "Assets/UI/MonsterVignette.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S - 0.5f;
                    float dy = (y + 0.5f) / S - 0.5f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) * 2f); 
                    float a = Mathf.Clamp01((d - 0.35f) / 0.65f);
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a * a));
                }
            }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Application.dataPath, "UI/MonsterVignette.png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var imp=AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null)
            {
                imp.textureType=TextureImporterType.Sprite;
                imp.spriteImportMode=SpriteImportMode.Single;
                imp.alphaIsTransparency=true;
                imp.SaveAndReimport();
            }
            var spr=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            _report.AppendLine("vignette sprite: " + (spr != null ? "generated Assets/UI/MonsterVignette.png" : "FAILED"));
            return spr;
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
