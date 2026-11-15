#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using NightShift.Rewind;
using NightShift.World;

namespace NightShift.EditorBuild
{
    public static partial class OfficeBuilder
    {
        
        public const float Row1Z = 0.6f,Row2Z = -6.4f,CubW = 2.4f,CubD = 2.2f;
        public static readonly float[] GroupX = { -13f,0f,13f };
        public static readonly Vector3 PlayerDeskPos = new Vector3(0f,0f,Row2Z - 0.42f); 
        public static readonly Vector3 CopierPos = new Vector3(18.3f,0f,4.5f);
        static readonly Color SignWhite = new Color(0.92f,0.94f,0.95f),SignDim = new Color(0.62f,0.66f,0.7f);
        static readonly List<Transform> DoorHinges = new List<Transform>();
        static Transform _elevDoorL0,_elevDoorR0,_elevDoorL1,_elevDoorR1;
        static Light _cabLight0;
        static int _screensLit;

        static void Dress()
        {
            DoorHinges.Clear(); _screensLit = 0;
            CeilingLights();
            CubicleFarm();
            SouthZone();
            EdgeAisles();
            CopyNook();
            Corridor();
            ManagerOffice();
            Meeting1();
            Meeting2();
            BreakRoom();
            Storage();
            Lobby();
            for (int i = 0; i < DoorXs.Length; i++) MakeDoor(DoorXs[i],i == 3 || i == 1);
        }

static void CeilingLights()
        {
            
            float[] xs = { -15f,-9f,-3f,3f,9f,15f };
            float[] aisleZ = { 3.9f,-2.9f,-11.2f };
            int[][] aisle =
            {
                new[] { 1,1,0,2,1,1 },   
                new[] { 1,0,1,1,0,1 },   
                new[] { 0,2,1,1,1,0 },   
            };
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 6; c++)
                    Fixture(V(xs[c],H,aisleZ[r]),aisle[r][c] > 0, "floor", aisle[r][c] == 2);
            
            int[][] block = { new[] { 1, 1, 0 }, new[] { 2, 1, 1 } };
            float[] rowZ = { Row1Z, Row2Z };
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                    Fixture(V(GroupX[c], H, rowZ[r] + (r == 0 ? 1.2f : -1.2f)), block[r][c] > 0, "floor", block[r][c] == 2, 6.5f, 7.5f);
            
            float[] cx = { -17f, -11f, -5f, 1f, 7f, 13f, 18.5f };
            int[] cs = { 2, 1, 1, 0, 1, 1, 1 };
            for (int i = 0; i < cx.Length; i++) Fixture(V(cx[i], H, 6.5f), cs[i] > 0, "corridor", cs[i] == 2, 7f, 8f);
            foreach (var p in new[] { V(-12f, H, -4.6f), V(4.5f, H, -9f), V(12f, H, 2.4f), V(-2f, H, 6.5f), V(10f, H, 6.5f) })
                Put(p, 0f, g => Props.CeilingVent(g));
        }

static void CubicleFarm()
        {
            int idx=0;
            foreach (float rowZ in new[] { Row1Z, Row2Z })
                foreach (float gx in GroupX)
                {
                    float len=CubW * 3f;
                    Put(V(gx, 0, rowZ), 0f, g => Props.Partition(g, len));
                    Col(V(gx, 0.7f, rowZ), V(len, 1.4f, 0.07f));
                    for (int k=0; k <= 3; k++)
                    {
                        float x=gx - len / 2 + k * CubW;
                        foreach (float s in new[] { -1f, 1f })
                        {
                            Put(V(x, 0, rowZ + s * (CubD / 2 + 0.03f)), 90f, g => Props.Partition(g, CubD));
                            Col(V(x, 0.7f, rowZ + s * (CubD / 2 + 0.03f)), V(0.07f, 1.4f, CubD));
                        }
                    }
                    for (int k=0; k < 3; k++)
                        foreach (bool north in new[] { true, false })
                        {
                            float cxm=gx - len / 2 + (k + 0.5f) * CubW;
                            bool mine=Mathf.Abs(cxm - PlayerDeskPos.x) < 0.1f && !north && rowZ == Row2Z;
                            Cubicle(V(cxm, 0, rowZ), north, idx++, mine);
                        }
                }
        }

        static void Cubicle(Vector3 spine, bool north, int idx, bool mine)
        {
            float yaw=north ? 0f : 180f;
            Vector3 deskPos=spine + V(0, 0, north ? 0.42f : -0.42f);
            Quaternion rot=Quaternion.Euler(0, yaw, 0);
            bool empty=!mine && idx % 7 == 5;        
            bool on=mine || (!empty && idx % 4 == 1 && _screensLit < 9);
            string screen=mine ? "screenLogin" : on ? (idx % 3 == 0 ? "screenError" : "screenSheet") : "screenOff";
            float mx=-0.12f + (idx % 3 - 1) * 0.08f;
            bool lamp=!empty && (idx % 9 == 2);
            Vector3 lampLocal=Vector3.zero;

            Put(deskPos, yaw, g =>
            {
                Props.Desk(g, 1.6f, 0.75f, true);
                float y=Props.DeskH;
                if (empty) { using (g.At(V(0.2f, y, 0.05f), 20f)) Props.CardboardBox(g, 0.36f, 0.24f, 0.28f); return; }

bool phone=mine || idx % 5 == 0;
                bool secondScreen=!phone && idx % 6 == 3;
                bool papers=idx % 3 != 1;
                bool mug=mine || idx % 2 == 0;
                using (g.At(V(mx, y, -0.16f), (idx % 5 - 2) * 3f)) Props.Monitor(g, screen);
                if (secondScreen) using (g.At(V(0.5f, y, -0.15f), -14f)) Props.Monitor(g, "screenOff",0.46f);
                using (g.At(V(mx,y,0.16f),(idx % 3 - 1) * 4f)) Props.Keyboard(g);
                using (g.At(V(mx + 0.34f,y,0.18f),8f)) Props.Mouse(g);
                if (!mine && phone) using (g.At(V(0.6f,y,-0.2f),-20f)) Props.Phone(g);
                if (lamp) using (g.At(V(-0.62f,y,-0.25f),30f)) lampLocal = V(-0.62f,y,-0.25f) + Quaternion.Euler(0,30f,0) * Props.DeskLamp(g);
                Vector3 paperSpot = lamp ? V(-0.6f,y,0.15f) : V(-0.52f,y,-0.12f);
                if (papers) using (g.At(paperSpot,6f + idx * 5f)) Props.PaperStack(g,0.012f + (idx % 4) * 0.012f);
                Vector3 mugSpot = lamp && papers ? V(0.62f,y,0.24f) : V(-0.62f,y,0.22f);
                if (mug) using (g.At(mugSpot,40f + idx * 17f)) Props.Mug(g,idx % 3 == 0 ? "ceramicRed" : idx % 3 == 1 ? "ceramicWhite" : "ceramicBlue", idx % 4 != 0);
                
                float pz=-0.388f;
                if (idx % 2 == 1) g.Quad(idx % 4 == 1 ? "noteYellow" : "notePink", V(mx + 0.45f, 1.08f, pz), V(0.038f, 0, 0), V(0, 0.038f, 0));
                if (idx % 3 == 0) g.Quad("noteYellow", V(mx + 0.36f, 1.17f, pz), V(0.038f, 0, 0), V(0, 0.038f, 0));
                if (idx % 4 == 2) g.Quad("paperLines", V(mx - 0.5f, 1.12f, pz), V(0.105f, 0, 0), V(0, 0.148f, 0));
            });
            ColLocal(deskPos, yaw, V(0, Props.DeskH / 2, 0), V(1.6f, Props.DeskH, 0.75f));

            if (on)
            {
                _screensLit++;
                Vector3 p=deskPos + rot * V(mx, 1.1f, 0.25f);
                var c=screen == "screenLogin" ? new Color(0.35f,0.6f,1f) : screen == "screenError" ? new Color(0.25f, 0.4f, 1f) : new Color(0.75f, 0.9f, 0.85f);
                Glow("Monitor", p, c, mine ? 1.6f : 1.1f, 2.6f, "accent", true, !mine);
            }
            if (lamp) Glow("DeskLamp",deskPos + rot * lampLocal,new Color(1f,0.74f,0.45f),2.6f,3.2f, "accent", false, false, LightType.Spot, Vector3.down + rot * V(0, 0, 0.3f), 110f);

            if (!empty)
            {
                Vector3 chairPos = deskPos + rot * V(mx + (idx % 3 - 1) * 0.1f, 0, 0.78f + (idx % 4) * 0.06f);
                if (!mine) Chair(chairPos, yaw + 180f + ((idx * 37) % 70 - 35));
                else Chair(deskPos + rot * V(-0.85f, 0, 1.0f), yaw + 230f);     
                if (idx % 3 == 2) Bin(deskPos + rot * V(-1.0f, 0, 0.35f));
            }
            if (mine)
            {
                Put(deskPos, yaw, g => { using (g.At(V(0.55f, Props.DeskH, -0.18f), -18f)) Props.Phone(g); });
                Label("EMPLOYEE OF THE MONTH", deskPos + rot * V(0f, 1.3f, -0.388f), rot * Vector3.forward, 0.42f, new Color(0.15f, 0.15f, 0.18f), 0.8f, 0.1f);
                Label("(position vacant)", deskPos + rot * V(0f, 1.24f, -0.388f), rot * Vector3.forward, 0.32f, new Color(0.25f, 0.25f, 0.3f), 0.8f, 0.1f, FontStyles.Italic);
                Put(deskPos, yaw, g => g.Quad("paper",V(0f,1.27f,-0.389f),V(0.32f,0,0),V(0,0.075f,0)));
            }
        }

static void SouthZone()
        {
            
            for (float x = -17.5f; x <= 17.6f; x += 5f)
            {
                for (int i = -1; i <= 1; i++)
                {
                    if ((int)(x + 40) % 2 == 0 && i == 1) continue;
                    Vector3 p = V(x + i * 0.5f,0,ZMin + WallT / 2 + 0.34f);
                    Put(p,0f,g => Props.FilingCabinet(g,2));
                    Col(p + V(0,0.35f,0),V(0.47f,0.7f,0.62f));
                }
                Put(V(x + 0.9f,0.7f,ZMin + 0.45f),15f,g => Props.PaperStack(g,0.03f));
            }
            int seed = 5;
            foreach (float x in new[] { -15f,-5f,5f,15f })
            {
                Vector3 p = V(x,0,ZMin + 0.55f);
                int sd = seed++;
                Put(p,0f,g => Props.Plant(g,0.19f,1.25f,13,sd));
                Col(p + V(0,0.2f,0),V(0.4f,0.4f,0.4f));
            }
            
            foreach (float x in new[] { -10f,10f })
            {
                Vector3 p = V(x,0,-11.3f);
                Put(p,0f,g => Props.RoundTable(g,0.55f));
                Col(p + V(0,0.37f,0),V(0.5f,0.74f,0.5f));
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 120f + 20f;
                    SideChair(p + Quaternion.Euler(0,a,0) * V(0,0,0.95f),a + 180f + R(-15,15));
                }
                Put(p + V(0.1f,0.74f,0.05f),30f,g => Props.Mug(g, "ceramicWhite", false));
            }
            Dyn("WetFloorSign", () => { var g = new Geo(); Props.WetFloorSign(g); return g; }, V(3.2f, 0, -10.2f), 25f,
                new[] { new BoxDef(V(0, 0.3f, 0), V(0.3f, 0.6f, 0.34f)) }, 1.2f, "clatter", false, Pm(false));
            Label("CAUTION",V(3.2f,0.42f,-10.2f) + Quaternion.Euler(0,25f,0) * V(0,0,0.11f),Quaternion.Euler(-14f,25f,0) * Vector3.forward,0.5f,Color.black,0.3f,0.1f).transform.SetParent(DynamicProps[DynamicProps.Count - 1].transform,true);
            Bin(V(-11.2f,0,-12.4f)); Bin(V(8.6f,0,-12.6f));
            
            Put(Columns[0] + V(0,0,0.45f),0f,g => Props.WaterCooler(g));
            Col(Columns[0] + V(0,0.7f,0.45f),V(0.32f,1.4f,0.32f));
            Put(Columns[1] + V(0,0,0.5f),0f,g => Props.Plant(g,0.18f,1.3f,12,21));
            Col(Columns[1] + V(0,0.2f,0.5f),V(0.38f,0.4f,0.38f));
            Put(Columns[2] + V(0,0,-0.52f),180f,g => Props.FilingCabinet(g,3));
            Col(Columns[2] + V(0,0.5f,-0.6f),V(0.47f,1.0f,0.62f));
            Put(Columns[3] + V(0,0,-0.5f),0f,g => Props.Plant(g,0.18f,1.2f,11,33));
            Col(Columns[3] + V(0,0.2f,-0.5f),V(0.38f,0.4f,0.38f));
            Bin(Columns[0] + V(0.5f,0,-0.1f));
            foreach (var cpos in Columns) { Put(cpos + V(0.2775f,1.5f,0),90f,g => Props.Poster(g, "posterCream", 0.3f, 0.42f)); }
            Label("FIRE\nASSEMBLY\nPOINT:\nELEVATORS", Columns[0] + V(0.2945f, 1.5f, 0), Vector3.right, 0.42f, new Color(0.5f, 0.1f, 0.08f), 0.28f, 0.4f);
            Label("PLEASE\nDO NOT\nFEED\nTHE NEW\nEMPLOYEE", Columns[2] + V(0.2945f, 1.5f, 0), Vector3.right, 0.4f, new Color(0.12f, 0.12f, 0.15f), 0.28f, 0.4f);
            Label("QUIET\nZONE",Columns[1] + V(0.2945f,1.5f,0),Vector3.right,0.7f,new Color(0.1f,0.25f,0.4f),0.28f,0.4f);
            Label("YOU ARE\nHERE", Columns[3] + V(0.2945f, 1.5f, 0), Vector3.right, 0.6f, new Color(0.5f, 0.1f, 0.08f), 0.28f, 0.4f);
        }

        static void EdgeAisles()
        {
            
            foreach (float z in new[] { -6.5f, -0.2f })
            {
                Vector3 pw = V(XMin + WallT / 2 + 0.18f, 0, z);
                int sd = (int)(z * 10) + 80;
                Put(pw, 90f, g => Props.Bookcase(g, 0.9f, 1.9f, 0.32f, sd));
                Col(pw + V(0, 0.95f, 0), V(0.32f, 1.9f, 0.9f));
                Vector3 pe = V(XMax - WallT / 2 - 0.33f, 0, z);
                Put(pe, -90f, g => Props.FilingCabinet(g, 4));
                Col(pe + V(0, 0.66f, 0), V(0.62f, 1.32f, 0.47f));
                Put(pe + V(0, 0, 0.5f), -90f, g => Props.FilingCabinet(g, 4));
                Col(pe + V(0, 0.66f, 0.5f), V(0.62f, 1.32f, 0.47f));
            }
            
            Vector3 pp = PlantPos;
            Put(pp, 0f, g => Props.Plant(g, 0.24f, 1.55f, 16, 77));
            Col(pp + V(0, 0.22f, 0), V(0.5f, 0.44f, 0.5f));
            Put(pp + V(0.55f, 0, 0.2f), 0f, g => { g.Lathe("plasticGrey", Vector3.zero, new[] { new Vector2(0f, 0f), new Vector2(0.09f, 0f), new Vector2(0.1f, 0.2f), new Vector2(0.08f, 0.22f) }, 10); g.Tube("plasticGrey", V(0.09f, 0.12f, 0), V(0.24f, 0.24f, 0), 0.012f, 0.008f, 6); });
            Label("WATER ME\n(please)",pp + V(0.0f,0.3f,0.26f),Vector3.forward,0.35f,new Color(0.9f,0.9f,0.85f),0.3f,0.12f);
        }

        public static readonly Vector3 PlantPos = new Vector3(-18.9f,0f,-12.6f);

        static GameObject _copier,_facePage;

        static void CopyNook()
        {
            
            var g = new Geo();
            Props.Copier(g);
            _copier = g.Emit("Copier", null, false);
            _copier.layer = LNoNav;
            _copier.transform.SetPositionAndRotation(CopierPos, Quaternion.Euler(0, 180f, 0));
            var bc = _copier.AddComponent<BoxCollider>();
            bc.center = V(0, 0.47f, 0);
            bc.size = V(0.74f, 0.94f, 0.66f);
            var fg = new Geo();
            using (fg.At(V(-0.47f, 0.6f, 0.02f), Quaternion.Euler(0, 0, 10f))) fg.Quad("paperFace", V(0, 0.002f, 0), V(0.105f, 0, 0), V(0, 0, -0.148f));
            _facePage = fg.Emit("FacePage", _copier.transform, false, false);
            
            Vector3 sp=V(16.6f, 0, CorrZ0 - WallT / 2 - 0.25f);
            Put(sp, 180f, s => { Props.MetalShelf(s, 1.4f, 1.8f, 0.42f, 4); for (int i=0; i < 3; i++) for (int j=0; j < 3; j++) using (s.At(V(-0.42f + j * 0.42f, 0.133f + i * 0.533f, 0), j * 7f - 6f)) if ((i + j) % 4 != 3) Props.CardboardBox(s, 0.32f, 0.2f + (i % 2) * 0.06f, 0.3f); });
            Col(sp + V(0, 0.9f, 0), V(1.4f, 1.8f, 0.42f));
            Bin(V(19.3f, 0, 3.4f));
            Label("OUT OF ORDER",V(18.3f,1.45f,CorrZ0 - WallT / 2),Vector3.back,0.55f,new Color(0.55f,0.1f,0.08f),0.7f,0.12f);
            Label("(it isn't)", V(18.3f, 1.35f, CorrZ0 - WallT / 2), Vector3.back, 0.32f, new Color(0.2f, 0.2f, 0.24f), 0.7f, 0.1f, FontStyles.Italic);
            Put(V(18.3f, 1.42f, CorrZ0 - WallT / 2), 180f, s => s.Quad("paper", V(0, 0, 0.002f), V(0.3f, 0, 0), V(0, 0.12f, 0)));
        }

static void Plate(string text, float x, float z, Vector3 normal)
        {
            float yaw = normal.z < 0 ? 180f : 0f;
            Put(V(x, 1.6f, z), yaw, g => g.Box("plasticBlack", V(0, 0, 0.006f), V(0.62f, 0.13f, 0.012f), 0.003f));
            Label(text, V(x, 1.6f, z) + normal * 0.013f, normal, 0.46f, SignWhite, 0.6f, 0.12f, FontStyles.Bold, TextAlignmentOptions.Center, 6f);
        }

        static void ExitSign(Vector3 pos, Vector3 normal, string text)
        {
            float yaw=Mathf.Atan2(normal.x, normal.z) * Mathf.Rad2Deg;
            Put(pos, yaw, g => Props.ExitSignBody(g));
            Label(text, pos + normal * 0.027f, normal, 0.95f, new Color(0.45f, 1f, 0.65f), 0.4f, 0.16f);
            Label(text, pos - normal * 0.027f, -normal, 0.95f, new Color(0.45f, 1f, 0.65f), 0.4f, 0.16f);
            Glow("Exit",pos + V(0,-0.15f,0),new Color(0.3f,1f,0.55f),0.35f,1.6f, "accent", true, false);
        }

        static void Corridor()
        {
            float wn = CorrZ1 - WallT / 2, ws = CorrZ0 + WallT / 2;
            string[] names = { "MANAGER", "MEETING 1", "MEETING 2", "BREAK ROOM", "STORAGE" };
            for (int i = 0; i < DoorXs.Length; i++) Plate(names[i], DoorXs[i] + 0.95f, wn, Vector3.back);
            ExitSign(V(10.6f, H - 0.3f, 6.5f), Vector3.left, "EXIT >");
            ExitSign(V(-1.9f, H - 0.3f, 6.5f), Vector3.left, "EXIT >");
            ExitSign(V(0f,H - 0.3f,3.2f),Vector3.back, "EXIT");
            
            Put(V(-8.5f, 1.55f, ws), 0f, g => Props.Poster(g, "posterBlue", 0.7f, 0.95f));
            Label("SYNERGY", V(-8.5f, 1.72f, ws + 0.017f), Vector3.forward, 1.0f, SignWhite, 0.66f, 0.2f, FontStyles.Bold, TextAlignmentOptions.Center, 10f);
            Label("is not optional.",V(-8.5f,1.5f,ws + 0.017f),Vector3.forward,0.4f,SignDim,0.66f,0.12f,FontStyles.Italic);
            Label("CHRONOCORP", V(-8.5f, 1.18f, ws + 0.017f), Vector3.forward, 0.3f, SignDim, 0.66f, 0.1f, FontStyles.Bold, TextAlignmentOptions.Center, 14f);
            Put(V(5.2f, 1.55f, ws), 0f, g => Props.Poster(g, "posterCream", 0.9f, 0.95f));
            Label("TEMPORAL RECOVERY SYSTEM", V(5.2f, 1.86f, ws + 0.017f), Vector3.forward, 0.42f, new Color(0.1f, 0.12f, 0.2f), 0.86f, 0.12f);
            Label("In the event of death:\n1. Remain calm.\n2. You will be returned to a point\n    shortly before the incident.\n3. Please do not die the same way twice.\n    It learns.",V(5.2f,1.52f,ws + 0.017f),Vector3.forward,0.3f,new Color(0.14f,0.14f,0.18f),0.8f,0.5f,FontStyles.Normal,TextAlignmentOptions.TopLeft);
            Label("Your deaths are company property.", V(5.2f, 1.14f, ws + 0.017f), Vector3.forward, 0.24f, new Color(0.45f, 0.1f, 0.08f), 0.8f, 0.08f, FontStyles.Italic);
            Put(V(-17.3f, 1.55f, ws), 0f, g => Props.Poster(g, "posterRed", 0.7f, 0.5f));
            Label("DAYS SINCE LAST INCIDENT", V(-17.3f, 1.68f, ws + 0.017f), Vector3.forward, 0.3f, SignWhite, 0.66f, 0.1f);
            Label("0",V(-17.3f,1.5f,ws + 0.017f),Vector3.forward,1.5f,SignWhite,0.66f,0.28f);
            
            Put(V(8.5f,0.72f,wn - 0.075f),180f,g => { Props.FireExtinguisher(g); g.Box("metal", V(0, 0.2f, -0.07f), V(0.16f, 0.3f, 0.012f), 0.003f); g.Box("metal", V(0, -0.006f, -0.02f), V(0.16f, 0.012f, 0.11f), 0.003f); });
            
            MakeClock(V(3.9f, 2.25f, wn), Vector3.back);
            
            Put(V(8.8f, 0, ws + 0.3f), 0f, g => Props.Bench(g, 1.5f));
            Col(V(8.8f, 0.24f, ws + 0.3f), V(1.5f, 0.48f, 0.48f));
            Put(V(-19.4f, 0, 7.4f), 0f, g => Props.Plant(g, 0.18f, 1.35f, 12, 55));
            Col(V(-19.4f, 0.2f, 7.4f), V(0.38f, 0.4f, 0.38f));
            Bin(V(9.9f, 0, 5.5f));

            
            var sg = new Geo();
            for (int i = 0; i < 14; i++) sg.Box("elevatorMetal", V(0, 0.1f + i * 0.2f, 0), V(0.06f, 0.19f, CorrZ1 - CorrZ0 - WallT), 0.02f);
            sg.Box("extinguisher",V(0,0.05f,0),V(0.08f,0.1f,CorrZ1 - CorrZ0 - WallT),0.01f);
            var shutter = sg.Emit("Act3Shutter", null, false);
            shutter.transform.position = V(ShutterX, 0, (CorrZ0 + CorrZ1) / 2);
            var sbc = shutter.AddComponent<BoxCollider>();
            sbc.center = V(0, H / 2, 0);
            sbc.size = V(0.2f, H, CorrZ1 - CorrZ0);
            var obs = shutter.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obs.center = V(0, H / 2, 0);
            obs.size = V(0.5f, H, CorrZ1 - CorrZ0);
            obs.carving = true;
            shutter.layer = LNoNav;
            shutter.SetActive(false);
            _shutter = shutter;
        }

        public const float ShutterX = 4.4f;
        static GameObject _shutter;

        static void MakeClock(Vector3 pos, Vector3 normal)
        {
            var g = new Geo();
            Props.ClockFace(g);
            var go = g.Emit("WallClock", null, false, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal));
            var wc = go.AddComponent<WallClock>();
            Transform Hand(string n, float len, float w)
            {
                var hg = new Geo();
                hg.Box("plasticBlack", V(0, len / 2 - 0.015f, 0), V(w, len, 0.004f), 0f);
                var h=hg.Emit(n, go.transform, false, false);
                h.transform.localPosition=V(0, 0, n == "Hour" ? 0.041f : 0.045f);
                return h.transform;
            }
            wc.HourHand = Hand("Hour", 0.085f, 0.014f);
            wc.MinuteHand = Hand("Minute", 0.13f, 0.009f);
        }

static void ManagerOffice()
        {
            float x0 = RoomX[0], x1 = RoomX[1], cx = -17.2f;
            Vector3 desk = V(cx, 0, 11.9f);
            Vector3 lampLocal = Vector3.zero;
            Put(desk, 0f, g =>
            {
                Props.Desk(g, 2.0f, 0.9f, true, "woodDark");
                float y=Props.DeskH;
                using (g.At(V(0.45f, y, -0.2f), 12f)) Props.Monitor(g, "screenOff",0.6f);
                using (g.At(V(0.4f,y,0.15f))) Props.Keyboard(g);
                using (g.At(V(-0.75f,y,-0.25f),25f)) lampLocal = V(-0.75f,y,-0.25f) + Quaternion.Euler(0,25f,0) * Props.DeskLamp(g);
                using (g.At(V(-0.25f,y,-0.05f),4f)) { g.Box("plasticBlack", V(0, 0.03f, 0), V(0.26f, 0.06f, 0.34f), 0.006f); using (g.At(V(0, 0.06f, 0))) Props.PaperStack(g, 0.03f, "paperReport"); } 
                using (g.At(V(-0.1f, y, -0.36f)))
                { g.Box("woodDark", V(0, 0.03f, 0), V(0.34f, 0.06f, 0.05f), 0.006f); }                                                                         
                using (g.At(V(0.85f, y, 0.2f), 60f)) Props.Mug(g, "ceramicWhite");
            });
            ColLocal(desk,0f,V(0,Props.DeskH / 2,0),V(2.0f,Props.DeskH,0.9f));
            Label("REGIONAL MANAGER", desk + V(-0.1f, Props.DeskH + 0.03f, -0.386f), Vector3.back, 0.2f, new Color(0.9f, 0.8f, 0.5f), 0.32f, 0.05f);
            Glow("ManagerLamp", desk + lampLocal, new Color(1f, 0.72f, 0.42f), 6f, 5f, "meeting", false, false, LightType.Spot, Vector3.down + V(0.1f, 0, 0.35f), 125f);
            Glow("ManagerFill",V(-17.2f,2.2f,10.2f),new Color(1f,0.8f,0.6f),0.5f,5f, "meeting", false, true);
            Chair(desk + V(0.1f, 0, 0.85f), 205f, "fabricRed");
            SideChair(V(cx - 0.6f, 0, 10.7f), 10f); SideChair(V(cx + 0.65f, 0, 10.75f), -12f);
            foreach (float z in new[] { 9.6f, 10.6f })
            {
                Vector3 p = V(x0 + WallT / 2 + 0.18f, 0, z);
                int sd = (int)(z * 7);
                Put(p, 90f, g => Props.Bookcase(g, 0.95f, 2.0f, 0.32f, sd));
                Col(p + V(0, 1f, 0), V(0.32f, 2f, 0.95f));
            }
            Vector3 fc = V(x1 - WallT / 2 - 0.33f, 0, 13.3f);
            Put(fc, -90f, g => Props.FilingCabinet(g, 4));
            Col(fc + V(0, 0.66f, 0), V(0.62f, 1.32f, 0.47f));
            Put(V(x1 - 0.6f, 0, 8.7f), 0f, g => Props.Plant(g, 0.2f, 1.5f, 14, 91));
            Col(V(x1 - 0.6f, 0.2f, 8.7f), V(0.42f, 0.4f, 0.42f));
            Put(V(cx, 1.65f, ZMax - WallT / 2), 180f, g => Props.Poster(g, "posterCream", 1.1f, 0.75f));
            Label("EMPLOYEE RETENTION",V(cx,1.88f,ZMax - WallT / 2 - 0.017f),Vector3.back,0.42f,new Color(0.12f,0.12f,0.16f),1.0f,0.12f);
            Label("100%", V(cx, 1.62f, ZMax - WallT / 2 - 0.017f), Vector3.back, 1.6f, new Color(0.45f, 0.1f, 0.08f), 1.0f, 0.3f);
            Label("Nobody has ever left.", V(cx, 1.38f, ZMax - WallT / 2 - 0.017f), Vector3.back, 0.32f, new Color(0.2f, 0.2f, 0.25f), 1.0f, 0.1f, FontStyles.Italic);
            Fixture(V(cx, H, 10.4f), false, "meeting");
            Bin(V(cx + 1.3f, 0, 12.6f));
        }

        static void Meeting1()
        {
            float x0=RoomX[1], x1=RoomX[2], cx=(x0 + x1) / 2;
            Vector3 t=V(cx, 0, 11.4f);
            Put(t, 0f, g => { Props.RoundTable(g, 0.7f); using (g.At(V(0.2f, 0.74f, 0.1f), 30f)) Props.PaperStack(g, 0.01f); using (g.At(V(-0.25f, 0.74f, -0.2f))) Props.Mug(g, "ceramicBlue"); });
            Col(t + V(0,0.37f,0),V(0.6f,0.74f,0.6f));
            for (int i = 0; i < 4; i++) { float a = i * 90f + 45f; Chair(t + Quaternion.Euler(0,a,0) * V(0,0,1.15f),a + 180f + R(-25,25)); }
            Put(V(x0 + WallT / 2,1.5f,11.4f),90f,g => Props.Whiteboard(g,2.2f,1.15f));
            Put(V(x1 - WallT / 2,1.55f,11.4f),-90f,g => Props.WallScreen(g, "screenError", 1.3f));
            Glow("TvGlow", V(x1 - 1.1f, 1.4f, 11.4f), new Color(0.25f, 0.4f, 1f), 0.5f, 3.5f, "accent", true, true);
            Fixture(V(cx, H, 11f), true, "meeting",true,6.5f,7f);
            Bin(V(x1 - 0.5f,0,8.7f));
        }

        public static readonly Vector3 ReportSpot = new Vector3(-4.3f,0.76f,11.3f);   

        static void Meeting2()
        {
            float x0 = RoomX[2],x1 = RoomX[3],cx = -5.7f;
            Vector3 t = V(cx,0,11.3f);
            Put(t,0f,g =>
            {
                Props.ConferenceTable(g,3.4f,1.25f);
                using (g.At(V(-1.0f,0.75f,0.2f),14f)) Props.PaperStack(g,0.008f);
                using (g.At(V(0.9f,0.75f,-0.25f),-30f)) Props.Mug(g, "ceramicWhite", false);
                using (g.At(V(1.2f, 0.75f, 0.2f), 80f)) Props.Phone(g);
            });
            Col(t + V(0, 0.375f, 0), V(3.4f, 0.75f, 1.25f));
            for (int i = 0; i < 4; i++)
            {
                float x = cx - 1.2f + i * 0.8f;
                Chair(V(x + R(-0.08f, 0.08f), 0, 12.35f + R(0, 0.15f)), 180f + R(-20, 20));
                Chair(V(x + R(-0.08f, 0.08f), 0, 10.25f - R(0, 0.15f)), R(-20, 20));
            }
            Put(V(cx, 1.5f, ZMax - WallT / 2), 180f, g => Props.Whiteboard(g, 2.6f, 1.2f));
            Label("Q3 GOALS", V(cx - 0.75f, 1.98f, ZMax - WallT / 2 - 0.026f), Vector3.back, 0.42f, new Color(0.1f, 0.2f, 0.55f), 0.9f, 0.1f, FontStyles.Bold, TextAlignmentOptions.Left);
            Put(V(x1 - WallT / 2, 1.55f, 11.3f), -90f, g => Props.WallScreen(g, "screenSheet", 1.5f));
            Glow("TvGlow2",V(x1 - 1.1f,1.4f,11.3f),new Color(0.75f,0.9f,0.85f),0.5f,3.5f, "accent", true, true);
            Put(V(x0 + 0.5f, 0, 13.4f), 0f, g => Props.Plant(g, 0.2f, 1.45f, 14, 123));
            Col(V(x0 + 0.5f, 0.2f, 13.4f), V(0.42f, 0.4f, 0.42f));
            Fixture(V(cx - 1.2f, H, 11.3f), true, "meeting", false, 7f, 8f);
            Fixture(V(cx + 1.6f, H, 11.3f), false, "meeting");
        }

        public static readonly Vector3 CoffeePos=new Vector3(1.0f, 0.905f, 13.52f);
        public static readonly Vector3 CoffeeReveal=new Vector3(2.7f, 0f, 11.4f);

        static void BreakRoom()
        {
            float x0=RoomX[3], x1=RoomX[4];
            Vector3 counter=V(0.2f, 0, ZMax - WallT / 2 - 0.32f);
            Put(counter, 180f, g => Props.KitchenCounter(g, 3.2f));
            Col(counter + V(0, 0.45f, 0), V(3.2f, 0.9f, 0.64f));
            Col(counter + V(0, 1.75f, 0.14f), V(3.2f, 0.6f, 0.34f));
            Put(CoffeePos, 180f, g => { Props.CoffeeMachine(g); using (g.At(V(0, 0.024f, 0.09f))) Props.Mug(g, "ceramicWhite",false); });
            Put(V(-0.75f,0.905f,13.56f),180f,g => Props.Microwave(g));
            Put(V(0.1f,0.905f,13.45f),40f,g => Props.Mug(g, "ceramicRed"));
            Vector3 fr = V(x0 + WallT / 2 + 0.38f, 0, 13.52f);
            Put(fr, 180f, g => Props.Fridge(g)); Col(fr + V(0, 0.9f, 0), V(0.7f, 1.8f, 0.68f));
            Vector3 vm = V(x1 - WallT / 2 - 0.42f, 0, 11.9f);
            Put(vm, -90f, g => Props.VendingMachine(g));
            Col(vm + V(0, 0.93f, 0), V(0.8f, 1.86f, 0.95f));
            Glow("Vending", vm + V(-0.7f, 1.2f, 0), new Color(0.55f, 0.75f, 1f), 2.6f, 4.5f, "accent", true, false);
            Vector3 wc=V(2.5f, 0, 13.68f);
            Put(wc, 180f, g => Props.WaterCooler(g));
            Col(wc + V(0, 0.7f, 0), V(0.32f, 1.4f, 0.32f));
            Vector3 t=V(-0.3f, 0, 10.4f);
            Put(t, 0f, g => { Props.RoundTable(g, 0.55f); using (g.At(V(0.15f, 0.74f, 0f), 50f)) Props.Mug(g, "ceramicBlue"); using (g.At(V(-0.2f,0.74f,0.12f),12f)) Props.PaperStack(g,0.006f); });
            Col(t + V(0,0.37f,0),V(0.5f,0.74f,0.5f));
            for (int i = 0; i < 3; i++) { float a = i * 120f + 70f; SideChair(t + Quaternion.Euler(0,a,0) * V(0,0,0.95f),a + 180f + R(-20,20)); }
            Bin(V(x1 - 0.6f,0,13.4f));
            Put(V(x0 + WallT / 2,1.6f,10.6f),90f,g => Props.Poster(g, "posterBlue", 0.7f, 0.9f));
            Label("YOUR MUG\nIS NOT\nYOUR MUG", V(x0 + WallT / 2 + 0.017f, 1.68f, 10.6f), Vector3.right, 0.6f, SignWhite, 0.66f, 0.5f);
            Label("Label your food. Label yourself.", V(x0 + WallT / 2 + 0.017f, 1.25f, 10.6f), Vector3.right, 0.22f, SignDim, 0.66f, 0.08f, FontStyles.Italic);
            Fixture(V(0.75f, H, 10.2f), true, "break",false,7.5f,8f,new Color(1f,0.95f,0.85f));
            Fixture(V(0.75f,H,12.7f),true, "break", false, 6.5f, 7f, new Color(1f, 0.95f, 0.85f));
        }

        public static readonly Vector3 BreakerPos = new Vector3(7.55f, 1.4f, 13.9f);
        public static readonly Quaternion BreakerRotation = Quaternion.Euler(0f, 180f, 0f);

        static void Storage()
        {
            float x0 = RoomX[4], x1 = RoomX[5];
            
            Action<Vector3, float, int> shelf = (p, yaw, seed) =>
            {
                Put(p, yaw, g =>
                {
                    Props.MetalShelf(g, 1.8f, 1.95f, 0.45f, 4);
                    var r = new System.Random(seed);
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 3; j++)
                        {
                            if (r.NextDouble() < 0.25) continue;
                            float w = 0.34f + (float)r.NextDouble() * 0.14f, h = 0.18f + (float)r.NextDouble() * 0.2f;
                            using (g.At(V(-0.56f + j * 0.56f, 0.133f + i * 0.583f, 0), (float)r.NextDouble() * 12f - 6f)) Props.CardboardBox(g, w, h, 0.34f);
                        }
                });
                bool z = Mathf.Abs(Mathf.Repeat(yaw, 180f) - 90f) < 1f;
                Col(p + V(0, 0.98f, 0), z ? V(0.45f, 1.95f, 1.8f) : V(1.8f, 1.95f, 0.45f));
            };
            shelf(V(x0 + WallT / 2 + 0.25f, 0, 10.3f), 90f, 3);
            shelf(V(x0 + WallT / 2 + 0.25f, 0, 12.4f), 90f, 4);
            shelf(V(5.8f, 0, ZMax - WallT / 2 - 0.25f), 180f, 5);
            Vector3 rack = V(8.6f, 0, 13.3f);
            Put(rack, 180f, g => Props.ServerRack(g)); Col(rack + V(0, 1f, 0), V(0.62f, 2f, 0.9f));
            Glow("Server", rack + V(0, 1.2f, -0.8f), new Color(0.3f, 0.95f, 0.5f), 0.45f, 2.6f, "accent", true, false);
            Put(BreakerPos, 180f, g => Props.BreakerPanel(g));
            Label("MAIN BREAKER",BreakerPos + BreakerRotation * V(0,0.47f,0.126f),Vector3.back,0.3f,new Color(0.9f,0.85f,0.3f),0.5f,0.08f);
            
            Glow("BreakerWorkLight", BreakerPos + BreakerRotation * V(0, 0.9f, 0.7f), new Color(1f, 0.78f, 0.52f), 2.4f, 3.5f, "storage", true, true);
            
            
            Put(BreakerPos + BreakerRotation * V(0, 0.78f, 0.2f), 180f, g =>
            {
                g.Box("plasticBlack", V(0, 0, 0), V(0.26f, 0.08f, 0.13f), 0.012f);
                g.Box("fixtureOn",V(0,-0.047f,0.018f),V(0.22f,0.018f,0.1f),0.006f);
            });

            Box(V(6.6f,0,10.2f),20f,0.5f,0.4f,0.4f);
            Box(V(7.2f,0,10.9f),-30f,0.4f,0.3f,0.3f);
            Box(V(6.7f,0.4f,10.2f),50f,0.4f,0.3f,0.3f);
            Box(V(5.6f,0,9.5f),10f,0.5f,0.4f,0.4f);
            Box(V(5.2f,0,8.8f),75f,0.4f,0.3f,0.3f);
            Bin(V(8.9f,0,9.6f));
            Fixture(V((x0 + x1) / 2,H,11f),false, "storage");
            Fixture(V(8.2f, H, 10.4f), true, "storage", true, 8f, 7f, new Color(0.95f, 0.97f, 1f)); 
        }

        public static readonly Vector3 SpawnPos = new Vector3(18.5f, 0.05f, 12.3f);
        public static readonly Vector3 ExitPos = new Vector3(18.6f, 0f, 11.1f);
        public static readonly Vector3 ClockInPos = new Vector3(13.2f, 1.4f, ZMax - WallT / 2);
        public static readonly Vector3 ParcelPos = new Vector3(17.4f, 0f, 9.3f);
        public static readonly Vector3 InternPos = new Vector3(20.95f, 0.0f, 9.9f);

        static void Lobby()
        {
            float x0 = RoomX[5];
            for (int i = 0; i < 2; i++)
            {
                float z = ElevZ[i];
                
                Put(V(XMax, 0, z), 90f, g => Props.DoorFrame(g, 1.2f, 2.15f, WallT + 0.04f));
                var geo = Z(V(XMax - 0.5f, 0, z));
                
                Col(V(XMax + 0.9f, -0.08f, z), V(2f, 0.16f, 1.8f), 0f, LNoNav);
                geo.Box("linoleum", V(XMax + 0.95f, -0.02f, z), V(1.7f, 0.04f, 1.7f), 0f);
                geo.Box("elevatorMetal",V(XMax + 1.82f,1.2f,z),V(0.06f,2.4f,1.7f),0.004f);
                geo.Box("elevatorMetal", V(XMax + 0.95f, 1.2f, z - 0.87f), V(1.7f, 2.4f, 0.06f), 0.004f);
                geo.Box("elevatorMetal", V(XMax + 0.95f, 1.2f, z + 0.87f), V(1.7f, 2.4f, 0.06f), 0.004f);
                geo.Box("metal", V(XMax + 0.95f, 2.42f, z), V(1.7f, 0.04f, 1.7f), 0f);
                geo.Quad("fixtureOn",V(XMax + 0.95f,2.398f,z),V(0.45f,0,0),V(0,0,0.45f));
                geo.Tube("chrome", V(XMax + 1.76f, 0.95f, z - 0.7f), V(XMax + 1.76f, 0.95f, z + 0.7f), 0.02f, 0.02f, 8);
                geo.Box("wallDark", V(XMax + 0.16f, 1.2f, z - 0.74f), V(0.12f, 2.4f, 0.28f), 0f);     
                geo.Box("wallDark", V(XMax + 0.16f, 1.2f, z + 0.74f), V(0.12f, 2.4f, 0.28f), 0f);
                Col(V(XMax + 1.82f, 1.2f, z), V(0.1f, 2.4f, 1.8f), 0f, LNoNav);
                Col(V(XMax + 0.95f, 1.2f, z - 0.9f), V(1.8f, 2.4f, 0.1f), 0f, LNoNav);
                Col(V(XMax + 0.95f, 1.2f, z + 0.9f), V(1.8f, 2.4f, 0.1f), 0f, LNoNav);
                var cab=Glow("Cab" + i,V(XMax + 0.95f,2.1f,z),new Color(1f,0.96f,0.86f),3.2f,4f, "lobby", false, false);
                if (i == 0) _cabLight0 = cab.Lamp;
                
                Transform[] leaves = new Transform[2];
                for (int k = 0; k < 2; k++)
                {
                    var dg = new Geo();
                    dg.Box("elevatorMetal", V(0, 1.075f, 0), V(0.045f, 2.15f, 0.6f), 0.004f);
                    dg.Box("metal", V(k == 0 ? -0.0f : 0.0f, 1.075f, k == 0 ? 0.297f : -0.297f), V(0.05f, 2.15f, 0.008f), 0f);
                    var leaf=dg.Emit($"Elevator{i}_Door{(k == 0 ? "L" : "R")}", null, false);
                    leaf.layer = LNoNav;
                    leaf.transform.position = V(XMax + 0.02f, 0, z + (k == 0 ? -0.3f : 0.3f));
                    var lbc = leaf.AddComponent<BoxCollider>();
                    lbc.center = V(0, 1.075f, 0);
                    lbc.size = V(0.08f, 2.15f, 0.6f);
                    leaves[k] = leaf.transform;
                }
                if (i == 0)
                { _elevDoorL0 = leaves[0]; _elevDoorR0 = leaves[1]; } else { _elevDoorL1 = leaves[0]; _elevDoorR1 = leaves[1]; }
                
                Put(V(XMax - WallT / 2, 2.38f, z), -90f, g => g.Box("plasticBlack", V(0, 0, 0.012f), V(0.34f, 0.16f, 0.024f), 0.004f));
                Label("13",V(XMax - WallT / 2 - 0.026f,2.38f,z),Vector3.left,0.9f,new Color(1f,0.55f,0.15f),0.3f,0.14f);
            }
            
            float mz = (ElevZ[0] + ElevZ[1]) / 2;
            Put(V(XMax - WallT / 2,1.15f,mz),-90f,g => { g.Box("metalLight", V(0, 0, 0.008f), V(0.12f, 0.26f, 0.016f), 0.004f); g.Cyl("ledAmber", V(0, 0.05f, 0.016f), 0.02f, 0.004f, 10); g.Cyl("plasticBlack", V(0, -0.05f, 0.016f), 0.02f, 0.004f, 10); });
            Glow("CallButton",V(XMax - 0.35f,1.2f,mz),new Color(1f,0.6f,0.15f),0.8f,2f, "accent", true, true);

            
            Put(ClockInPos, 180f, g => Props.PunchClock(g));
            Label("CLOCK IN", ClockInPos + V(0, 0.3f, -0.004f), Vector3.back, 0.5f, SignWhite, 0.6f, 0.1f, FontStyles.Bold, TextAlignmentOptions.Center, 8f);
            
            float wx = x0 + WallT / 2;
            Label("13", V(wx, 1.55f, 11.2f), Vector3.right, 12f, new Color(0.85f, 0.88f, 0.9f), 3.2f, 1.9f);
            Label("CHRONOCORP  ·  ACCOUNTS",V(wx,0.62f,11.2f),Vector3.right,0.9f,SignDim,3.6f,0.2f,FontStyles.Bold,TextAlignmentOptions.Center,16f);
            Label("Putting the \"again\" in \"try again\".", V(wx, 0.42f, 11.2f), Vector3.right, 0.5f, SignDim, 3.6f, 0.14f, FontStyles.Italic);
            
            Vector3 bench = V(15.6f, 0, ZMax - WallT / 2 - 0.3f);
            Put(bench, 180f, g => Props.Bench(g, 1.8f));
            Col(bench + V(0, 0.24f, 0), V(1.8f, 0.48f, 0.48f));
            Put(V(x0 + 0.55f, 0, 13.4f), 0f, g => Props.Plant(g, 0.22f, 1.6f, 15, 201));
            Col(V(x0 + 0.55f, 0.2f, 13.4f), V(0.46f, 0.4f, 0.46f));
            Put(V(19.3f, 0, 13.45f), 0f, g => Props.Plant(g, 0.2f, 1.4f, 13, 202));
            Col(V(19.3f, 0.2f, 13.45f), V(0.42f, 0.4f, 0.42f));
            Put(V(19.35f, 0, 8.55f), 0f, g => Props.Plant(g, 0.2f, 1.3f, 12, 203));
            Col(V(19.35f, 0.2f, 8.55f), V(0.42f, 0.4f, 0.42f));
            Bin(V(14.3f, 0, 13.6f));
            
            Put(ParcelPos, 25f, g => Props.CardboardBox(g, 0.42f, 0.3f, 0.32f));
            Col(ParcelPos + V(0, 0.15f, 0), V(0.4f, 0.3f, 0.36f), 25f);
            Label("FRAGILE\nTHIS WAY UP", ParcelPos + V(0, 0.3013f, 0), Vector3.up, 0.3f, new Color(0.45f, 0.08f, 0.06f), 0.36f, 0.14f).transform.rotation = Quaternion.Euler(90f, 25f + 180f, 0);
            Fixture(V(13f, H, 11f), true, "lobby", false, 8f, 9f, new Color(1f, 0.96f, 0.88f), 90f);
            Fixture(V(17.2f, H, 11f), true, "lobby",false,7.5f,9f,new Color(1f,0.96f,0.88f),90f);
        }

static void MakeDoor(float x,bool startsOpen)
        {
            Vector3 c = V(x,0,CorrZ1);
            Put(c,0f,g => Props.DoorFrame(g,DoorW,DoorH,WallT));
            var root = new GameObject("Door_" + x.ToString("0.0"))
{ layer = LNoNav };
            root.transform.position = c;
            var hinge = new GameObject("Hinge") { layer=LNoNav };
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition=V(-DoorW / 2 + 0.008f, 0.012f, 0);
            var g2=new Geo();
            Props.DoorLeaf(g2, DoorW - 0.016f, DoorH - 0.02f);
            var leaf=g2.Emit("Leaf",hinge.transform,false);
            leaf.layer = LNoNav;
            var bc = leaf.AddComponent<BoxCollider>();
            bc.center = V((DoorW - 0.016f) / 2,DoorH / 2,0);
            bc.size = V(DoorW - 0.016f,DoorH - 0.02f,0.05f);
            var rb = hinge.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var rw = hinge.AddComponent<RewindableObject>();
            rw.Local = true;
            rw.RecordPosition = false;
            var door = root.AddComponent<Door>();
            door.Hinge = hinge.transform;
            door.DoorwayCenter = c + V(0,1f,0);
            door.StartsOpen = startsOpen;
            DoorHinges.Add(hinge.transform);
        }
    }
}
#endif
