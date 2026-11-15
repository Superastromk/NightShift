#if UNITY_EDITOR
using UnityEngine;
using NightShift.Monster;

namespace NightShift.EditorBuild
{
    public class FigureSpec
    {
        public string Skin = "skin", Top = "suit", Bottom="suit",Shirt = "shirt", Tie = "tie", Hair = "hair", Shoes="shoe";
        public float Scale = 1f;
        
        public float ArmLength = 1f;
        public float MouthWidth = 0.05f;
        public bool Jacket = true,HasTie = true,Badge = true,LongFingers = false;
        public float EyeSize = 0.0135f;
        
        public bool SunkenEyes = false;
        
        public float Build = 0.5f;
    }

    public static class Figures
    {
        static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
        static Vector4 S(float y,float rx,float rz,float z = 0f) => new Vector4(y,rx,rz,z);

        const float HipHeight = 0.945f,ThighLength = 0.415f,ShinLength = 0.405f;
        const float SpineOffset = 0.10f,ShoulderHeight = 0.43f,ShoulderWidth = 0.185f,NeckTop = 0.505f;

        static Transform Joint(string name,Transform parent,Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition = localPos;
            return go.transform;
        }

        static void Attach(Transform joint,Geo g,string name)
        {
            var go = g.Emit(name,joint,false,true);
            go.layer = joint.gameObject.layer;
        }

        public static FigureAnimator Build(string name,Transform parent,FigureSpec s,int layer)
        {
            var root = new GameObject(name);
            if (parent != null) root.transform.SetParent(parent,false);
            root.transform.localScale = Vector3.one * s.Scale;
            var anim = root.AddComponent<FigureAnimator>();
            float broad = Mathf.Lerp(0.92f,1.1f,s.Build);

            
            var hips = Joint("Hips", root.transform, V(0, HipHeight, 0));
            var g = new Geo();
            g.Loft(s.Bottom, Vector3.zero, new[]
            {
                S(-0.13f, 0.12f * broad, 0.085f), S(-0.07f, 0.158f * broad, 0.1f, 0.004f), S(0.0f, 0.162f * broad, 0.104f, 0.006f),
                S(0.07f, 0.152f * broad, 0.1f, 0.004f), S(0.11f, 0.148f * broad, 0.098f),
            }, 24);
            g.Loft("plasticBlack", Vector3.zero, new[] { S(0.075f, 0.155f * broad, 0.103f, 0.004f), S(0.11f, 0.151f * broad, 0.1f, 0.002f) }, 16, false, false); 
            g.Box("chrome", V(0, 0.092f, 0.105f), V(0.038f, 0.026f, 0.008f), 0.003f);
            if (s.Jacket)  
                g.Loft(s.Top, Vector3.zero, new[] { S(0.12f, 0.16f * broad, 0.108f, 0.004f), S(0.0f, 0.17f * broad, 0.115f, 0.008f), S(-0.1f, 0.172f * broad, 0.118f, 0.01f) }, 16, false, false);
            Attach(hips, g, name + "_Pelvis");

            
            var spine = Joint("Spine", hips, V(0, SpineOffset, 0));
            g = new Geo();
            g.Loft(s.Top, Vector3.zero, new[]
            {
                S(-0.02f, 0.15f * broad, 0.1f), S(0.09f, 0.156f * broad, 0.106f, 0.006f), S(0.19f, 0.17f * broad, 0.116f, 0.012f),
                S(0.3f, 0.184f * broad, 0.122f, 0.014f), S(0.39f, 0.196f * broad, 0.118f, 0.006f), S(0.45f, 0.18f * broad, 0.1f, -0.004f),
                S(0.49f, 0.12f, 0.078f, -0.01f), S(0.525f, 0.062f, 0.056f, 0f),
            }, 24);
            foreach (float sx in new[] { -1f, 1f })
                g.Ball(s.Top, V(sx * (ShoulderWidth - 0.012f) * broad, ShoulderHeight - 0.02f, -0.004f), V(0.062f, 0.05f, 0.07f), 12, 8); 
            float chestFront = 0.134f;
            if (s.Jacket || s.HasTie)
            {
                
                using (g.At(V(0, 0.39f, chestFront - 0.004f), Quaternion.Euler(-8f, 0, 0)))
                    g.Box(s.Shirt, Vector3.zero, V(0.075f, 0.2f, 0.01f), 0.004f);
            }
            if (s.Jacket)
            {
                foreach (float sx in new[] { -1f, 1f })
                    using (g.At(V(sx * 0.055f, 0.36f, chestFront + 0.002f), Quaternion.Euler(-6f, 0, sx * 18f)))
                        g.Box(s.Top, Vector3.zero, V(0.05f, 0.24f, 0.012f), 0.004f); 
                g.Ball("plasticBlack", V(0.0f, 0.13f, 0.124f), V(0.009f, 0.009f, 0.004f), 6, 4);
                g.Ball("plasticBlack", V(0.0f, 0.06f, 0.118f), V(0.009f, 0.009f, 0.004f), 6, 4);
            }
            if (s.HasTie)
            {
                g.Box(s.Tie, V(0, 0.48f, chestFront + 0.004f), V(0.04f, 0.035f, 0.014f), 0.005f);
                using (g.At(V(0, 0.36f, chestFront + 0.006f), Quaternion.Euler(-7f, 0, 0)))
                    g.Loft(s.Tie, Vector3.zero, new[] { S(-0.11f, 0.004f, 0.004f), S(-0.09f, 0.026f, 0.004f), S(0.1f, 0.02f, 0.004f) }, 4);
            }
            g.Loft(s.Shirt, Vector3.zero, new[] { S(0.48f, 0.072f, 0.064f, 0.004f), S(0.53f, 0.066f, 0.06f, 0.004f) }, 12, false, false); 
            if (s.Badge)
            {
                g.Box("badge",V(-0.11f,0.33f,chestFront + 0.002f),V(0.05f,0.072f,0.004f),0.002f);
                g.Box("posterBlue", V(-0.11f, 0.352f, chestFront + 0.0045f), V(0.042f, 0.018f, 0.001f), 0f);
            }
            Attach(spine, g, name + "_Torso");

            
            var head = Joint("Head", spine, V(0, NeckTop, 0));
            g=new Geo();
            g.Loft(s.Skin, Vector3.zero, new[] { S(-0.06f, 0.054f, 0.054f), S(0.05f, 0.05f, 0.05f, 0.004f) }, 12, false, false); 
            g.Loft(s.Skin, Vector3.zero, new[]
            {
                S(0.015f, 0.04f, 0.045f, -0.025f), S(0.055f, 0.065f, 0.065f, -0.016f),
                S(0.065f, 0.066f, 0.086f, 0.012f),
                S(0.08f, 0.072f, 0.096f, 0.006f), S(0.11f, 0.076f, 0.1f, 0.003f), S(0.14f, 0.079f, 0.102f, 0f),
                S(0.17f, 0.08f, 0.101f, -0.004f), S(0.2f, 0.076f, 0.095f, -0.008f), S(0.225f, 0.062f, 0.08f, -0.012f),
                S(0.245f, 0.036f, 0.046f, -0.015f), S(0.255f, 0.004f, 0.004f, -0.015f),
            }, 24);
            
            g.Tube(s.Skin, V(0, 0.142f, 0.098f), V(0, 0.103f, 0.122f), 0.009f, 0.013f, 8);
            g.Ball(s.Skin, V(0, 0.101f, 0.12f), V(0.017f, 0.013f, 0.013f), 8, 5);
            
            foreach (float sx in new[] { -1f, 1f })
                g.Ball(s.Skin, V(sx * 0.078f, 0.132f, -0.006f), V(0.011f, 0.028f, 0.019f), 8, 5);
            
            foreach (float sx in new[] { -1f, 1f })
            {
                float socket=s.SunkenEyes ? 1.5f : 1f;
                g.Ball(s.Skin, V(sx * 0.034f, 0.151f, 0.092f), V(0.02f, 0.006f, 0.01f), 16, 8);
                g.Ball(s.Skin, V(sx * 0.034f, 0.131f, 0.092f), V(0.018f, 0.004f, 0.008f), 16, 8);
                g.Ball("eyeDark",V(sx * 0.034f,0.142f,0.088f),V(0.021f * socket,0.013f * socket,0.012f),8,6);
                g.Ball("eyeWhite", V(sx * 0.034f, 0.142f, 0.09f), V(s.EyeSize, s.EyeSize * 0.8f, s.EyeSize * 0.75f), 8, 6);
                g.Ball("eyeDark", V(sx * 0.034f, 0.142f, 0.09f + s.EyeSize * 0.68f), V(s.EyeSize * 0.42f, s.EyeSize * 0.42f, 0.002f), 6, 4);
                using (g.At(V(sx * 0.036f, 0.162f, 0.1f), Quaternion.Euler(0, 0, sx * -6f)))
                    g.Box(s.Hair, Vector3.zero, V(0.038f, 0.007f, 0.012f), 0.003f);
            }
            
            g.Loft(s.Skin, V(0, 0.158f, 0f), new[] { S(-0.012f, 0.07f, 0.098f, 0.004f), S(0f, 0.074f, 0.104f, 0.004f), S(0.012f, 0.07f, 0.1f, 0.002f) }, 16, false, false);
            foreach (float sx in new[] { -1f, 1f })
                g.Ball(s.Skin, V(sx * 0.052f, 0.118f, 0.072f), V(0.022f, 0.016f, 0.02f), 8, 5);
            
            g.Ball(s.Skin, V(0, 0.074f, 0.097f), V(s.MouthWidth * 0.85f, 0.007f, 0.011f), 10, 5);

            g.Box("mouth", V(0, 0.067f, 0.1f), V(s.MouthWidth * 1.5f, 0.004f, 0.008f), 0.0015f);
            
            g.Loft(s.Hair, Vector3.zero, new[]
            {
                S(0.178f, 0.0835f, 0.104f, -0.012f), S(0.2f, 0.08f, 0.099f, -0.012f), S(0.226f, 0.066f, 0.084f, -0.015f),
                S(0.247f, 0.04f, 0.05f, -0.017f), S(0.259f, 0.005f, 0.005f, -0.017f),
            }, 16, false, true);
            g.Ball(s.Hair, V(0, 0.16f, -0.03f), V(0.081f, 0.082f, 0.078f), 12, 8);
            Attach(head, g, name + "_Head");

            
            var jaw = Joint("Jaw", head, V(0, 0.065f, 0.012f));
            g = new Geo();
            g.Loft(s.Skin, Vector3.zero, new[] {
                S(-0.065f, 0.033f, 0.024f, 0.04f),
                S(-0.045f, 0.047f, 0.035f, 0.04f),
                S(-0.015f, 0.052f, 0.04f, 0.04f),
                S(0f, 0.052f, 0.04f, 0.04f)
            }, 24, true, false);
            g.Ball("mouth", V(0, 0.001f, 0.07f), V(s.MouthWidth * 0.9f, 0.012f, 0.025f), 16, 8);
            g.Ball(s.Skin, V(0, -0.007f, 0.083f), V(s.MouthWidth * 0.65f, 0.006f, 0.008f), 16, 8);
            g.Box("teeth", V(0, 0f, 0.078f), V(s.MouthWidth, 0.004f, 0.006f), 0.001f);
            Attach(jaw, g, name + "_Jaw");

            
            var shoulders = new Transform[2];
            var elbows = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                float upper = 0.30f * s.ArmLength,lower = 0.265f * s.ArmLength;
                var sh = Joint(i == 0 ? "ShoulderL" : "ShoulderR", spine, V(sx * ShoulderWidth * broad, ShoulderHeight, 0));
                g = new Geo();
                g.Loft(s.Top, Vector3.zero, new[]
                {
                    S(0.03f, 0.05f, 0.054f), S(-0.04f, 0.058f, 0.06f), S(-0.14f, 0.051f, 0.053f), S(-upper + 0.03f, 0.045f, 0.046f), S(-upper - 0.02f, 0.043f, 0.044f),
                }, 16);
                Attach(sh, g, name + "_UpperArm" + i);

                var el=Joint(i == 0 ? "ElbowL" : "ElbowR", sh, V(0, -upper, 0));
                g = new Geo();
                g.Loft(s.Top, Vector3.zero, new[]
                {
                    S(0.02f, 0.043f, 0.045f), S(-0.07f, 0.045f, 0.046f, 0.004f), S(-0.17f, 0.038f, 0.038f), S(-lower + 0.02f, 0.034f, 0.033f),
                }, 16);
                g.Loft(s.Shirt, Vector3.zero, new[] { S(-lower + 0.025f, 0.035f, 0.034f), S(-lower - 0.004f, 0.034f, 0.033f) }, 12, false, false);  
                g.Loft(s.Skin, Vector3.zero, new[] { S(-lower + 0.01f, 0.026f, 0.022f), S(-lower - 0.03f, 0.027f, 0.019f) }, 10, false, false);       
                Hand(g, V(0, -lower - 0.03f, 0), s, sx);
                Attach(el, g, name + "_Forearm" + i);
                shoulders[i] = sh; elbows[i] = el;
            }

            
            var hipJoints = new Transform[2];
            var knees = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1f : 1f;
                var hp = Joint(i == 0 ? "HipL" : "HipR",hips,V(sx * 0.09f * broad,-0.045f,0));
                g = new Geo();
                g.Loft(s.Bottom,Vector3.zero,new[]
                {
                    S(0.05f,0.08f,0.084f),S(-0.05f,0.083f,0.087f,0.006f),S(-0.2f,0.07f,0.074f,0.008f),S(-0.36f,0.058f,0.06f,0.004f),S(-ThighLength - 0.01f,0.053f,0.055f),
                },16);
                Attach(hp,g,name + "_Thigh" + i);

                var kn = Joint(i == 0 ? "KneeL" : "KneeR", hp, V(0, -ThighLength, 0));
                g=new Geo();
                g.Loft(s.Bottom, Vector3.zero, new[]
                {
                    S(0.03f, 0.053f, 0.056f), S(-0.09f, 0.055f, 0.058f, -0.006f), S(-0.22f, 0.047f, 0.048f, -0.004f), S(-0.37f, 0.042f, 0.044f), S(-ShinLength + 0.01f, 0.044f, 0.046f),
                }, 16);
                Shoe(g, V(0, -ShinLength, 0), s.Shoes);
                Attach(kn, g, name + "_Shin" + i);
                hipJoints[i] = hp; knees[i] = kn;
            }

            anim.Hips = hips; anim.Spine = spine; anim.Head = head; anim.Jaw = jaw;
            anim.ShoulderL = shoulders[0]; anim.ShoulderR = shoulders[1];
            anim.ElbowL = elbows[0]; anim.ElbowR = elbows[1];
            anim.HipL = hipJoints[0]; anim.HipR = hipJoints[1];
            anim.KneeL = knees[0]; anim.KneeR = knees[1];
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            return anim;
        }

        
        static void Hand(Geo g,Vector3 wrist,FigureSpec s,float side)
        {
            using (g.At(wrist,Quaternion.Euler(0,side * 90f,0)))
            {
                g.Loft(s.Skin,Vector3.zero,new[] { S(0.0f,0.032f,0.018f),S(-0.05f,0.046f,0.022f),S(-0.098f,0.044f,0.018f) },10);
                float length = s.LongFingers ? 0.15f : 0.075f;
                for (int f = 0; f < 4; f++)
                {
                    float x = -0.03f + f * 0.02f;
                    float len = length * (f == 0 || f == 3 ? 0.86f : 1f);
                    Vector3 a = V(x,-0.092f,0f),b = a + V(0,-len * 0.55f,0.008f),c = b + V(0,-len * 0.45f,0.018f);
                    g.Tube(s.Skin,a,b,0.0098f,0.009f,6);
                    g.Tube(s.Skin,b,c,0.009f,s.LongFingers ? 0.0045f : 0.0075f,6);
                }
                g.Tube(s.Skin,V(0.034f,-0.03f,0.008f),V(0.046f,-0.075f,0.02f),0.011f,0.008f,6);
            }
        }

        static void Shoe(Geo g,Vector3 ankle,string mat)
        {
            g.Loft(mat,ankle,new[] { S(0.02f,0.044f,0.046f),S(-0.035f,0.05f,0.06f,0.012f) },12,false,false);
            g.Box(mat,ankle + V(0,-0.045f,0.045f),V(0.096f,0.06f,0.24f),0.028f);
            g.Ball(mat,ankle + V(0,-0.05f,0.15f),V(0.048f,0.032f,0.06f),10,6);
            g.Box("rubber", ankle + V(0, -0.072f, 0.05f), V(0.1f, 0.016f, 0.275f), 0.006f);
        }
    }
}
#endif
