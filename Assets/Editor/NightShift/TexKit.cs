#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightShift.EditorBuild
{
    
    public class Canvas2
    {
        public readonly int W,H;
        public readonly Color[] Px;
        public Canvas2(int w,int h,Color fill) { W = w; H = h; Px = new Color[w * h]; for (int i = 0; i < Px.Length; i++) Px[i] = fill; }

        public void Each(Func<float,float,Color,Color> fn)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    Px[y * W + x] = fn((x + 0.5f) / W,(y + 0.5f) / H,Px[y * W + x]);
        }

        public void Rect(float x0,float y0,float x1,float y1,Color c)
        {
            int ax = Mathf.Clamp(Mathf.RoundToInt(x0 * W),0,W),bx = Mathf.Clamp(Mathf.RoundToInt(x1 * W),0,W);
            int ay = Mathf.Clamp(Mathf.RoundToInt(y0 * H),0,H),by = Mathf.Clamp(Mathf.RoundToInt(y1 * H),0,H);
            for (int y = ay; y < by; y++)
                for (int x = ax; x < bx; x++)
                    Px[y * W + x] = Color.Lerp(Px[y * W + x],new Color(c.r,c.g,c.b,1f),c.a);
        }

        public void Disc(float cx,float cy,float r,Color c,float ring = 0f)
        {
            float aspect = W / (float)H;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = ((x + 0.5f) / W - cx) * aspect,dy = (y + 0.5f) / H - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = 1.5f / H;
                    float a = Mathf.Clamp01((r - d) / edge);
                    if (ring > 0f) a *= Mathf.Clamp01((d - (r - ring)) / edge);
                    if (a > 0f) Px[y * W + x] = Color.Lerp(Px[y * W + x],new Color(c.r,c.g,c.b,1f),a * c.a);
                }
        }

        public void Line(float x0,float y0,float x1,float y1,float thick,Color c)
        {
            float aspect = W / (float)H;
            Vector2 a = new Vector2(x0 * aspect,y0),b = new Vector2(x1 * aspect,y1);
            Vector2 ab = b - a; float len2 = Mathf.Max(1e-6f,ab.sqrMagnitude);
            int minx = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(x0,x1) - thick) * W),0,W - 1),maxx = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(x0,x1) + thick) * W),0,W - 1);
            int miny = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(y0,y1) - thick) * H),0,H - 1),maxy = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(y0,y1) + thick) * H),0,H - 1);
            for (int y = miny; y <= maxy; y++)
                for (int x = minx; x <= maxx; x++)
                {
                    Vector2 p = new Vector2((x + 0.5f) / W * aspect,(y + 0.5f) / H);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a,ab) / len2);
                    float d = (p - (a + ab * t)).magnitude;
                    float al = Mathf.Clamp01((thick * 0.5f - d) / (1.5f / H));
                    if (al > 0f) Px[y * W + x] = Color.Lerp(Px[y * W + x],new Color(c.r,c.g,c.b,1f),al * c.a);
                }
        }
    }

    public static class TexKit
    {
        public const string Dir = "Assets/Generated/Textures";
        const int Version = 4;

static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        
        public static float VNoise(float x, float y, int seed, int period)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = ((xi % period) + period) % period, x1 = (x0 + 1) % period;
            int y0 = ((yi % period) + period) % period, y1 = (y0 + 1) % period;
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx), Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx), fy);
        }

        public static float Fbm(float u, float v, int seed, int basePeriod, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, norm = 0f; int per = basePeriod;
            for (int o = 0; o < octaves; o++)
            {
                sum += VNoise(u * per, v * per, seed + o * 17, per) * amp;
                norm += amp; amp *= 0.5f; per *= 2;
            }
            return sum / norm;
        }

static bool UpToDate(string path)
        {
            string stamp = Dir + "/.version";
            return File.Exists(path) && File.Exists(stamp) && File.ReadAllText(stamp).Trim() == Version.ToString();
        }

        public static void Begin()
        { Directory.CreateDirectory(Dir); }
        public static void End()
        { File.WriteAllText(Dir + "/.version", Version.ToString()); }

        public enum Kind { Albedo, Normal, Sprite, SpriteSliced, Linear }

        public static Texture2D Save(string name, Canvas2 c, Kind kind=Kind.Albedo, bool repeat=true, int maxSize=1024)
        {
            string path=$"{Dir}/{name}.png";
            if (!UpToDate(path))
            {
                var tex = new Texture2D(c.W,c.H,TextureFormat.RGBA32,false);
                tex.SetPixels(c.Px);
                tex.Apply();
                File.WriteAllBytes(path,tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                imp.textureType = kind == Kind.Normal ? TextureImporterType.NormalMap
                    : (kind == Kind.Sprite || kind == Kind.SpriteSliced) ? TextureImporterType.Sprite : TextureImporterType.Default;
                imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.maxTextureSize = maxSize;
                imp.anisoLevel = 8;
                imp.filterMode = FilterMode.Trilinear;
                imp.mipmapEnabled = kind != Kind.Sprite && kind != Kind.SpriteSliced;
                imp.sRGBTexture = kind != Kind.Normal && kind != Kind.Linear;
                imp.alphaIsTransparency = kind == Kind.Sprite || kind == Kind.SpriteSliced;
                if (kind == Kind.Sprite || kind == Kind.SpriteSliced)
                {
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.textureCompression = TextureImporterCompression.Uncompressed;
                    if (kind == Kind.SpriteSliced) { int b = c.W / 3; imp.spriteBorder = new Vector4(b,b,b,b); }
                }
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public static Sprite SpriteOf(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");

        
        public static Texture2D SaveNormal(string name, int size, Func<float, float, float> height, float strength)
        {
            string path = $"{Dir}/{name}.png";
            if (UpToDate(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var h = new float[size, size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) h[x, y] = height((x + 0.5f) / size, (y + 0.5f) / size);
            var c = new Canvas2(size, size, Color.white);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (h[(x + 1) % size, y] - h[(x - 1 + size) % size, y]) * strength * size / 64f;
                    float dy = (h[x, (y + 1) % size] - h[x, (y - 1 + size) % size]) * strength * size / 64f;
                    Vector3 n = new Vector3(-dx, -dy, 1f).normalized;
                    c.Px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            return Save(name, c, Kind.Normal);
        }

        
        public static Texture2D NormalFromPhoto(string name, string photoPath, float strength, int size = 512)
        {
            string path = $"{Dir}/{name}.png";
            if (UpToDate(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var src=new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(photoPath));
            var lum=new float[size, size];
            for (int y=0; y < size; y++)
                for (int x=0; x < size; x++)
                    lum[x, y]=src.GetPixelBilinear((x + 0.5f) / size, (y + 0.5f) / size).grayscale;
            UnityEngine.Object.DestroyImmediate(src);
            return SaveNormal(name, size, (u, v) => lum[Mathf.Min(size - 1, (int)(u * size)), Mathf.Min(size - 1, (int)(v * size))], strength);
        }

public static readonly Dictionary<string, Texture2D> T=new Dictionary<string, Texture2D>();

        public static void BuildAll()
        {
            Begin();
            T.Clear();
            var rnd=new System.Random(1307);
            Func<float> R=() => (float)rnd.NextDouble();

            
            T["carpet"] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/dirty_carpet_diff_1k.jpg");
            T["plaster"] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/plastered_wall_diff_1k.jpg");
            T["fabric"] = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/cotton_jersey_diff_1k.jpg");
            T["carpet_n"] = NormalFromPhoto("carpet_n", "Assets/Textures/dirty_carpet_diff_1k.jpg",2.2f);
            T["plaster_n"] = NormalFromPhoto("plaster_n", "Assets/Textures/plastered_wall_diff_1k.jpg", 1.2f);
            T["fabric_n"] = NormalFromPhoto("fabric_n", "Assets/Textures/cotton_jersey_diff_1k.jpg", 2.0f);

            
            {
                Func<float, float, float> grid = (u, v) =>
                {
                    float gu = Mathf.Abs((u * 2f) % 1f - 0.5f), gv = Mathf.Abs((v * 2f) % 1f - 0.5f);
                    return Mathf.Max(gu, gv) > 0.48f ? 1f : 0f;
                };
                var c = new Canvas2(512, 512, Color.white);
                c.Each((u, v, _) =>
                {
                    float g = grid(u, v);
                    float speck = VNoise(u * 180f, v * 180f, 5, 180) > 0.83f ? 0.78f : 1f;
                    float tone = 0.80f + 0.06f * Fbm(u, v, 9, 4) + 0.03f * Hash((int)(u * 2), (int)(v * 2), 3);
                    Color tile = new Color(tone, tone * 0.99f, tone * 0.96f) * speck;
                    return Color.Lerp(tile, new Color(0.55f, 0.56f, 0.58f), g);
                });
                T["ceiling"]=Save("ceiling",c);
                T["ceiling_n"] = SaveNormal("ceiling_n", 512, (u, v) => grid(u, v) * 1.0f - (VNoise(u * 180f, v * 180f, 5, 180) > 0.83f ? 0.25f : 0f), 1.4f);
            }

            
            Action<string, Color, Color, int> wood = (name, a, b, seed) =>
            {
                var c = new Canvas2(512, 512, Color.white);
                c.Each((u, v, _) =>
                {
                    float grain = Fbm(u * 0.25f + Fbm(u, v, seed + 3, 3) * 0.06f, v, seed, 24, 4);
                    float ring = Mathf.Sin((v + grain * 0.35f) * 60f) * 0.5f + 0.5f;
                    float k = Mathf.Clamp01(grain * 0.7f + ring * 0.3f);
                    return Color.Lerp(a, b, k);
                });
                T[name] = Save(name, c);
            };
            wood("laminate", new Color(0.58f, 0.50f, 0.40f), new Color(0.74f, 0.66f, 0.54f), 11);
            wood("wood_dark",new Color(0.16f,0.09f,0.055f),new Color(0.30f,0.17f,0.10f),23);
            T["wood_n"] = SaveNormal("wood_n", 256, (u, v) => Fbm(u * 0.25f, v, 11, 24, 3), 0.5f);

            
            {
                var c = new Canvas2(256, 256, Color.white);
                c.Each((u, v, _) =>
                {
                    float s = 0.82f + 0.18f * Fbm(u * 0.04f, v, 31, 64, 3);
                    return new Color(s, s, s);
                });
                T["brushed"]=Save("brushed",c);
                T["brushed_n"] = SaveNormal("brushed_n", 256, (u, v) => Fbm(u * 0.04f, v, 31, 64, 3), 0.35f);
            }

            
            T["grain_n"]=SaveNormal("grain_n",256,(u,v) => VNoise(u * 96f,v * 96f,41,96),0.35f);

            
            {
                var c = new Canvas2(512,512,Color.white);
                c.Each((u,v,_) =>
                {
                    bool dark = ((int)(u * 2f) + (int)(v * 2f)) % 2 == 0;
                    Color b = dark ? new Color(0.30f,0.33f,0.32f) : new Color(0.62f,0.63f,0.58f);
                    float n = Fbm(u,v,51,8,5);
                    float gu = Mathf.Abs((u * 2f) % 1f - 0.5f),gv = Mathf.Abs((v * 2f) % 1f - 0.5f);
                    float seam = Mathf.Max(gu,gv) > 0.492f ? 0.6f : 1f;
                    return b * (0.82f + 0.3f * n) * seam;
                });
                T["linoleum"] = Save("linoleum", c);
            }

            
            {
                var c = new Canvas2(256, 256, Color.white);
                c.Each((u, v, _) =>
                {
                    float corr = 0.94f + 0.06f * Mathf.Sin(u * Mathf.PI * 2f * 40f);
                    float n = 0.85f + 0.2f * Fbm(u, v, 61, 8, 4);
                    return new Color(0.60f, 0.45f, 0.29f) * corr * n;
                });
                T["cardboard"]=Save("cardboard",c);
            }

            
            Action<Canvas2,float,float,float,float> textLines = (c,x0,y0,y1,dens) =>
            {
                for (float y = y1; y > y0; y -= 0.035f)
                    if (R() < dens) c.Rect(x0,y,x0 + 0.25f + R() * (0.92f - x0 - 0.25f),y + 0.012f,new Color(0.25f,0.25f,0.28f,0.75f));
            };
            {
                var c = new Canvas2(256,362,new Color(0.93f,0.93f,0.90f));
                c.Rect(0.08f,0.90f,0.6f,0.93f,new Color(0.15f,0.15f,0.2f));
                textLines(c,0.08f,0.08f,0.86f,0.85f);
                T["paper_lines"] = Save("paper_lines", c, Kind.Albedo, false);

                c = new Canvas2(256, 362, new Color(0.94f, 0.94f, 0.91f));
                c.Rect(0f, 0.88f, 1f, 1f, new Color(0.12f, 0.22f, 0.36f));
                c.Rect(0.08f, 0.91f, 0.7f, 0.95f, new Color(0.9f, 0.9f, 0.9f));
                float[] bars = { 0.34f, 0.28f, 0.22f, 0.15f, 0.08f, 0.03f };
                for (int i = 0; i < bars.Length; i++)
                    c.Rect(0.12f + i * 0.13f, 0.5f, 0.21f + i * 0.13f, 0.5f + bars[i], i < 4 ? new Color(0.2f, 0.45f, 0.7f) : new Color(0.8f, 0.2f, 0.15f));
                c.Rect(0.1f, 0.495f, 0.92f, 0.5f, new Color(0.1f, 0.1f, 0.1f));
                textLines(c, 0.08f, 0.06f, 0.44f, 0.8f);
                T["paper_report"]=Save("paper_report",c,Kind.Albedo,false);

                
                c = new Canvas2(256,362,new Color(0.95f,0.95f,0.93f));
                Color ink = new Color(0.04f,0.04f,0.05f);
                c.Disc(0.33f,0.62f,0.085f,ink); c.Disc(0.67f,0.62f,0.085f,ink);
                c.Disc(0.345f,0.615f,0.022f,new Color(0.95f,0.95f,0.93f));
                c.Disc(0.655f,0.615f,0.022f,new Color(0.95f,0.95f,0.93f));
                for (int i = 0; i < 24; i++)
                {
                    float a0 = Mathf.Lerp(-0.42f,0.42f,i / 24f),a1 = Mathf.Lerp(-0.42f,0.42f,(i + 1) / 24f);
                    c.Line(0.5f + a0,0.40f - 0.18f * (1f - a0 * a0 * 5.5f),0.5f + a1,0.40f - 0.18f * (1f - a1 * a1 * 5.5f),0.02f,ink);
                    if (i % 2 == 0) c.Line(0.5f + a0,0.40f - 0.18f * (1f - a0 * a0 * 5.5f),0.5f + a0,0.44f - 0.10f * (1f - a0 * a0 * 5.5f),0.008f,ink);
                }
                c.Line(0.08f,0.40f,0.92f,0.40f,0.014f,ink);
                for (int i = 0; i < 60; i++) { float x = R(),y = R(); c.Line(x,y,x + (R() - 0.5f) * 0.05f,y + (R() - 0.5f) * 0.05f,0.004f,new Color(0.04f,0.04f,0.05f,0.5f)); }
                T["paper_face"] = Save("paper_face", c, Kind.Albedo, false);
            }

            
            {
                var c = new Canvas2(256, 144, new Color(0.86f, 0.89f, 0.92f));
                c.Rect(0f, 0.9f, 1f, 1f, new Color(0.10f, 0.42f, 0.25f));
                c.Rect(0f, 0.82f, 1f, 0.9f, new Color(0.78f, 0.80f, 0.82f));
                for (int i = 1; i < 10; i++) c.Rect(i * 0.1f, 0f, i * 0.1f + 0.004f, 0.82f, new Color(0.6f, 0.63f, 0.66f));
                for (int j = 1; j < 11; j++) c.Rect(0f, j * 0.075f, 1f, j * 0.075f + 0.008f, new Color(0.6f, 0.63f, 0.66f));
                for (int k = 0; k < 26; k++)
                {
                    int i = rnd.Next(0, 10), j = rnd.Next(0, 10);
                    Color col = k % 7 == 0 ? new Color(0.85f, 0.3f, 0.25f) : k % 5 == 0 ? new Color(0.95f, 0.8f, 0.35f) : new Color(0.3f, 0.32f, 0.36f);
                    c.Rect(i * 0.1f + 0.012f, j * 0.075f + 0.02f, i * 0.1f + 0.03f + R() * 0.06f, j * 0.075f + 0.055f, col);
                }
                T["screen_sheet"]=Save("screen_sheet",c,Kind.Albedo,false);

                c = new Canvas2(256,144,Color.black);
                c.Each((u,v,_) => Color.Lerp(new Color(0.03f,0.08f,0.2f),new Color(0.07f,0.22f,0.42f),v + 0.2f * Fbm(u,v,71,3)));
                c.Disc(0.5f,0.62f,0.13f,new Color(0.8f,0.85f,0.9f));
                c.Disc(0.5f,0.66f,0.05f,new Color(0.07f,0.2f,0.4f));
                c.Rect(0.34f,0.26f,0.66f,0.36f,new Color(0.9f,0.92f,0.95f));
                c.Rect(0.36f,0.29f,0.5f,0.33f,new Color(0.3f,0.3f,0.35f));
                T["screen_login"] = Save("screen_login", c, Kind.Albedo, false);

                c = new Canvas2(256, 144, new Color(0.05f, 0.18f, 0.55f));
                c.Rect(0.08f, 0.66f, 0.22f, 0.86f, Color.white);
                for (int j = 0; j < 5; j++) c.Rect(0.08f, 0.5f - j * 0.09f, 0.3f + R() * 0.6f, 0.54f - j * 0.09f, new Color(0.9f, 0.92f, 1f));
                T["screen_error"]=Save("screen_error",c,Kind.Albedo,false);
            }

            
            {
                var c = new Canvas2(256,96,new Color(0.05f,0.05f,0.06f));
                for (int j = 0; j < 5; j++)
                    for (int i = 0; i < 15; i++)
                    {
                        if (j == 0 && i > 3 && i < 10) continue;
                        c.Rect(0.02f + i * 0.064f,0.06f + j * 0.18f,0.02f + i * 0.064f + 0.05f,0.06f + j * 0.18f + 0.13f,new Color(0.16f,0.16f,0.18f));
                    }
                c.Rect(0.02f + 4 * 0.064f,0.06f,0.02f + 9 * 0.064f + 0.05f,0.19f,new Color(0.16f,0.16f,0.18f));
                T["keyboard"] = Save("keyboard", c, Kind.Albedo, false);
            }

            
            {
                var c = new Canvas2(1024, 256, Color.black);
                c.Each((u, v, _) => Color.Lerp(new Color(0.05f, 0.07f, 0.13f), new Color(0.012f, 0.016f, 0.04f), Mathf.Pow(v, 0.7f))
                                    + new Color(0.10f, 0.06f, 0.03f) * Mathf.Pow(1f - v, 5f));
                for (int i = 0; i < 90; i++)
                { float x = R(), y = 0.5f + R() * 0.5f; c.Rect(x, y, x + 0.0012f, y + 0.005f, new Color(0.8f, 0.85f, 1f, 0.4f + R() * 0.5f)); }
                c.Disc(0.81f, 0.78f, 0.075f, new Color(0.85f, 0.88f, 0.8f));
                c.Disc(0.825f, 0.80f, 0.07f, new Color(0.05f, 0.06f, 0.12f, 0.35f));
                for (int layer = 0; layer < 2; layer++)
                {
                    float x = 0f;
                    while (x < 1f)
                    {
                        float w = 0.025f + R() * 0.05f, h = (layer == 0 ? 0.25f : 0.12f) + R() * (layer == 0 ? 0.42f : 0.38f);
                        Color body = layer == 0 ? new Color(0.03f, 0.04f, 0.07f) : new Color(0.012f, 0.015f, 0.028f);
                        c.Rect(x, 0f, x + w, h, body);
                        int cols = Mathf.Max(2, (int)(w / 0.006f)), rows = (int)(h / 0.03f);
                        float lit = 0.05f + R() * 0.22f;
                        for (int ry = 0; ry < rows; ry++)
                            for (int cx = 0; cx < cols; cx++)
                                if (R() < lit)
                                {
                                    Color wc = R() < 0.75f ? new Color(1f, 0.82f, 0.48f) : new Color(0.75f, 0.9f, 1f);
                                    float wx = x + 0.002f + cx * (w - 0.004f) / cols, wy = 0.012f + ry * 0.03f;
                                    c.Rect(wx, wy, wx + (w - 0.004f) / cols * 0.6f, wy + 0.014f, wc * (layer == 0 ? 0.6f : 1f));
                                }
                        x += w + R() * 0.012f;
                    }
                }
                T["skyline"]=Save("skyline",c,Kind.Albedo,true,1024);
            }

            
            {
                var c = new Canvas2(256,512,new Color(0.04f,0.05f,0.08f));
                c.Rect(0.06f,0.30f,0.72f,0.96f,new Color(0.10f,0.13f,0.18f));
                Color[] cans = { new Color(0.9f,0.2f,0.15f),new Color(0.15f,0.5f,0.9f),new Color(0.95f,0.75f,0.1f),new Color(0.2f,0.75f,0.35f),new Color(0.9f,0.45f,0.1f),new Color(0.75f,0.75f,0.8f) };
                for (int j = 0; j < 6; j++)
                {
                    c.Rect(0.06f,0.30f + j * 0.11f,0.72f,0.305f + j * 0.11f,new Color(0.5f,0.55f,0.6f));
                    for (int i = 0; i < 6; i++)
                        c.Rect(0.085f + i * 0.105f,0.315f + j * 0.11f,0.155f + i * 0.105f,0.395f + j * 0.11f,cans[(i + j * 2) % cans.Length] * (0.75f + R() * 0.25f));
                }
                c.Rect(0.78f,0.62f,0.94f,0.80f,new Color(0.15f,0.8f,0.5f));
                c.Rect(0.80f,0.48f,0.92f,0.58f,new Color(0.2f,0.2f,0.22f));
                c.Rect(0.12f,0.08f,0.66f,0.20f,new Color(0.01f,0.01f,0.012f));
                T["vending"] = Save("vending", c, Kind.Albedo, false);
            }

            
            {
                var c = new Canvas2(512, 256, new Color(0.93f, 0.94f, 0.94f));
                Color blue = new Color(0.1f, 0.2f, 0.6f), red = new Color(0.75f, 0.1f, 0.1f), blk = new Color(0.1f, 0.1f, 0.12f);
                c.Line(0.08f, 0.15f, 0.08f, 0.85f, 0.012f, blk);
                c.Line(0.08f, 0.15f, 0.55f, 0.15f, 0.012f, blk);
                float[] ys = { 0.72f, 0.70f, 0.62f, 0.66f, 0.45f, 0.30f, 0.18f };
                for (int i = 0; i < ys.Length - 1; i++) c.Line(0.1f + i * 0.07f, ys[i], 0.17f + i * 0.07f, ys[i + 1], 0.014f, red);
                c.Line(0.47f, 0.25f, 0.52f, 0.18f, 0.014f, red);
                c.Line(0.46f, 0.17f, 0.52f, 0.18f, 0.014f, red);
                for (int j = 0; j < 5; j++) c.Line(0.62f, 0.8f - j * 0.12f, 0.70f + R() * 0.24f, 0.8f - j * 0.12f + (R() - 0.5f) * 0.02f, 0.012f, blue);
                c.Disc(0.82f, 0.25f, 0.09f, blk, 0.012f);
                c.Disc(0.79f, 0.27f, 0.012f, blk); c.Disc(0.85f, 0.27f, 0.012f, blk);
                c.Line(0.78f, 0.21f, 0.86f, 0.21f, 0.01f, blk);
                T["whiteboard"]=Save("whiteboard",c,Kind.Albedo,false);
            }

            
            {
                var c = new Canvas2(128,256,new Color(0.02f,0.02f,0.025f));
                for (int j = 0; j < 20; j++)
                {
                    c.Rect(0.05f,j * 0.05f + 0.005f,0.95f,j * 0.05f + 0.045f,new Color(0.06f,0.06f,0.07f));
                    for (int i = 0; i < 6; i++)
                        if (R() < 0.7f) c.Rect(0.1f + i * 0.05f,j * 0.05f + 0.018f,0.125f + i * 0.05f,j * 0.05f + 0.032f,R() < 0.8f ? new Color(0.1f,0.95f,0.3f) : new Color(1f,0.6f,0.05f));
                }
                T["server_leds"] = Save("server_leds", c, Kind.Albedo, false);
            }

            
            {
                var c = new Canvas2(64, 64, new Color(1, 1, 1, 0));
                c.Each((u, v, _) => new Color(1, 1, 1, Mathf.Clamp01((0.48f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f))) * 64f)));
                Save("ui_circle", c, Kind.Sprite, false);
                c=new Canvas2(128, 128, new Color(1, 1, 1, 0));
                c.Each((u, v, _) => { float d=Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)); return new Color(1, 1, 1, Mathf.Clamp01((0.48f - d) * 128f) * Mathf.Clamp01((d - 0.38f) * 128f)); });
                Save("ui_ring",c,Kind.Sprite,false);
                c = new Canvas2(64,64,new Color(1,1,1,0));
                c.Line(0.18f,0.32f,0.5f,0.8f,0.105f,Color.white);
                c.Line(0.5f,0.8f,0.82f,0.32f,0.105f,Color.white);
                Save("ui_chevron", c, Kind.Sprite, false);
                c = new Canvas2(48, 48, new Color(1, 1, 1, 0));
                c.Each((u, v, _) =>
                {
                    float r = 0.3f;
                    Vector2 p = new Vector2(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f));
                    Vector2 q = new Vector2(Mathf.Max(p.x - (0.5f - r), 0f), Mathf.Max(p.y - (0.5f - r), 0f));
                    return new Color(1, 1, 1, Mathf.Clamp01((r - q.magnitude) * 48f));
                });
                Save("ui_round", c, Kind.SpriteSliced, false);
                c = new Canvas2(128, 128, new Color(1, 1, 1, 0));
                c.Each((u, v, _) => new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f), 2f)));
                Save("ui_soft", c, Kind.Sprite, false);
                c=new Canvas2(8, 64, new Color(1, 1, 1, 0));
                c.Each((u, v, _) => new Color(1, 1, 1, ((int)(v * 64f) % 4) < 2 ? 0.16f : 0f));
                Save("ui_scan",c,Kind.Sprite,true);
                c = new Canvas2(256,8,new Color(0,0,0,0));
                c.Each((u,v,_) => new Color(0,0,0,Mathf.Pow(Mathf.Clamp01(1f - u),1.6f)));
                Save("ui_grad_h", c, Kind.Sprite, false);
                c = new Canvas2(256, 256, new Color(0, 0, 0, 0));
                c.Each((u, v, _) => new Color(0, 0, 0, Mathf.Clamp01(Mathf.Pow(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 1.55f, 2.6f))));
                Save("ui_vignette", c, Kind.Sprite, false);
            }
            End();
            AssetDatabase.SaveAssets();
        }
    }

    
    public static class Mats
    {
        const string Dir = "Assets/Generated/Materials";
        static readonly Dictionary<string, Material> M=new Dictionary<string, Material>();
        static Shader _lit, _unlit;

        public static Material Get(string key)
        {
            if (M.TryGetValue(key, out var m) && m != null) return m;
            Debug.LogError("BUILD missing material key: " + key);
            return M["plasticBlack"];
        }

        static Material Lit(string key, Color color, float smooth = 0.25f, float metal = 0f, string tex = null, string normal = null,
            float tile = 1f, float normalScale = 1f, Color? emission = null, string emissionTex = null)
        {
            string path = $"{Dir}/{key}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            { mat = new Material(_lit); AssetDatabase.CreateAsset(mat, path); }
            else mat.shader = _lit;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness",smooth);
            mat.SetFloat("_Metallic", metal);
            var t = tex != null && TexKit.T.TryGetValue(tex, out var tt) ? tt : null;
            mat.SetTexture("_BaseMap", t);
            mat.SetTextureScale("_BaseMap", new Vector2(tile, tile));
            var n=normal != null && TexKit.T.TryGetValue(normal, out var nn) ? nn : null;
            mat.SetTexture("_BumpMap",n);
            mat.SetFloat("_BumpScale", Mathf.Min(normalScale, 0.45f));
            if (n != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
                var e = emissionTex != null && TexKit.T.TryGetValue(emissionTex, out var ee) ? ee : null;
                mat.SetTexture("_EmissionMap", e);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",Color.black); }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            M[key] = mat;
            return mat;
        }

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        public static Material Ghost { get; private set; }

        public static void BuildAll()
        {
            Directory.CreateDirectory(Dir);
            _lit = Shader.Find("Universal Render Pipeline/Lit");
            _unlit = Shader.Find("Universal Render Pipeline/Unlit");
            M.Clear();

            
            Lit("carpet",Hex("6E7684"), 0.05f, 0f, "carpet", "carpet_n", 0.45f, 1.2f);
            Lit("wall",Hex("B9BBB4"), 0.08f, 0f, "plaster", "plaster_n", 0.4f, 0.8f);
            Lit("wallAccent",Hex("3F5B66"), 0.1f, 0f, "plaster", "plaster_n", 0.4f, 0.8f);
            Lit("wallDark",Hex("6F7474"), 0.08f, 0f, "plaster", "plaster_n", 0.4f, 0.8f);
            Lit("ceiling",Hex("C9C9C4"), 0.05f, 0f, "ceiling", "ceiling_n", 1f / 1.2f, 1f);
            Lit("baseboard",Hex("2A2C30"), 0.3f);
            Lit("linoleum", Hex("C8C8C0"), 0.45f, 0f, "linoleum",null,1f / 1.2f);
            Lit("concrete", Hex("5E6062"), 0.15f, 0f, "plaster", "plaster_n",0.5f,1.2f);

            
            Lit("laminate", Hex("E6DFD2"), 0.35f, 0f, "laminate", "wood_n",0.9f,0.4f);
            Lit("laminateWhite", Hex("D9DAD6"), 0.4f, 0f, null, "grain_n", 4f, 0.5f);
            Lit("woodDark",Hex("FFFFFF"), 0.45f, 0f, "wood_dark", "wood_n", 0.8f, 0.5f);
            Lit("doorWood",Hex("C9A27A"), 0.3f, 0f, "laminate", "wood_n", 0.6f, 0.5f);
            Lit("metal",Hex("3A3D42"), 0.5f, 0.75f, "brushed", "brushed_n", 2f, 0.6f);
            Lit("metalLight",Hex("9AA0A6"), 0.55f, 0.85f, "brushed", "brushed_n", 2f, 0.6f);
            Lit("cabinet",Hex("7C8388"), 0.35f, 0.5f, "brushed", "brushed_n", 1.5f, 0.5f);
            Lit("chrome",Hex("D5D8DC"), 0.85f, 1f);
            Lit("elevatorMetal", Hex("8E9499"), 0.62f, 0.9f, "brushed", "brushed_n", 1.5f, 0.8f);
            Lit("plasticBlack", Hex("17181B"), 0.42f, 0f, null, "grain_n",6f,0.5f);
            Lit("plasticGrey", Hex("9C9F9C"), 0.38f, 0f, null, "grain_n", 6f, 0.5f);
            Lit("plasticWhite",Hex("DCDDD8"), 0.4f, 0f, null, "grain_n", 6f, 0.4f);
            Lit("rubber", Hex("0C0C0D"),0.15f);
            Lit("fabricPartition", Hex("5F7480"), 0.03f, 0f, "fabric", "fabric_n",1.6f,1.3f);
            Lit("fabricChair", Hex("2E3238"), 0.05f, 0f, "fabric", "fabric_n",3f,1.3f);
            Lit("fabricRed", Hex("7A2A26"), 0.05f, 0f, "fabric", "fabric_n",3f,1.3f);
            Lit("cardboard", Hex("FFFFFF"), 0.1f, 0f, "cardboard", null, 1.5f);
            Lit("tape",Hex("B79A6A"), 0.5f);
            Lit("paper", Hex("ECEBE4"), 0.15f);
            Lit("paperLines",Hex("FFFFFF"), 0.15f, 0f, "paper_lines");
            Lit("paperReport", Hex("FFFFFF"),0.15f,0f, "paper_report");
            Lit("paperFace", Hex("FFFFFF"), 0.15f, 0f, "paper_face");
            Lit("noteYellow", Hex("F2D95C"), 0.1f);
            Lit("notePink", Hex("EE8FB0"),0.1f);
            Lit("ceramicWhite", Hex("E8E6DF"), 0.7f);
            Lit("ceramicRed", Hex("A8322B"),0.7f);
            Lit("ceramicBlue", Hex("2F5C8A"), 0.7f);
            Lit("coffee", Hex("1A0E08"),0.8f);
            Lit("pot", Hex("9B5B3C"), 0.2f);
            Lit("soil", Hex("231A14"),0.05f);
            Lit("leaf", Hex("2F6B38"), 0.35f);
            Lit("leafDark", Hex("1D4A2A"),0.3f);
            Lit("jug", Hex("7FB6DA"), 0.9f, 0f, null, null, 1f, 1f, Hex("16303F"));
            Lit("glassDark",Hex("0A0F16"), 0.95f, 0.2f);
            Lit("whiteboard", Hex("FFFFFF"), 0.75f, 0f, "whiteboard");
            Lit("keyboard", Hex("FFFFFF"), 0.3f, 0f, "keyboard");
            Lit("yellowPlastic",Hex("E3B81C"), 0.4f);
            Lit("extinguisher", Hex("B3201B"), 0.6f, 0.2f);
            Lit("black",Hex("040405"), 0.1f);
            Lit("posterBlue", Hex("27465E"), 0.2f);
            Lit("posterRed",Hex("6B2420"), 0.2f);
            Lit("posterCream", Hex("D8CFB8"), 0.2f);
            Lit("signGreen",Hex("0D3B22"), 0.3f);

            
            Lit("fixtureOn", Hex("F2F6FF"), 0.2f, 0f, null, null, 1f, 1f, new Color(0.85f, 0.93f, 1f) * 1.45f);
            Lit("fixtureOff",Hex("6A6F74"), 0.25f);
            Lit("fixtureRed", Hex("FF5040"), 0.2f, 0f, null, null, 1f, 1f, new Color(1f, 0.16f, 0.1f) * 1.6f);
            Lit("lampGlow",Hex("FFE2B0"), 0.2f, 0f, null, null, 1f, 1f, new Color(1f, 0.74f, 0.42f) * 3f);
            Lit("screenSheet", Hex("101010"), 0.85f, 0f, "screen_sheet",null,1f,1f,Color.white * 1.15f, "screen_sheet");
            Lit("screenLogin", Hex("101010"), 0.85f, 0f, "screen_login",null,1f,1f,Color.white * 1.3f, "screen_login");
            Lit("screenError", Hex("101010"), 0.85f, 0f, "screen_error",null,1f,1f,Color.white * 1.3f, "screen_error");
            Lit("screenOff", Hex("050608"), 0.9f);
            Lit("skyline",Hex("000000"), 0f, 0f, null, null, 1f, 1f, Color.white * 1.6f, "skyline");
            Lit("vending", Hex("202020"),0.8f,0f, "vending", null, 1f, 1f, Color.white * 1.5f, "vending");
            Lit("serverLeds", Hex("101010"),0.6f,0f, "server_leds", null, 1f, 1f, Color.white * 1.2f, "server_leds");
            Lit("exitGlow", Hex("15E06A"),0.2f,0f,null,null,1f,1f,new Color(0.1f,1f,0.45f) * 1.4f);
            Lit("ledRed", Hex("FF2A1A"), 0.2f, 0f, null, null, 1f, 1f, new Color(1f, 0.12f, 0.06f) * 3f);
            Lit("ledGreen", Hex("2AFF6A"),0.2f,0f,null,null,1f,1f,new Color(0.1f,1f,0.35f) * 3f);
            Lit("ledAmber", Hex("FFB02A"), 0.2f, 0f, null, null, 1f, 1f, new Color(1f, 0.6f, 0.1f) * 3f);

            
            Lit("skin", Hex("B98F78"),0.24f);
            Lit("skinPale", Hex("A09B93"), 0.22f);
            Lit("suit", Hex("14161B"),0.1f,0f, "fabric", "fabric_n", 4f, 1f);
            Lit("shirt", Hex("D9DBD7"),0.15f);
            Lit("tie", Hex("7E1612"), 0.35f);
            Lit("shoe", Hex("0B0B0C"),0.6f);
            Lit("hair", Hex("16120F"), 0.35f);
            Lit("hairBrown", Hex("4A3020"),0.35f);
            Lit("cardigan", Hex("A89B80"), 0.05f, 0f, "fabric", "fabric_n",4f,1.2f);
            Lit("trousers", Hex("3A3F48"), 0.1f, 0f, "fabric", "fabric_n",4f,1f);
            Lit("trousersDark", Hex("2B2E35"), 0.14f, 0f, "fabric", "fabric_n",4f,1f);
            Lit("internShirt", Hex("7FA3C7"), 0.12f);
            Lit("eyeWhite", Hex("D8D6CC"),0.85f,0f,null,null,1f,1f,new Color(0.9f,0.9f,0.82f) * 0.15f);
            Lit("eyeDark", Hex("030303"), 0.9f);
            Lit("mouth", Hex("120405"),0.6f);
            Lit("teeth", Hex("D8D4C2"), 0.6f);
            Lit("badge", Hex("E6E6E0"),0.5f);

            
            {
                string path = $"{Dir}/ghost.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(_unlit); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = _unlit;
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 2f); 
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                mat.SetInt("_ZWrite",0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetColor("_BaseColor", new Color(0.45f, 0.8f, 1f, 0.22f));
                EditorUtility.SetDirty(mat);
                Ghost = mat;
                M["ghost"]=mat;
            }
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
