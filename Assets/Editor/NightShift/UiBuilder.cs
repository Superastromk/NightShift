#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using NightShift.UI;

namespace NightShift.EditorBuild
{

public static class UiBuilder
    {
        static readonly Color Ink=new Color(0.86f, 0.9f, 0.93f);
        static readonly Color Dim=new Color(0.6f, 0.66f, 0.7f);
        static readonly Color Amber=new Color(0.95f, 0.72f, 0.3f);
        static readonly Color PanelTint=new Color(0.009f, 0.014f, 0.019f, 0.99f);
        static readonly Color ButtonTint=new Color(0.11f, 0.15f, 0.19f, 0.95f);
        static readonly Color TrackTint=new Color(1f, 1f, 1f, 0.12f);

        static Sprite _round, _circle, _ring, _vignette, _gradient, _scan, _soft;

        public static void Build(BuildRefs refs)
        {
            _round=TexKit.SpriteOf("ui_round");
            _circle = TexKit.SpriteOf("ui_circle");
            _ring = TexKit.SpriteOf("ui_ring");
            _vignette = TexKit.SpriteOf("ui_vignette");
            _gradient=TexKit.SpriteOf("ui_grad_h");
            _scan = TexKit.SpriteOf("ui_scan");
            _soft = TexKit.SpriteOf("ui_soft");

            var canvasGo = new GameObject("Interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=10;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920, 1080);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight=0.5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));

            var ui = refs.Ui;
            var flow = refs.Flow;
            var root = canvasGo.transform;

            
            ui.FadeImage = FullScreen(root, "Fade", null, Color.clear);
            ui.ComposureVignette = FullScreen(root, "FearVignette", _vignette, new Color(0.25f, 0f, 0f, 0f));

            ui.HudPanel = BuildHud(root, ui).gameObject;
            ui.TitlePanel = BuildTitle(root, flow).gameObject;
            ui.SettingsPanel = BuildSettings(root, flow).gameObject;
            ui.HowToPanel = BuildHowTo(root, flow).gameObject;
            ui.PausePanel = BuildPause(root, flow).gameObject;
            ui.EndingPanel = BuildEnding(root, flow, ui).gameObject;

            
            ui.FlashImage = FullScreen(root, "RewindFlash", null, new Color(0.85f, 0.95f, 1f, 0f));
            var scan=FullScreen(root, "RewindScanlines",_scan,new Color(0.6f,0.85f,1f,1f));
            scan.type = Image.Type.Tiled;
            scan.rectTransform.offsetMax = new Vector2(0,64);   
            ui.ScanlineImage = scan;
            ui.ScanlineImage.gameObject.SetActive(false);

            ui.FpsText = Label(root, "FpsCounter", "", 18, Dim, TextAlignmentOptions.TopRight);
            Anchor(ui.FpsText.rectTransform, new Vector2(1, 1), new Vector2(-16, -10), new Vector2(160, 30));
            ui.FpsText.gameObject.SetActive(false);

            foreach (var panel in new[] { ui.PausePanel, ui.HowToPanel, ui.SettingsPanel, ui.EndingPanel, ui.HudPanel })
                panel.SetActive(false);
        }

static RectTransform BuildHud(Transform root, UIManager ui)
        {
            var hud = Stretch(root, "HUD");

ImageAt(hud, "TaskBacking",_gradient,new Color(1,1,1,0.55f),new Vector2(0,1),new Vector2(0,-24),new Vector2(980,120));
            ui.ObjectiveLabel = Label(hud, "TaskLabel", "CURRENT TASK", 22, Amber, TextAlignmentOptions.TopLeft, FontStyles.Bold, 4f);
            Anchor(ui.ObjectiveLabel.rectTransform, new Vector2(0, 1), new Vector2(48, -38), new Vector2(700, 30));
            ui.ObjectiveText = Label(hud, "Task", "",32,Ink,TextAlignmentOptions.TopLeft);
            Anchor(ui.ObjectiveText.rectTransform,new Vector2(0,1),new Vector2(48,-70),new Vector2(860,84));
            ui.ObjectiveText.textWrappingMode = TextWrappingModes.Normal;
            ui.HintText = Label(hud, "Hint", "", 22, Dim, TextAlignmentOptions.TopLeft);
            Anchor(ui.HintText.rectTransform, new Vector2(0, 1), new Vector2(48, -158), new Vector2(1000, 64));
            ui.HintText.textWrappingMode = TextWrappingModes.Normal;
            ui.HintGroup = ui.HintText.gameObject.AddComponent<CanvasGroup>();

            
            ui.ClockText = Label(hud, "Clock", "02:47 AM",34,Ink,TextAlignmentOptions.TopRight,FontStyles.Bold);
            Anchor(ui.ClockText.rectTransform,new Vector2(1,1),new Vector2(-48,-36),new Vector2(320,44));
            ui.TimelineText = Label(hud, "Timeline", "TIMELINE 00", 22, Amber, TextAlignmentOptions.TopRight, FontStyles.Bold, 4f);
            Anchor(ui.TimelineText.rectTransform, new Vector2(1, 1), new Vector2(-48, -84), new Vector2(320, 30));

            
            ui.Crosshair = ImageAt(hud, "Crosshair", _circle, new Color(1, 1, 1, 0.35f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            ui.HoldRing=ImageAt(hud, "HoldRing",_ring,Amber,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(54,54));
            ui.HoldRing.type = Image.Type.Filled;
            ui.HoldRing.fillMethod = Image.FillMethod.Radial360;
            ui.HoldRing.fillOrigin = (int)Image.Origin360.Top;
            ui.HoldRing.gameObject.SetActive(false);
            var prompt = Panel(hud, "Prompt", new Vector2(0.5f, 0.5f), new Vector2(0, -112), new Vector2(560, 46), new Color(0, 0, 0, 0.45f));
            ui.PromptText = Label(prompt, "PromptText", "", 22, Ink, TextAlignmentOptions.Center);
            Fill(ui.PromptText.rectTransform);
            ui.PromptGroup=prompt.gameObject;
            ui.PromptGroup.SetActive(false);

            
            var threats=hud.gameObject.AddComponent<DirectionalThreatUI>();
            threats.Chevron=TexKit.SpriteOf("ui_chevron");

            
            ui.AwarenessIcon = ImageAt(hud, "Awareness", _soft, Color.clear, new Vector2(0.5f, 0.5f), new Vector2(0, 102), new Vector2(26, 26));

            
            var toast = Stretch(hud, "Toast");
            toast.anchorMin = new Vector2(0.5f, 0.76f); toast.anchorMax = new Vector2(0.5f, 0.76f);
            toast.sizeDelta = new Vector2(1000, 150);
            ui.ToastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            ui.ToastGroup.alpha = 0f;
            ui.ToastTitle = Label(toast, "ToastTitle", "",54,Ink,TextAlignmentOptions.Center,FontStyles.Bold,8f);
            Anchor(ui.ToastTitle.rectTransform,new Vector2(0.5f,0.5f),new Vector2(0,22),new Vector2(1000,70));
            ui.ToastBody = Label(toast, "ToastBody", "", 22, Dim, TextAlignmentOptions.Center, FontStyles.Normal, 6f);
            Anchor(ui.ToastBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -32), new Vector2(1000, 34));

            
            var subtitle = Panel(hud, "Subtitle", new Vector2(0.5f, 0f), new Vector2(0, 140), new Vector2(1300, 112), new Color(0, 0, 0, 0.55f));
            ui.SubtitleText=Label(subtitle, "SubtitleText", "", 26, Ink, TextAlignmentOptions.Center);
            Fill(ui.SubtitleText.rectTransform, 18);
            ui.SubtitleText.textWrappingMode = TextWrappingModes.Normal;
            ui.SubtitleGroup = subtitle.gameObject;
            ui.SubtitleGroup.SetActive(false);

            
            ui.StaminaGroup = Meter(hud, "Stamina", "STAMINA", new Vector2(48, 92), out ui.StaminaFill);
            ui.BatteryGroup=Meter(hud, "Battery", "FLASHLIGHT", new Vector2(48, 52), out ui.BatteryFill);

            
            var noiseLabel = Label(hud, "NoiseLabel", "NOISE", 18, Dim, TextAlignmentOptions.BottomRight, FontStyles.Bold, 4f);
            Anchor(noiseLabel.rectTransform, new Vector2(1, 0), new Vector2(-150, 46), new Vector2(120, 24));
            ui.NoiseBars=new Image[5];
            for (int i=0; i < 5; i++)
            {
                var bar=ImageAt(hud, "NoiseBar" + i,null,TrackTint,new Vector2(1,0),new Vector2(-122 + i * 16,48),new Vector2(9,10 + i * 7));
                bar.rectTransform.pivot = new Vector2(0.5f,0f);
                ui.NoiseBars[i] = bar;
            }

            
            var marker = new GameObject("ObjectiveMarker", typeof(RectTransform)).GetComponent<RectTransform>();
            marker.SetParent(hud, false);
            marker.sizeDelta = new Vector2(90, 60);
            var markerUi = marker.gameObject.AddComponent<ObjectiveMarkerUI>();
            markerUi.Marker = marker;
            markerUi.Icon = ImageAt(marker, "Diamond", _round, Amber, new Vector2(0.5f, 0.5f), new Vector2(0, 12), new Vector2(14, 14));
            markerUi.DistanceText = Label(marker, "Distance", "",17,Ink,TextAlignmentOptions.Center);
            Anchor(markerUi.DistanceText.rectTransform,new Vector2(0.5f,0.5f),new Vector2(0,-14),new Vector2(90,24));
            return hud;
        }

        static CanvasGroup Meter(Transform parent,string name,string caption,Vector2 bottomLeft,out Image fill)
        {
            var group = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(parent,false);
            Anchor(group,new Vector2(0,0),bottomLeft,new Vector2(360,30));
            group.pivot = new Vector2(0,0);
            var label = Label(group, "Label", caption, 18, Dim, TextAlignmentOptions.Left, FontStyles.Bold, 4f);
            Anchor(label.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(130, 26));
            label.rectTransform.pivot = new Vector2(0, 0.5f);
            var track = ImageAt(group, "Track", _round, TrackTint, new Vector2(0, 0.5f), new Vector2(136, 0), new Vector2(200, 8));
            track.rectTransform.pivot = new Vector2(0, 0.5f);
            track.type = Image.Type.Sliced;
            fill = ImageAt(track.rectTransform, "Fill", null, Ink, new Vector2(0, 0.5f), Vector2.zero, new Vector2(200, 8));
            fill.rectTransform.pivot=new Vector2(0, 0.5f);
            fill.type=Image.Type.Filled;
            fill.fillMethod=Image.FillMethod.Horizontal;
            var cg=group.gameObject.AddComponent<CanvasGroup>();
            cg.alpha=0f;
            return cg;
        }

static RectTransform BuildTitle(Transform root, GameFlowUI flow)
        {
            var panel=Stretch(root, "Title");
            var shade = FullScreen(panel, "Shade", _gradient, new Color(1,1,1,.99f));
            shade.rectTransform.anchorMax = new Vector2(.7f,1f);
            FullScreen(panel,"EdgeShade",_vignette,new Color(.3f,.36f,.42f,.35f));

            var eyebrow=Label(panel,"AccessLabel","CHRONOCORP  /  EMPLOYEE ACCESS",18,Amber,TextAlignmentOptions.Left,FontStyles.Normal,4f);
            Anchor(eyebrow.rectTransform,new Vector2(0,.5f),new Vector2(144,414),new Vector2(700,28));
            ImageAt(panel,"GoldRule",null,Amber,new Vector2(0,.5f),new Vector2(144,378),new Vector2(54,2));
            var title=Label(panel,"Title","THE NIGHT\nSHIFT",90,Ink,TextAlignmentOptions.Left,FontStyles.Normal,9f);
            Anchor(title.rectTransform,new Vector2(0,.5f),new Vector2(140,222),new Vector2(780,210));
            title.lineSpacing=-10f;
            var tagline=Label(panel,"Tagline","You get to go back. It doesn't.",25,Dim,TextAlignmentOptions.Left);
            Anchor(tagline.rectTransform,new Vector2(0,.5f),new Vector2(144,96),new Vector2(730,44));

            var location=Label(panel,"Location","FLOOR 13  /  ACCOUNTS\n02:47 AM",18,Ink,TextAlignmentOptions.TopRight,FontStyles.Normal,3f);
            Anchor(location.rectTransform,new Vector2(1,1),new Vector2(-80,-62),new Vector2(390,76));
            var assignment=Label(panel,"Assignment","OVERNIGHT ASSIGNMENT\n10 tasks. One way out.",18,Dim,TextAlignmentOptions.BottomRight);
            Anchor(assignment.rectTransform,new Vector2(1,0),new Vector2(-80,62),new Vector2(420,62));

            void TitleAction(string text,UnityAction action,float y,bool primary=false)
            {
                var edge=ImageAt(panel,text+"Button",null,new Color(.55f,.64f,.69f,.24f),new Vector2(0,.5f),new Vector2(144,y),new Vector2(442,60));
                var face=ImageAt(edge.rectTransform,"Face",null,primary?new Color(.72f,.52f,.25f,.96f):new Color(.018f,.026f,.032f,.88f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(440,58));
                face.raycastTarget=true;
                var caption=Label(face.rectTransform,"Label",text,23,primary?new Color(.055f,.06f,.06f):Ink,TextAlignmentOptions.Left);
                Anchor(caption.rectTransform,new Vector2(0,.5f),new Vector2(26,0),new Vector2(340,46));
                var arrow=Label(face.rectTransform,"Arrow",">",25,primary?new Color(.055f,.06f,.06f):Amber,TextAlignmentOptions.Right);
                Anchor(arrow.rectTransform,new Vector2(1,.5f),new Vector2(-24,0),new Vector2(28,40));
                var button=face.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic=face;
                SetButtonColors(button);
                UnityEventTools.AddPersistentListener(button.onClick,action);
            }
            TitleAction("Start shift",flow.StartShift,-12f,true);
            TitleAction("Settings",flow.OpenSettings,-86f);
            TitleAction("How to play",flow.OpenHowTo,-160f);
            TitleAction("Quit",flow.QuitGame,-234f);

            var hint=Label(panel,"Footer","HEADPHONES RECOMMENDED",16,Dim,TextAlignmentOptions.BottomLeft,FontStyles.Normal,2f);
            Anchor(hint.rectTransform,new Vector2(0,0),new Vector2(144,88),new Vector2(500,26));
            var enter=Label(panel,"EnterHint","ENTER  /  START SHIFT",16,Dim,TextAlignmentOptions.BottomLeft,FontStyles.Normal,2f);
            Anchor(enter.rectTransform,new Vector2(0,0),new Vector2(144,58),new Vector2(500,26));
            return panel;
        }

        static RectTransform BuildPause(Transform root, GameFlowUI flow)
        {
            var panel=Stretch(root, "Pause");
            FullScreen(panel, "Shade",null,PanelTint);
            Heading(panel, "SHIFT PAUSED", 220);
            float y = 70;
            foreach (var (label, action) in new (string, UnityAction)[] { ("Resume", flow.ResumeShift), ("Settings",flow.OpenSettings),("Return to title",flow.QuitToTitle),("Quit game", flow.QuitGame) })
            {
                MenuButton(panel, label, new Vector2(0.5f, 0.5f), new Vector2(0, y), action, false);
                y -= 76;
            }
            return panel;
        }

        static RectTransform BuildHowTo(Transform root, GameFlowUI flow)
        {
            var panel = Stretch(root, "HowToPlay");
            FullScreen(panel, "Shade",null,PanelTint);
            Heading(panel, "HOW TO SURVIVE THE NIGHT SHIFT",330);
            string[,] rows =
            {
                { "WASD / Mouse", "Move and look" },
                { "Shift", "Sprint. Limited stamina, and loud." },
                { "Ctrl or C", "Crouch. Quiet, and harder to see." },
                { "E (hold)", "Use, open doors, do your tasks" },
                { "F", "Flashlight. It sees you sooner when it's on." },
                { "Esc", "Pause" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = 210 - i * 58;
                var key = Label(panel, "Key" + i, rows[i, 0], 26, Amber, TextAlignmentOptions.Right, FontStyles.Bold);
                Anchor(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-220, y), new Vector2(360, 44));
                var what = Label(panel, "What" + i, rows[i, 1], 26, Ink, TextAlignmentOptions.Left);
                Anchor(what.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(240, y), new Vector2(520, 44));
            }
            var lore=Label(panel, "Lore", "If it kills you, time rewinds a few seconds. You remember what happened.\nSo does it. Every death makes it better at finding you. Close doors behind you.",
                22,Dim,TextAlignmentOptions.Center);
            Anchor(lore.rectTransform,new Vector2(0.5f,0.5f),new Vector2(0,-190),new Vector2(1300,90));
            lore.textWrappingMode = TextWrappingModes.Normal;
            MenuButton(panel, "Back", new Vector2(0.5f, 0.5f), new Vector2(0, -330), flow.Back, false);
            return panel;
        }

        static RectTransform BuildEnding(Transform root, GameFlowUI flow, UIManager ui)
        {
            var panel = Stretch(root,"Ending");
            FullScreen(panel,"Shade",null,new Color(.009f,.013f,.018f,1f));
            FullScreen(panel,"QuietGlow",_gradient,new Color(.3f,.38f,.42f,.32f));
            var report=Label(panel,"ReportLabel","CHRONOCORP  /  SHIFT REPORT",17,Amber,TextAlignmentOptions.TopLeft,FontStyles.Normal,4f);
            Anchor(report.rectTransform,new Vector2(0,1),new Vector2(96,-58),new Vector2(720,28));
            var floor=Label(panel,"FloorLabel","FLOOR 13  /  ACCOUNTS",16,Dim,TextAlignmentOptions.TopRight,FontStyles.Normal,3f);
            Anchor(floor.rectTransform,new Vector2(1,1),new Vector2(-96,-58),new Vector2(500,28));
            ImageAt(panel,"TopRule",null,new Color(.55f,.64f,.69f,.2f),new Vector2(.5f,1),new Vector2(0,-105),new Vector2(1728,1));

            var clockLabel=Label(panel,"ClockLabel","YOUR SHIFT IS OVER",17,Dim,TextAlignmentOptions.Left,FontStyles.Normal,4f);
            Anchor(clockLabel.rectTransform,new Vector2(0,.5f),new Vector2(132,262),new Vector2(440,28));
            var clock=Label(panel,"Clock","06:00",136,Ink,TextAlignmentOptions.Left);
            Anchor(clock.rectTransform,new Vector2(0,.5f),new Vector2(124,140),new Vector2(510,174));
            var am=Label(panel,"Am","AM",26,Amber,TextAlignmentOptions.Left,FontStyles.Normal,8f);
            Anchor(am.rectTransform,new Vector2(0,.5f),new Vector2(136,28),new Vector2(160,40));
            var gameTitle=Label(panel,"GameTitle","THE NIGHT SHIFT",18,Dim,TextAlignmentOptions.Left,FontStyles.Normal,4f);
            Anchor(gameTitle.rectTransform,new Vector2(0,.5f),new Vector2(136,-236),new Vector2(440,30));
            ImageAt(panel,"Divider",null,new Color(.55f,.64f,.69f,.18f),new Vector2(.5f,.5f),new Vector2(-278,10),new Vector2(1,486));

            var outcome=Label(panel,"OutcomeLabel","EMPLOYEE OUTCOME",17,Dim,TextAlignmentOptions.Left,FontStyles.Normal,4f);
            Anchor(outcome.rectTransform,new Vector2(.5f,.5f),new Vector2(224,242),new Vector2(760,28));
            ui.EndingTitleText=Label(panel,"EndingTitle","SHIFT COMPLETE",52,Ink,TextAlignmentOptions.Left,FontStyles.Normal,3f);
            Anchor(ui.EndingTitleText.rectTransform,new Vector2(.5f,.5f),new Vector2(259,160),new Vector2(830,78));
            ui.EndingTimelineText=Label(panel,"Summary","",25,Ink,TextAlignmentOptions.TopLeft);
            Anchor(ui.EndingTimelineText.rectTransform,new Vector2(.5f,.5f),new Vector2(224,24),new Vector2(760,164));
            ui.EndingTimelineText.textWrappingMode=TextWrappingModes.Normal;
            ui.EndingTimelineText.lineSpacing=6f;
            ui.EndingTimelineText.overflowMode=TextOverflowModes.Overflow;
            var quote=Label(panel,"Voicemail","\"Same shift tomorrow. Don't be late.\"",28,Amber,TextAlignmentOptions.Left,FontStyles.Italic);
            Anchor(quote.rectTransform,new Vector2(.5f,.5f),new Vector2(254,-126),new Vector2(820,54));
            MenuButton(panel,"Return to title",new Vector2(.5f,.5f),new Vector2(24,-280),flow.QuitToTitle,false);
            MenuButton(panel,"Quit game",new Vector2(.5f,.5f),new Vector2(450,-280),flow.QuitGame,false);
            ImageAt(panel,"BottomRule",null,new Color(.55f,.64f,.69f,.2f),new Vector2(.5f,0),new Vector2(0,96),new Vector2(1728,1));
            var footer=Label(panel,"Footer","02:47 - 06:00  /  PERSONNEL RECORD ARCHIVED",15,Dim,TextAlignmentOptions.BottomLeft,FontStyles.Normal,3f);
            Anchor(footer.rectTransform,new Vector2(0,0),new Vector2(96,52),new Vector2(1000,26));
            return panel;
        }

        static RectTransform BuildSettings(Transform root,GameFlowUI flow)
        {
            var panel=Stretch(root, "Settings");
            FullScreen(panel, "Shade", null, PanelTint);
            Heading(panel, "SETTINGS", 430);

            var columns=new (string title, SettingWidget.Kind[] kinds)[]
            {
                ("AUDIO & CONTROLS",new[]
                {
                    SettingWidget.Kind.Master,SettingWidget.Kind.Ambience,SettingWidget.Kind.Sfx,SettingWidget.Kind.Voice,
                    SettingWidget.Kind.Sensitivity,SettingWidget.Kind.InvertY,SettingWidget.Kind.HoldCrouch,
                    SettingWidget.Kind.Fov,SettingWidget.Kind.HeadBob,SettingWidget.Kind.Subtitles,
                }),
                ("GRAPHICS & DISPLAY",new[]
                {
                    SettingWidget.Kind.Quality,SettingWidget.Kind.RenderScale,SettingWidget.Kind.Brightness,SettingWidget.Kind.Fullscreen,
                    SettingWidget.Kind.VSync,SettingWidget.Kind.FpsCap,SettingWidget.Kind.FilmGrain,SettingWidget.Kind.ScreenShake,
                    SettingWidget.Kind.ShowFps,
                }),
            };
            for (int c = 0; c < columns.Length; c++)
            {
                float x = c == 0 ? -420 : 420;
                var header = Label(panel, "Column" + c, columns[c].title, 20, Amber, TextAlignmentOptions.Left, FontStyles.Bold, 4f);
                Anchor(header.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 345), new Vector2(720, 30));
                for (int r = 0; r < columns[c].kinds.Length; r++)
                    SettingRow(panel, columns[c].kinds[r], new Vector2(x, 290 - r * 58));
            }
            MenuButton(panel, "Reset to defaults", new Vector2(0.5f, 0.5f), new Vector2(-200, -390), flow.ResetSettings, false);
            MenuButton(panel, "Back",new Vector2(0.5f,0.5f),new Vector2(200,-390),flow.Back,false);
            return panel;
        }

        static void SettingRow(Transform parent,SettingWidget.Kind kind,Vector2 position)
        {
            var row=new GameObject(kind.ToString(),typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent,false);
            Anchor(row,new Vector2(0.5f,0.5f),position,new Vector2(720,50));
            var bg=row.gameObject.AddComponent<Image>();
            bg.sprite=_round; bg.type=Image.Type.Sliced; bg.color=new Color(1,1,1,0.04f); bg.raycastTarget=false;

            var label=Label(row, "Label",NiceName(kind),22,Ink,TextAlignmentOptions.Left);
            Anchor(label.rectTransform,new Vector2(0,0.5f),new Vector2(20,0),new Vector2(300,44));
            label.rectTransform.pivot = new Vector2(0,0.5f);

            var widget = row.gameObject.AddComponent<SettingWidget>();
            widget.Setting = kind;
            widget.ValueLabel = Label(row, "Value", "", 20, Amber, TextAlignmentOptions.Right, FontStyles.Bold);
            Anchor(widget.ValueLabel.rectTransform, new Vector2(1, 0.5f), new Vector2(-18, 0), new Vector2(110, 44));
            widget.ValueLabel.rectTransform.pivot=new Vector2(1, 0.5f);

            switch (KindOf(kind))
            {
                case Control.Slider: widget.Slider=MakeSlider(row, kind); break;
                case Control.Choice:
                    widget.Prev=SmallButton(row, "Previous", "<",new Vector2(340,0));
                    widget.Next = SmallButton(row, "Next", ">", new Vector2(470, 0));
                    break;
                default: widget.Toggle=MakeToggle(row); break;
            }
        }

        enum Control { Slider, Toggle, Choice }

        static Control KindOf(SettingWidget.Kind kind)
        {
            switch (kind)
            {
                case SettingWidget.Kind.Master: case SettingWidget.Kind.Ambience: case SettingWidget.Kind.Sfx: case SettingWidget.Kind.Voice:
                case SettingWidget.Kind.Sensitivity: case SettingWidget.Kind.Fov: case SettingWidget.Kind.RenderScale: case SettingWidget.Kind.Brightness:
                    return Control.Slider;
                case SettingWidget.Kind.Quality: case SettingWidget.Kind.FpsCap:
                    return Control.Choice;
                default:
                    return Control.Toggle;
            }
        }

        static string NiceName(SettingWidget.Kind kind)
        {
            switch (kind)
            {
                case SettingWidget.Kind.Master: return "Master volume";
                case SettingWidget.Kind.Ambience: return "Ambience volume";
                case SettingWidget.Kind.Sfx: return "Effects volume";
                case SettingWidget.Kind.Voice: return "Voice volume";
                case SettingWidget.Kind.Sensitivity: return "Mouse sensitivity";
                case SettingWidget.Kind.InvertY: return "Invert mouse Y";
                case SettingWidget.Kind.HoldCrouch: return "Hold to crouch";
                case SettingWidget.Kind.Fov: return "Field of view";
                case SettingWidget.Kind.HeadBob: return "Head bob";
                case SettingWidget.Kind.Subtitles: return "Subtitles";
                case SettingWidget.Kind.Quality: return "Graphics quality";
                case SettingWidget.Kind.RenderScale: return "Render scale";
                case SettingWidget.Kind.Brightness: return "Brightness";
                case SettingWidget.Kind.Fullscreen: return "Fullscreen";
                case SettingWidget.Kind.VSync: return "V-Sync";
                case SettingWidget.Kind.FpsCap: return "Frame-rate limit";
                case SettingWidget.Kind.FilmGrain: return "Film grain";
                case SettingWidget.Kind.ScreenShake: return "Screen shake";
                case SettingWidget.Kind.ShowFps: return "Show FPS";
            }
            return kind.ToString();
        }

        static Slider MakeSlider(RectTransform row, SettingWidget.Kind kind)
        {
            var track = ImageAt(row, "Slider", _round, TrackTint, new Vector2(0, 0.5f), new Vector2(330, 0), new Vector2(250, 10));
            track.rectTransform.pivot=new Vector2(0, 0.5f);
            track.type=Image.Type.Sliced;
            track.raycastTarget=true;
            var fillArea=new GameObject("FillArea",typeof(RectTransform)).GetComponent<RectTransform>();
            fillArea.SetParent(track.rectTransform,false);
            Fill(fillArea);
            var fill=ImageAt(fillArea, "Fill",_round,Amber,new Vector2(0,0.5f),Vector2.zero,Vector2.zero);
            fill.type = Image.Type.Sliced;
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0,1);
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = new GameObject("HandleArea", typeof(RectTransform)).GetComponent<RectTransform>();
            handleArea.SetParent(track.rectTransform, false);
            Fill(handleArea);
            var handle = ImageAt(handleArea, "Handle", _circle, Ink, new Vector2(0, 0.5f), Vector2.zero, new Vector2(24, 24));
            handle.raycastTarget=true;

            var slider=track.gameObject.AddComponent<Slider>();
            slider.fillRect=fill.rectTransform;
            slider.handleRect=handle.rectTransform;
            slider.targetGraphic=handle;
            slider.direction=Slider.Direction.LeftToRight;
            switch (kind)
            {
                case SettingWidget.Kind.Sensitivity: slider.minValue=0.2f; slider.maxValue=3f; break;
                case SettingWidget.Kind.Fov: slider.minValue=60f; slider.maxValue=95f; break;
                case SettingWidget.Kind.RenderScale: slider.minValue=0.5f; slider.maxValue=1f; break;
                case SettingWidget.Kind.Brightness: slider.minValue=-1f; slider.maxValue=1f; break;
                default: slider.minValue=0f; slider.maxValue=1f; break;
            }
            return slider;
        }

        static Toggle MakeToggle(RectTransform row)
        {
            var box=ImageAt(row, "Toggle",_round,TrackTint,new Vector2(0,0.5f),new Vector2(330,0),new Vector2(34,34));
            box.rectTransform.pivot=new Vector2(0,0.5f);
            box.type=Image.Type.Sliced;
            box.raycastTarget=true;
            var check=ImageAt(box.rectTransform, "Check",_round,Amber,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(22,22));
            check.type = Image.Type.Sliced;
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            return toggle;
        }

        static Button SmallButton(RectTransform row,string name,string glyph,Vector2 left)
        {
            var img = ImageAt(row,name,_round,ButtonTint,new Vector2(0,0.5f),left,new Vector2(56,38));
            img.rectTransform.pivot = new Vector2(0,0.5f);
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            var label = Label(img.rectTransform, "Glyph", glyph, 24, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            Fill(label.rectTransform);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            SetButtonColors(button);
            return button;
        }

        static void MenuButton(Transform parent, string text, Vector2 anchor, Vector2 position, UnityAction action, bool leftAligned)
        {
            var img = ImageAt(parent, text, _round, ButtonTint, anchor, position, new Vector2(400, 60));
            img.type = Image.Type.Sliced;
            img.raycastTarget = true;
            if (leftAligned) img.rectTransform.pivot = new Vector2(0, 0.5f);
            var label = Label(img.rectTransform, "Label", text, 26, Ink, leftAligned ? TextAlignmentOptions.Left : TextAlignmentOptions.Center);
            Fill(label.rectTransform, leftAligned ? 28 : 0);
            var button=img.gameObject.AddComponent<Button>();
            button.targetGraphic=img;
            SetButtonColors(button);
            UnityEventTools.AddPersistentListener(button.onClick, action);
        }

        static void SetButtonColors(Button button)
        {
            var colors=button.colors;
            colors.normalColor=Color.white;
            colors.highlightedColor=new Color(1.14f,1.09f,.98f);
            colors.pressedColor=new Color(0.8f, 0.8f, 0.8f);
            colors.selectedColor=new Color(1.1f,1.06f,.98f);
            colors.fadeDuration=0.08f;
            button.colors=colors;
        }

        static TMP_Text Heading(Transform parent, string text, float y)
        {
            var t=Label(parent, "Heading",text,56,Ink,TextAlignmentOptions.Center,FontStyles.Bold,6f);
            Anchor(t.rectTransform,new Vector2(0.5f,0.5f),new Vector2(0,y),new Vector2(1400,80));
            return t;
        }

static RectTransform Stretch(Transform parent,string name)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent,false);
            Fill(rt);
            return rt;
        }

        static void Fill(RectTransform rt,float padding=0f)
        {
            rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one;
            rt.offsetMin=new Vector2(padding,0); rt.offsetMax=new Vector2(-padding,0);
        }

        static void Anchor(RectTransform rt,Vector2 anchor,Vector2 position,Vector2 size)
        {
            rt.anchorMin=anchor; rt.anchorMax=anchor; rt.pivot=anchor;
            rt.anchoredPosition=position; rt.sizeDelta=size;
        }

        static Image FullScreen(Transform parent,string name,Sprite sprite,Color color)
        {
            var img=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent,false);
            Fill(img.rectTransform);
            img.sprite=sprite; img.color=color; img.raycastTarget=false;
            return img;
        }

        static Image ImageAt(Transform parent,string name,Sprite sprite,Color color,Vector2 anchor,Vector2 position,Vector2 size)
        {
            var img=new GameObject(name,typeof(RectTransform),typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent,false);
            Anchor(img.rectTransform,anchor,position,size);
            img.sprite=sprite; img.color=color; img.raycastTarget=false;
            return img;
        }

        static RectTransform Panel(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var img=ImageAt(parent,name,_round,color,anchor,position,size);
            img.type=Image.Type.Sliced;
            return img.rectTransform;
        }

        static TMP_Text Label(Transform parent,string name,string text,float size,Color color,TextAlignmentOptions align,
            FontStyles style=FontStyles.Normal,float spacing=0f)
        {
            var t=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent,false);
            t.text=text; t.fontSize=size; t.color=color; t.alignment=align; t.fontStyle=style;
            t.characterSpacing=spacing; t.raycastTarget=false;
            t.textWrappingMode=TextWrappingModes.NoWrap;
            t.overflowMode=TextOverflowModes.Ellipsis;
            return t;
        }
    }
}
#endif
