#if UNITY_EDITOR
using UnityEngine;

namespace NightShift.EditorBuild
{
    public static class Props
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        public const float DeskH = 0.74f;

public static void Desk(Geo g, float w = 1.6f, float d = 0.75f, bool pedestal = true, string top = "laminate")
        {
            g.Box(top, V(0, DeskH - 0.015f, 0), V(w, 0.03f, d), 0.008f);
            g.Box("metalLight",V(0,DeskH - 0.036f,0),V(w - 0.04f,0.012f,d - 0.04f),0.002f);     
            g.Box("cabinet", V(-w / 2 + 0.02f, (DeskH - 0.04f) / 2, 0), V(0.03f, DeskH - 0.04f, d - 0.08f), 0.004f); 
            g.Box("cabinet", V(w / 2 - 0.02f, (DeskH - 0.04f) / 2, 0), V(0.03f, DeskH - 0.04f, d - 0.08f), 0.004f);
            g.Box("cabinet", V(0, DeskH - 0.26f, -d / 2 + 0.05f), V(w - 0.07f, 0.4f, 0.018f), 0.003f);    
            if (pedestal) Pedestal(g, V(w / 2 - 0.26f, 0, -0.03f));
        }

        static void Pedestal(Geo g, Vector3 at)
        {
            using (g.At(at))
            {
                g.Box("cabinet",V(0,0.30f,0),V(0.40f,0.56f,0.55f),0.008f);
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.11f + i * 0.18f;
                    g.Box("cabinet", V(0, y, 0.28f), V(0.37f, 0.16f, 0.012f), 0.004f);
                    g.Box("chrome", V(0, y + 0.04f, 0.292f), V(0.12f, 0.012f, 0.012f), 0.003f);
                }
                for (int i = 0; i < 4; i++) g.Cyl("rubber", V((i % 2 == 0 ? -1 : 1) * 0.16f, 0, (i < 2 ? -1 : 1) * 0.22f), 0.018f, 0.02f, 8);
            }
        }

        
        public static void Monitor(Geo g, string screen="screenSheet",float w = 0.54f)
        {
            float h = w * 0.58f;
            g.Cyl("plasticBlack", V(0, 0, -0.02f), 0.105f, 0.012f, 14);
            g.Tube("plasticBlack", V(0, 0.01f, -0.03f), V(0, 0.2f, -0.045f), 0.018f, 0.016f, 8);
            g.Box("plasticBlack", V(0, 0.13f + h / 2, -0.03f), V(w, h, 0.028f), 0.008f);
            g.Quad(screen, V(0, 0.13f + h / 2 + 0.005f, -0.015f), V(w / 2 - 0.014f, 0, 0), V(0, h / 2 - 0.022f, 0));
        }

        public static void Keyboard(Geo g)
        {
            g.Box("plasticBlack",V(0,0.011f,0),V(0.44f,0.02f,0.14f),0.005f);
            g.Quad("keyboard", V(0, 0.0215f, 0), V(0.21f, 0, 0), V(0, 0, -0.063f));
        }

        public static void Mouse(Geo g) { g.Ball("plasticBlack", V(0, 0.014f, 0), V(0.03f, 0.018f, 0.05f), 10, 6); }

        public static void Mug(Geo g, string mat = "ceramicWhite", bool coffee=true)
        {
            g.Lathe(mat, Vector3.zero, new[]
            {
                new Vector2(0.0f, 0.0f), new Vector2(0.036f, 0.0f), new Vector2(0.04f, 0.006f), new Vector2(0.041f, 0.092f),
                new Vector2(0.0385f, 0.095f), new Vector2(0.036f, 0.09f), new Vector2(0.035f, 0.012f), new Vector2(0f, 0.01f)
            }, 12);
            if (coffee) g.Cyl("coffee",V(0,0.07f,0),0.0355f,0.004f,12);
            g.Tube(mat,V(0.04f,0.075f,0),V(0.066f,0.066f,0),0.007f,0.007f,6);
            g.Tube(mat,V(0.066f,0.066f,0),V(0.066f,0.034f,0),0.007f,0.007f,6);
            g.Tube(mat,V(0.066f,0.034f,0),V(0.04f,0.024f,0),0.007f,0.007f,6);
        }

        
        public static void PaperStack(Geo g,float height = 0.02f,string topMat = "paperLines")
        {
            g.Box("paper", V(0, height / 2, 0), V(0.21f, height, 0.297f), 0.001f);
            g.Quad(topMat, V(0, height + 0.0008f, 0), V(0.105f, 0, 0), V(0, 0, -0.1485f));
        }

        public static void Phone(Geo g)
        {
            using (g.At(Vector3.zero, Quaternion.Euler(-12f, 0, 0)))
            {
                g.Box("plasticBlack", V(0, 0.03f, 0), V(0.2f, 0.045f, 0.22f), 0.008f);
                g.Quad("keyboard",V(0.035f,0.0535f,0.02f),V(0.055f,0,0),V(0,0,-0.065f));
                g.Quad("screenLogin", V(0.035f, 0.0535f, -0.07f), V(0.055f, 0, 0), V(0, 0, -0.02f));
                g.Box("plasticBlack", V(-0.065f, 0.068f, 0), V(0.05f, 0.03f, 0.2f), 0.012f);   
                g.Ball("plasticBlack", V(-0.065f, 0.062f, 0.08f), V(0.03f, 0.022f, 0.035f), 8, 5);
                g.Ball("plasticBlack",V(-0.065f,0.062f,-0.08f),V(0.03f,0.022f,0.035f),8,5);
            }
            g.Box("ledRed", V(0.085f, 0.03f, 0.105f), V(0.012f, 0.008f, 0.01f), 0.002f);
        }

        
        public static Vector3 DeskLamp(Geo g)
        {
            g.Cyl("metal", Vector3.zero, 0.075f, 0.015f, 14);
            g.Tube("metal", V(0, 0.015f, 0), V(0.0f, 0.26f, -0.08f), 0.008f, 0.008f, 6);
            g.Tube("metal",V(0.0f,0.26f,-0.08f),V(0.0f,0.38f,0.1f),0.008f,0.008f,6);
            using (g.At(V(0,0.38f,0.1f),Quaternion.Euler(35f,0,0)))
            {
                g.Lathe("metal", V(0, -0.09f, 0), new[] { new Vector2(0.085f, 0f), new Vector2(0.03f, 0.09f), new Vector2(0.012f, 0.1f) }, 12);
                g.Cyl("lampGlow", V(0, -0.088f, 0), 0.07f, 0.004f, 12);
            }
            return V(0, 0.30f, 0.16f);
        }

public static void OfficeChair(Geo g, string fabric = "fabricChair", bool arms=true)
        {
            for (int i=0; i < 5; i++)
            {
                float a=i / 5f * Mathf.PI * 2f;
                Vector3 tip=V(Mathf.Sin(a) * 0.29f, 0.055f, Mathf.Cos(a) * 0.29f);
                g.Tube("plasticBlack",V(0,0.09f,0),tip,0.022f,0.016f,6);
                g.Ball("rubber", tip + V(0, -0.028f, 0), V(0.028f, 0.028f, 0.028f), 8, 5);
            }
            g.Cyl("chrome", V(0, 0.08f, 0), 0.022f, 0.3f, 8);
            g.Cyl("plasticBlack", V(0, 0.36f, 0), 0.09f, 0.03f, 10);
            g.Box(fabric, V(0, 0.44f, 0.01f), V(0.48f, 0.085f, 0.46f), 0.03f);                       
            g.Tube("plasticBlack",V(0,0.40f,-0.2f),V(0,0.62f,-0.26f),0.022f,0.02f,6);       
            using (g.At(V(0,0.78f,-0.255f),Quaternion.Euler(-7f,0,0)))
                g.Box(fabric,Vector3.zero,V(0.44f,0.5f,0.07f),0.03f);                           
            if (arms)
                foreach (float sx in new[] { -1f,1f })
                {
                    g.Tube("plasticBlack", V(sx * 0.25f, 0.42f, -0.02f), V(sx * 0.27f, 0.62f, -0.02f), 0.014f, 0.014f, 6);
                    g.Box("plasticBlack", V(sx * 0.27f, 0.635f, 0.02f), V(0.055f, 0.03f, 0.24f), 0.012f);
                }
        }

        
        public static void SideChair(Geo g, string seat = "plasticGrey")
        {
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    g.Tube("chrome",V(sx * 0.19f,0,sz * 0.19f),V(sx * 0.18f,0.44f,sz * 0.17f),0.011f,0.011f,6);
            g.Box(seat,V(0,0.455f,0),V(0.42f,0.03f,0.42f),0.012f);
            g.Tube("chrome", V(-0.18f, 0.44f, -0.17f), V(-0.18f, 0.82f, -0.22f), 0.011f, 0.011f, 6);
            g.Tube("chrome", V(0.18f, 0.44f, -0.17f), V(0.18f, 0.82f, -0.22f), 0.011f, 0.011f, 6);
            using (g.At(V(0, 0.72f, -0.21f), Quaternion.Euler(-8f, 0, 0))) g.Box(seat, Vector3.zero, V(0.4f, 0.22f, 0.025f), 0.01f);
        }

        public static void Bench(Geo g, float w = 1.6f)
        {
            g.Box("fabricChair", V(0, 0.42f, 0), V(w, 0.1f, 0.48f), 0.03f);
            g.Box("metal",V(0,0.36f,0),V(w - 0.06f,0.03f,0.42f),0.004f);
            foreach (float sx in new[] { -1f,1f })
            {
                g.Box("metal", V(sx * (w / 2 - 0.12f), 0.18f, 0), V(0.04f, 0.36f, 0.04f), 0.004f);
                g.Box("metal", V(sx * (w / 2 - 0.12f), 0.015f, 0), V(0.06f, 0.03f, 0.42f), 0.004f);
            }
        }

public static void Partition(Geo g, float length, float height = 1.4f, float thick = 0.055f)
        {
            g.Box("fabricPartition", V(0, 0.09f + (height - 0.13f) / 2, 0), V(length - 0.03f, height - 0.13f, thick), 0.006f);
            g.Box("baseboard",V(0,0.045f,0),V(length,0.09f,thick + 0.012f),0.004f);                  
            g.Box("metalLight", V(0, height - 0.02f, 0), V(length, 0.04f, thick + 0.014f), 0.006f);          
            g.Box("metalLight", V(-length / 2 + 0.012f, height / 2, 0), V(0.024f, height, thick + 0.014f), 0.004f);
            g.Box("metalLight", V(length / 2 - 0.012f, height / 2, 0), V(0.024f, height, thick + 0.014f), 0.004f);
        }

public static void FilingCabinet(Geo g, int drawers=4, float w=0.47f, float d=0.62f)
        {
            float h=0.08f + drawers * 0.31f;
            g.Box("cabinet",V(0,h / 2,0),V(w,h,d),0.008f);
            for (int i = 0; i < drawers; i++)
            {
                float y = 0.06f + 0.155f + i * 0.31f;
                g.Box("cabinet", V(0, y, d / 2 + 0.004f), V(w - 0.03f, 0.285f, 0.012f), 0.005f);
                g.Box("chrome", V(0, y + 0.06f, d / 2 + 0.018f), V(0.14f, 0.014f, 0.014f), 0.004f);
                g.Box("paper", V(0, y - 0.02f, d / 2 + 0.0115f), V(0.08f, 0.035f, 0.002f), 0f);
            }
        }

        public static void MetalShelf(Geo g, float w=1.8f, float h=1.9f, float d=0.45f, int shelves=4)
        {
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    g.Box("metalLight",V(sx * (w / 2 - 0.02f),h / 2,sz * (d / 2 - 0.02f)),V(0.035f,h,0.035f),0.003f);
            for (int i = 0; i < shelves; i++)
                g.Box("cabinet", V(0, 0.12f + i * (h - 0.2f) / (shelves - 1), 0), V(w - 0.02f, 0.025f, d - 0.02f), 0.004f);
        }

        public static void CardboardBox(Geo g, float w = 0.4f, float h = 0.3f, float d = 0.3f)
        {
            g.Box("cardboard", V(0, h / 2, 0), V(w, h, d), 0.006f);
            g.Box("tape", V(0, h + 0.0006f, 0), V(w * 0.999f, 0.0012f, 0.05f), 0f);
            g.Box("tape",V(-w / 2 - 0.0006f,h - 0.04f,0),V(0.0012f,0.08f,0.05f),0f);
            g.Box("tape", V(w / 2 + 0.0006f, h - 0.04f, 0), V(0.0012f, 0.08f, 0.05f), 0f);
        }

        public static void Bookcase(Geo g, float w = 0.9f, float h = 1.9f, float d = 0.32f, int seed = 1)
        {
            var rnd = new System.Random(seed);
            g.Box("woodDark", V(-w / 2 + 0.012f, h / 2, 0), V(0.024f, h, d), 0.004f);
            g.Box("woodDark", V(w / 2 - 0.012f, h / 2, 0), V(0.024f, h, d), 0.004f);
            g.Box("woodDark",V(0,h / 2,-d / 2 + 0.008f),V(w,h,0.016f),0.002f);
            string[] spines = { "posterBlue", "posterRed", "posterCream", "cabinet", "signGreen", "plasticBlack" };
            for (int i = 0; i < 5; i++)
            {
                float y = 0.05f + i * (h - 0.07f) / 4f;
                g.Box("woodDark", V(0, y, 0), V(w - 0.03f, 0.024f, d - 0.01f), 0.003f);
                if (i == 4) break;
                float x=-w / 2 + 0.05f;
                while (x < w / 2 - 0.09f)
                {
                    float bw=0.025f + (float)rnd.NextDouble() * 0.035f, bh=0.2f + (float)rnd.NextDouble() * 0.12f;
                    if (rnd.NextDouble() < 0.2) { x += 0.08f; continue; }
                    g.Box(spines[rnd.Next(spines.Length)], V(x + bw / 2, y + 0.012f + bh / 2, 0.01f), V(bw, bh, d * 0.62f), 0.003f);
                    x += bw + 0.004f;
                }
            }
        }

public static void Plant(Geo g, float potR=0.17f, float height=1.1f, int blades=12, int seed=3)
        {
            var rnd=new System.Random(seed);
            float ph=potR * 1.7f;
            g.Lathe("pot",Vector3.zero,new[]
            {
                new Vector2(0f,0f),new Vector2(potR * 0.72f,0f),new Vector2(potR,ph * 0.92f),new Vector2(potR * 1.06f,ph * 0.94f),
                new Vector2(potR * 1.06f,ph),new Vector2(potR * 0.92f,ph),new Vector2(potR * 0.9f,ph * 0.88f)
            },14);
            g.Cyl("soil", V(0, ph * 0.86f, 0), potR * 0.92f, 0.01f, 14);
            for (int i = 0; i < blades; i++)
            {
                float a = (i + (float)rnd.NextDouble() * 0.6f) / blades * Mathf.PI * 2f;
                float len = (height - ph) * (0.55f + (float)rnd.NextDouble() * 0.45f);
                float lean = 0.12f + (float)rnd.NextDouble() * 0.34f;
                Vector3 dir = V(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 root = V(0, ph * 0.86f, 0) + dir * potR * 0.35f * (float)rnd.NextDouble();
                g.Blade(i % 3 == 0 ? "leafDark" : "leaf", root, root + Vector3.up * len + dir * lean * len * 0.4f, dir * lean * len, 0.035f + (float)rnd.NextDouble() * 0.02f, 4);
            }
        }

        public static void TrashBin(Geo g)
        {
            g.Lathe("metal",Vector3.zero,new[]
            {
                new Vector2(0f,0.004f),new Vector2(0.115f,0.004f),new Vector2(0.12f,0f),new Vector2(0.15f,0.33f),new Vector2(0.156f,0.335f),
                new Vector2(0.15f,0.34f),new Vector2(0.142f,0.33f),new Vector2(0.112f,0.012f),new Vector2(0f,0.012f)
            },12);
            g.Ball("paper", V(0.02f, 0.06f, 0.01f), V(0.05f, 0.04f, 0.05f), 6, 4);
        }

        public static void WetFloorSign(Geo g)
        {
            foreach (float s in new[] { -1f, 1f })
                using (g.At(V(0, 0, s * 0.14f), Quaternion.Euler(s * 14f, 0, 0)))
                    g.Box("yellowPlastic", V(0, 0.31f, 0), V(0.3f, 0.62f, 0.012f), 0.005f);
            g.Box("yellowPlastic", V(0, 0.61f, 0), V(0.16f, 0.03f, 0.05f), 0.01f);
        }

        public static void FireExtinguisher(Geo g)
        {
            g.Lathe("extinguisher",Vector3.zero,new[]
            {
                new Vector2(0f,0f),new Vector2(0.06f,0f),new Vector2(0.065f,0.01f),new Vector2(0.065f,0.36f),new Vector2(0.045f,0.42f),new Vector2(0.02f,0.44f),new Vector2(0.02f,0.47f)
            },12);
            g.Box("plasticBlack", V(0, 0.485f, 0.02f), V(0.03f, 0.03f, 0.1f), 0.006f);
            g.Tube("rubber", V(0, 0.46f, 0.02f), V(0.07f, 0.26f, 0.04f), 0.01f, 0.01f, 6);
            g.Quad("paper", V(0, 0.22f, 0.0662f), V(0.035f, 0, 0), V(0, 0.05f, 0));
        }

public static void Copier(Geo g)
        {
            g.Box("plasticGrey",V(0,0.42f,0),V(0.72f,0.72f,0.64f),0.02f);                     
            g.Box("plasticBlack", V(0, 0.03f, 0), V(0.66f, 0.06f, 0.58f), 0.01f);                    
            for (int i = 0; i < 3; i++)
            {
                g.Box("plasticWhite", V(0, 0.16f + i * 0.17f, 0.322f), V(0.62f, 0.15f, 0.012f), 0.006f);
                g.Box("plasticBlack", V(0, 0.2f + i * 0.17f, 0.332f), V(0.16f, 0.02f, 0.012f), 0.004f);
            }
            g.Box("plasticWhite",V(0,0.83f,-0.01f),V(0.74f,0.1f,0.66f),0.02f);                
            g.Box("plasticGrey", V(0, 0.905f, -0.03f), V(0.7f, 0.05f, 0.58f), 0.018f);               
            using (g.At(V(0.2f, 0.9f, 0.29f), Quaternion.Euler(28f, 0, 0)))
            {
                g.Box("plasticBlack", Vector3.zero, V(0.28f, 0.02f, 0.14f), 0.006f);                 
                g.Quad("screenLogin", V(-0.05f, 0.011f, 0), V(0.07f, 0, 0), V(0, 0, -0.05f));
                g.Box("ledGreen",V(0.1f,0.012f,0.02f),V(0.02f,0.006f,0.02f),0.002f);
            }
            g.Box("plasticBlack", V(-0.37f, 0.62f, 0.02f), V(0.03f, 0.03f, 0.4f), 0.004f);           
            using (g.At(V(-0.47f, 0.585f, 0.02f), Quaternion.Euler(0, 0, 10f)))
                g.Box("plasticGrey", Vector3.zero, V(0.24f, 0.015f, 0.36f), 0.006f);                 
        }

        public static void CoffeeMachine(Geo g)
        {
            g.Box("plasticBlack", V(0, 0.2f, -0.05f), V(0.28f, 0.4f, 0.24f), 0.015f);
            g.Box("plasticBlack",V(0,0.36f,0.08f),V(0.28f,0.1f,0.18f),0.015f);
            g.Box("metalLight", V(0, 0.012f, 0.08f), V(0.26f, 0.024f, 0.2f), 0.006f);
            g.Cyl("chrome", V(0, 0.26f, 0.09f), 0.014f, 0.05f, 8);
            g.Box("ledRed", V(0.09f, 0.375f, 0.171f), V(0.018f, 0.018f, 0.004f), 0.002f);
            g.Box("ledGreen",V(0.05f,0.375f,0.171f),V(0.018f,0.018f,0.004f),0.002f);
            g.Cyl("jug", V(0, 0.4f, -0.06f), 0.08f, 0.12f, 10);
            g.Cyl("plasticBlack", V(0, 0.52f, -0.06f), 0.085f, 0.015f, 10);
        }

        public static void Microwave(Geo g)
        {
            g.Box("plasticWhite", V(0, 0.15f, 0), V(0.5f, 0.3f, 0.36f), 0.012f);
            g.Quad("glassDark",V(-0.06f,0.15f,0.1805f),V(0.16f,0,0),V(0,0.11f,0));
            g.Quad("keyboard", V(0.185f, 0.13f, 0.1805f), V(0.045f, 0, 0), V(0, 0.08f, 0));
            g.Box("ledGreen", V(0.185f, 0.25f, 0.181f), V(0.07f, 0.025f, 0.003f), 0.001f);
        }

        public static void Fridge(Geo g)
        {
            g.Box("plasticWhite", V(0, 0.9f, 0), V(0.7f, 1.8f, 0.66f), 0.02f);
            g.Box("plasticWhite",V(0,0.62f,0.335f),V(0.68f,1.2f,0.02f),0.012f);
            g.Box("plasticWhite", V(0, 1.52f, 0.335f), V(0.68f, 0.54f, 0.02f), 0.012f);
            g.Box("chrome", V(-0.29f, 0.95f, 0.36f), V(0.02f, 0.4f, 0.025f), 0.006f);
            g.Box("chrome", V(-0.29f, 1.45f, 0.36f), V(0.02f, 0.24f, 0.025f), 0.006f);
        }

        public static void VendingMachine(Geo g)
        {
            g.Box("plasticBlack",V(0,0.93f,0),V(0.95f,1.86f,0.8f),0.02f);
            g.Quad("vending", V(0, 0.95f, 0.4012f), V(0.44f, 0, 0), V(0, 0.86f, 0));
            g.Box("metal", V(0, 0.03f, 0.02f), V(0.9f, 0.06f, 0.78f), 0.006f);
        }

        public static void WaterCooler(Geo g)
        {
            g.Box("plasticWhite", V(0, 0.5f, 0), V(0.32f, 1.0f, 0.32f), 0.02f);
            g.Box("plasticGrey",V(0,0.7f,0.15f),V(0.24f,0.22f,0.04f),0.01f);
            g.Box("ceramicBlue", V(-0.05f, 0.75f, 0.175f), V(0.03f, 0.03f, 0.03f), 0.005f);
            g.Box("ceramicRed", V(0.05f, 0.75f, 0.175f), V(0.03f, 0.03f, 0.03f), 0.005f);
            g.Box("metalLight", V(0, 0.585f, 0.17f), V(0.22f, 0.012f, 0.08f), 0.004f);
            g.Lathe("jug",V(0,1.0f,0),new[]
            {
                new Vector2(0.05f,0f),new Vector2(0.06f,0.04f),new Vector2(0.135f,0.1f),new Vector2(0.135f,0.4f),new Vector2(0.12f,0.44f),new Vector2(0f,0.445f)
            },14);
        }

        public static void ServerRack(Geo g)
        {
            g.Box("plasticBlack", V(0, 1.0f, 0), V(0.62f, 2.0f, 0.9f), 0.012f);
            g.Quad("serverLeds", V(0, 1.02f, 0.4515f), V(0.27f, 0, 0), V(0, 0.92f, 0));
            g.Box("metal", V(0, 0.03f, 0), V(0.64f, 0.06f, 0.92f), 0.006f);
        }

        
        public static void BreakerPanel(Geo g)
        {
            g.Box("cabinet",V(0,0,0.06f),V(0.6f,0.8f,0.12f),0.008f);
            g.Box("plasticBlack", V(0, 0.05f, 0.122f), V(0.5f, 0.6f, 0.006f), 0.002f);
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 8; c++)
                    g.Box(c == 5 && r == 1 ? "ledRed" : "plasticGrey", V(-0.2f + c * 0.057f, 0.26f - r * 0.13f, 0.13f), V(0.03f, 0.06f, 0.016f), 0.004f);
            
            using (g.At(V(-0.33f, 0, 0.12f), Quaternion.Euler(0, -115f, 0))) g.Box("cabinet",V(-0.29f,0,0),V(0.58f,0.78f,0.012f),0.004f); 
            g.Quad("noteYellow", V(0.0f, -0.33f, 0.127f), V(0.09f, 0, 0), V(0, 0.035f, 0));
        }

        
        public static void PunchClock(Geo g)
        {
            g.Box("plasticGrey", V(0, 0, 0.05f), V(0.26f, 0.34f, 0.1f), 0.012f);
            g.Quad("screenLogin", V(0, 0.07f, 0.1015f), V(0.09f, 0, 0), V(0, 0.045f, 0));
            g.Box("plasticBlack",V(0,-0.07f,0.1f),V(0.16f,0.015f,0.012f),0.003f);
            g.Box("ledGreen", V(0.09f, -0.12f, 0.101f), V(0.015f, 0.015f, 0.004f), 0.002f);
            
            g.Box("cabinet", V(0.36f, 0, 0.02f), V(0.3f, 0.5f, 0.04f), 0.006f);
            for (int i = 0; i < 6; i++) g.Box(i == 2 ? "noteYellow" : "paper",V(0.36f,0.18f - i * 0.075f,0.045f),V(0.24f,0.05f,0.012f),0.002f);
        }

public static void ConferenceTable(Geo g,float w = 3.0f,float d = 1.2f)
        {
            g.Box("woodDark", V(0, 0.725f, 0), V(w, 0.045f, d), 0.015f);
            foreach (float sx in new[] { -1f, 1f })
            {
                g.Box("metal", V(sx * (w / 2 - 0.5f), 0.36f, 0), V(0.1f, 0.68f, d * 0.5f), 0.01f);
                g.Box("metal", V(sx * (w / 2 - 0.5f), 0.015f, 0), V(0.16f, 0.03f, d * 0.75f), 0.006f);
            }
            g.Box("plasticBlack",V(0,0.752f,0),V(0.3f,0.01f,0.12f),0.004f);                    
        }

        public static void RoundTable(Geo g,float r = 0.5f)
        {
            g.Cyl("laminateWhite", V(0, 0.71f, 0), r, 0.03f, 20);
            g.Cyl("chrome", V(0, 0.02f, 0), 0.035f, 0.69f, 8);
            g.Cyl("metal", Vector3.zero, r * 0.55f, 0.02f, 16);
        }

        
        public static void Whiteboard(Geo g, float w=2.0f, float h=1.1f)
        {
            g.Box("metalLight",V(0,0,0.012f),V(w + 0.05f,h + 0.05f,0.024f),0.006f);
            g.Quad("whiteboard", V(0, 0, 0.0245f), V(w / 2, 0, 0), V(0, h / 2, 0));
            g.Box("metalLight", V(0, -h / 2 - 0.02f, 0.05f), V(w * 0.6f, 0.015f, 0.07f), 0.004f);
            g.Box("posterRed", V(-0.1f, -h / 2 - 0.005f, 0.05f), V(0.1f, 0.016f, 0.016f), 0.004f);
            g.Box("posterBlue",V(0.08f,-h / 2 - 0.005f,0.05f),V(0.1f,0.016f,0.016f),0.004f);
        }

        
        public static void WallScreen(Geo g,string screen = "screenError", float w = 1.3f)
        {
            float h = w * 0.5625f;
            g.Box("plasticBlack", V(0, 0, 0.025f), V(w, h, 0.05f), 0.01f);
            g.Quad(screen, V(0, 0, 0.0505f), V(w / 2 - 0.02f, 0, 0), V(0, h / 2 - 0.02f, 0));
        }

        
        public static void Poster(Geo g, string mat, float w = 0.6f, float h = 0.85f)
        {
            g.Box("plasticBlack", V(0, 0, 0.008f), V(w + 0.04f, h + 0.04f, 0.016f), 0.004f);
            g.Quad(mat, V(0, 0, 0.0165f), V(w / 2, 0, 0), V(0, h / 2, 0));
        }

        
        public static void KitchenCounter(Geo g, float w=3.0f)
        {
            g.Box("laminateWhite",V(0,0.44f,0),V(w,0.84f,0.6f),0.008f);
            g.Box("baseboard", V(0, 0.05f, 0.02f), V(w, 0.1f, 0.56f), 0.004f);
            g.Box("woodDark", V(0, 0.885f, 0.01f), V(w + 0.02f, 0.04f, 0.64f), 0.01f);
            int doors = Mathf.RoundToInt(w / 0.5f);
            for (int i = 0; i < doors; i++)
            {
                float x = -w / 2 + (i + 0.5f) * w / doors;
                g.Box("laminateWhite", V(x, 0.47f, 0.305f), V(w / doors - 0.02f, 0.7f, 0.014f), 0.006f);
                g.Box("chrome",V(x + (i % 2 == 0 ? 1 : -1) * (w / doors / 2 - 0.06f),0.72f,0.318f),V(0.014f,0.12f,0.014f),0.004f);
            }
            
            g.Box("metalLight", V(-w / 2 + 0.55f, 0.9f, 0.02f), V(0.5f, 0.012f, 0.42f), 0.004f);
            g.Box("metal", V(-w / 2 + 0.55f, 0.898f, 0.02f), V(0.4f, 0.014f, 0.32f), 0.002f);
            g.Tube("chrome", V(-w / 2 + 0.55f, 0.9f, -0.2f), V(-w / 2 + 0.55f, 1.12f, -0.2f), 0.012f, 0.012f, 8);
            g.Tube("chrome",V(-w / 2 + 0.55f,1.12f,-0.2f),V(-w / 2 + 0.55f,1.1f,-0.06f),0.011f,0.011f,8);
            
            g.Box("laminateWhite", V(0, 1.75f, -0.14f), V(w, 0.6f, 0.32f), 0.008f);
            for (int i = 0; i < doors; i++)
            {
                float x = -w / 2 + (i + 0.5f) * w / doors;
                g.Box("laminateWhite", V(x, 1.75f, 0.026f), V(w / doors - 0.02f, 0.56f, 0.014f), 0.006f);
            }
        }

        
        public static void TrofferFrame(Geo g, float w = 1.2f, float d = 0.6f)
        {
            g.Box("metalLight", V(0, -0.012f, d / 2), V(w + 0.03f, 0.024f, 0.03f), 0.004f);
            g.Box("metalLight",V(0,-0.012f,-d / 2),V(w + 0.03f,0.024f,0.03f),0.004f);
            g.Box("metalLight", V(w / 2, -0.012f, 0), V(0.03f, 0.024f, d), 0.004f);
            g.Box("metalLight", V(-w / 2, -0.012f, 0), V(0.03f, 0.024f, d), 0.004f);
        }

        public static void TrofferDiffuser(Geo g, string mat, float w = 1.2f, float d = 0.6f)
        {
            g.Quad(mat, V(0, -0.016f, 0), V(w / 2 - 0.015f, 0, 0), V(0, 0, d / 2 - 0.015f));
        }

        public static void CeilingVent(Geo g)
        {
            g.Box("metalLight", V(0, -0.01f, 0), V(0.6f, 0.02f, 0.6f), 0.004f);
            for (int i=0; i < 6; i++) g.Box("metal",V(0,-0.022f,-0.22f + i * 0.088f),V(0.52f,0.008f,0.05f),0.002f);
        }

        
        public static void Window(Geo g,float w,float h,float wallThick,float blindDrop)
        {
            float fz = 0.0f;
            g.Box("metal", V(0, h / 2 + 0.025f, fz), V(w + 0.1f, 0.05f, wallThick + 0.04f), 0.004f);
            g.Box("metal", V(0, -h / 2 - 0.025f, fz), V(w + 0.1f, 0.05f, wallThick + 0.04f), 0.004f);
            g.Box("metal", V(-w / 2 - 0.025f, 0, fz), V(0.05f, h, wallThick + 0.04f), 0.004f);
            g.Box("metal",V(w / 2 + 0.025f,0,fz),V(0.05f,h,wallThick + 0.04f),0.004f);
            g.Box("metal", V(0, 0, fz), V(0.04f, h, 0.05f), 0.004f);                                 
            g.Box("laminateWhite", V(0, -h / 2 - 0.04f, wallThick / 2 + 0.07f), V(w + 0.16f, 0.03f, 0.18f), 0.006f); 
            
            int slats = Mathf.RoundToInt(blindDrop * h / 0.045f);
            for (int i = 0; i < slats; i++)
                using (g.At(V(0, h / 2 - 0.03f - i * 0.045f, wallThick / 2 - 0.03f), Quaternion.Euler(28f, 0, 0)))
                    g.Box("plasticWhite", Vector3.zero, V(w - 0.02f, 0.003f, 0.04f), 0f);
            g.Box("plasticWhite",V(0,h / 2 - 0.01f,wallThick / 2 - 0.03f),V(w,0.03f,0.05f),0.004f);
        }

        
        public static void DoorFrame(Geo g,float w,float h,float wallThick)
        {
            g.Box("metal", V(-w / 2 - 0.03f, h / 2, 0), V(0.06f, h + 0.06f, wallThick + 0.03f), 0.006f);
            g.Box("metal", V(w / 2 + 0.03f, h / 2, 0), V(0.06f, h + 0.06f, wallThick + 0.03f), 0.006f);
            g.Box("metal", V(0, h + 0.03f, 0), V(w + 0.12f, 0.06f, wallThick + 0.03f), 0.006f);
        }

        
        public static void DoorLeaf(Geo g, float w, float h)
        {
            g.Box("doorWood",V(w / 2,h / 2 + 0.005f,0),V(w,h - 0.01f,0.045f),0.006f);
            g.Box("metalLight", V(w / 2, 0.12f, 0), V(w - 0.02f, 0.2f, 0.049f), 0.002f);              
            g.Box("glassDark", V(w * 0.72f, h * 0.68f, 0), V(0.14f, 0.5f, 0.049f), 0.002f);           
            foreach (float s in new[] { -1f, 1f })
            {
                g.Cyl("chrome", V(w - 0.08f, 1.02f, s * 0.022f), 0.026f, 0.0f, 10);
                g.Tube("chrome",V(w - 0.08f,1.02f,s * 0.02f),V(w - 0.08f,1.02f,s * 0.065f),0.011f,0.011f,8);
                g.Tube("chrome", V(w - 0.08f, 1.02f, s * 0.06f), V(w - 0.2f, 1.02f, s * 0.06f), 0.01f, 0.01f, 8);
            }
        }

        public static void ExitSignBody(Geo g)
        {
            g.Box("signGreen", V(0, 0, 0), V(0.42f, 0.18f, 0.05f), 0.008f);
            g.Tube("metal", V(-0.12f, 0.09f, 0), V(-0.12f, 0.2f, 0), 0.006f, 0.006f, 6);
            g.Tube("metal",V(0.12f,0.09f,0),V(0.12f,0.2f,0),0.006f,0.006f,6);
        }

        public static void ClockFace(Geo g)
        {
            g.Tube("plasticBlack", V(0, 0, 0), V(0, 0, 0.035f), 0.17f, 0.17f, 24);
            g.Tube("plasticWhite", V(0, 0, 0.03f), V(0, 0, 0.037f), 0.155f, 0.155f, 24);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                g.Box("plasticBlack", V(Mathf.Sin(a) * 0.132f, Mathf.Cos(a) * 0.132f, 0.038f), i % 3 == 0 ? V(0.014f, 0.014f, 0.002f) : V(0.007f, 0.007f, 0.002f), 0f);
            }
        }
    }
}
#endif
