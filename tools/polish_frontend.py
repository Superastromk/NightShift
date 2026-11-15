from pathlib import Path

root=Path(__file__).resolve().parents[1]
path=root/'Assets/Editor/NightShift/UiBuilder.cs'
s=path.read_text(encoding='utf-8-sig')
title='''static RectTransform BuildTitle(Transform root, GameFlowUI flow)
        {
            var panel=Stretch(root, "Title");
            var shade = FullScreen(panel, "Shade", _gradient, new Color(1,1,1,.99f));
            shade.rectTransform.anchorMax = new Vector2(.7f,1f);
            FullScreen(panel,"EdgeShade",_vignette,new Color(.3f,.36f,.42f,.35f));

            var eyebrow=Label(panel,"AccessLabel","CHRONOCORP  /  EMPLOYEE ACCESS",18,Amber,TextAlignmentOptions.Left,FontStyles.Normal,4f);
            Anchor(eyebrow.rectTransform,new Vector2(0,.5f),new Vector2(144,414),new Vector2(700,28));
            ImageAt(panel,"GoldRule",null,Amber,new Vector2(0,.5f),new Vector2(144,378),new Vector2(54,2));
            var title=Label(panel,"Title","THE NIGHT\\nSHIFT",90,Ink,TextAlignmentOptions.Left,FontStyles.Normal,9f);
            Anchor(title.rectTransform,new Vector2(0,.5f),new Vector2(140,222),new Vector2(780,210));
            title.lineSpacing=-10f;
            var tagline=Label(panel,"Tagline","You get to go back. It doesn't.",25,Dim,TextAlignmentOptions.Left);
            Anchor(tagline.rectTransform,new Vector2(0,.5f),new Vector2(144,96),new Vector2(730,44));

            var location=Label(panel,"Location","FLOOR 13  /  ACCOUNTS\\n02:47 AM",18,Ink,TextAlignmentOptions.TopRight,FontStyles.Normal,3f);
            Anchor(location.rectTransform,new Vector2(1,1),new Vector2(-80,-62),new Vector2(390,76));
            var assignment=Label(panel,"Assignment","OVERNIGHT ASSIGNMENT\\n10 tasks. One way out.",18,Dim,TextAlignmentOptions.BottomRight);
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

'''
a=s.index('static RectTransform BuildTitle(')
b=s.index('        static RectTransform BuildPause(',a)
s=s[:a]+title+s[b:]
ending='''static RectTransform BuildEnding(Transform root, GameFlowUI flow, UIManager ui)
        {
            var panel = Stretch(root,"Ending");
            FullScreen(panel,"Shade",null,new Color(.009f,.013f,.018f,.99f));
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
            Anchor(outcome.rectTransform,new Vector2(.5f,.5f),new Vector2(-156,242),new Vector2(760,28));
            ui.EndingTitleText=Label(panel,"EndingTitle","SHIFT COMPLETE",52,Ink,TextAlignmentOptions.Left,FontStyles.Normal,3f);
            Anchor(ui.EndingTitleText.rectTransform,new Vector2(.5f,.5f),new Vector2(-156,160),new Vector2(830,78));
            ui.EndingTimelineText=Label(panel,"Summary","",25,Ink,TextAlignmentOptions.TopLeft);
            Anchor(ui.EndingTimelineText.rectTransform,new Vector2(.5f,.5f),new Vector2(-156,24),new Vector2(760,164));
            ui.EndingTimelineText.textWrappingMode=TextWrappingModes.Normal;
            ui.EndingTimelineText.lineSpacing=6f;
            ui.EndingTimelineText.overflowMode=TextOverflowModes.Overflow;
            var quote=Label(panel,"Voicemail","\\\"Same shift tomorrow. Don't be late.\\\"",28,Amber,TextAlignmentOptions.Left,FontStyles.Italic);
            Anchor(quote.rectTransform,new Vector2(.5f,.5f),new Vector2(-156,-126),new Vector2(820,54));
            MenuButton(panel,"Return to title",new Vector2(.5f,.5f),new Vector2(24,-280),flow.QuitToTitle,false);
            MenuButton(panel,"Quit game",new Vector2(.5f,.5f),new Vector2(450,-280),flow.QuitGame,false);
            ImageAt(panel,"BottomRule",null,new Color(.55f,.64f,.69f,.2f),new Vector2(.5f,0),new Vector2(0,96),new Vector2(1728,1));
            var footer=Label(panel,"Footer","02:47 - 06:00  /  PERSONNEL RECORD ARCHIVED",15,Dim,TextAlignmentOptions.BottomLeft,FontStyles.Normal,3f);
            Anchor(footer.rectTransform,new Vector2(0,0),new Vector2(96,52),new Vector2(1000,26));
            return panel;
        }

'''
a=s.index('static RectTransform BuildEnding(')
b=s.index('        static RectTransform BuildSettings(',a)
s=s[:a]+ending+s[b:]
s=s.replace('colors.highlightedColor=new Color(1.5f, 1.35f, 1.1f);','colors.highlightedColor=new Color(1.14f,1.09f,.98f);')
s=s.replace('colors.selectedColor=new Color(1.3f, 1.2f, 1f);','colors.selectedColor=new Color(1.1f,1.06f,.98f);')
path.write_text(s,encoding='utf-8')

path=root/'Assets/Scripts/Progression/ShiftDirector.cs'
s=path.read_text(encoding='utf-8-sig')
a=s.index('            string verdict = deaths == 0')
b=s.index('            gm?.SetState(GameState.Ending);',a)
s=s[:a]+'''            string verdict = deaths == 0 ? "You made it through without dying."
                : deaths == 1 ? "You died once. The night remembers."
                : $"You died {deaths} times. It remembers every one.";
            string body =$"TIMELINE {timelines:00}\\n\\n" + verdict;
''' +s[b:]
path.write_text(s,encoding='utf-8')

path=root/'Assets/Scripts/Monster/HumanQaView.cs'
s=path.read_text(encoding='utf-8-sig')
s=s.replace('float distance=_close?0.9f:2.35f;','float distance=_close?0.9f:2.65f;')
path.write_text(s,encoding='utf-8')
print('Updated title, ending, summary, and restored the earlier inspection camera.')
