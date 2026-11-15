#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NightShift.Audio;
using NightShift.Core;
using NightShift.Monster;
using NightShift.Objectives;
using NightShift.Player;
using NightShift.Progression;
using NightShift.Rewind;
using NightShift.Scares;
using NightShift.UI;
using NightShift.World;

namespace NightShift.EditorBuild
{
    public static partial class OfficeBuilder
    {
        public const string ScenePath = "Assets/Scenes/OfficeFloor.unity";

        
        public const float XMin = -20f, XMax = 20f, ZMin = -14f, ZMax = 14f, H = 2.8f, WallT = 0.2f;
        public const float CorrZ0 = 5f, CorrZ1 = 8f;
        public const float DoorW = 1.0f, DoorH = 2.08f;
        public static readonly float[] RoomX = { -20f, -14.5f, -9f, -2.5f, 4f, 9.5f, 20f };
        public static readonly string[] RoomNames = { "Manager", "Meeting1", "Meeting2", "Break", "Storage", "Lobby" };
        public const float LobbyOpenX0 = 11.5f,LobbyOpenX1 = 18f;
        public static readonly float[] FarmOpenings = { -14f,0f,14f }; 

        public const int LPlayer = 6,LProp = 7,LMonster = 8,LNoNav = 9;

        static readonly Dictionary<string,Geo> Zones = new Dictionary<string,Geo>();
        static readonly Dictionary<string,GameObject> ColliderHosts = new Dictionary<string,GameObject>();
        static Transform _world,_dynamic,_colliders,_labels,_lights;
        static readonly List<string> Report = new List<string>();
        static readonly List<GameObject> DynamicProps = new List<GameObject>();
        static System.Random _rnd;

        static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);
        static float R() => (float)_rnd.NextDouble();
        static float R(float a,float b) => a + (b - a) * R();
        static void Log(string s) { Report.Add(s); }

[MenuItem("NightShift/Rebuild Office Scene")]
        public static void Run()
        {
            try { RunInternal(); }
            catch (Exception e)
            {
                Debug.LogError("NSBUILD FAILED " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
        }

        static void RunInternal()
        {
            Report.Clear(); Zones.Clear(); ColliderHosts.Clear(); DynamicProps.Clear();
            _rnd = new System.Random(20261114);

            QualitySetup.EnsureLayers();
            TexKit.BuildAll();
            Mats.BuildAll();
            MeshStore.Begin();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            _world = new GameObject("World").transform;
            _dynamic=new GameObject("DynamicProps").transform;
            _colliders = new GameObject("Colliders").transform;
            _colliders.SetParent(_world, false);
            _labels = new GameObject("Signage").transform; _labels.SetParent(_world, false);
            _lights = new GameObject("Lights").transform;

            Environment();
            Shell();
            Dress();                 
            var refs=Gameplay();   

            
            foreach (var kv in Zones)
            {
                var go=kv.Value.Emit("Zone_" + kv.Key,_world,true);
                Log($"zone {kv.Key} verts={kv.Value.VertexCount}");
            }
            MeshStore.End();

            BakeNavMesh(refs);
            UiBuilder.Build(refs);
            PostFx(refs);
            FloatCheck();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            QualitySetup.Run();
            try
            {
                StaticOcclusionCulling.smallestOccluder = 2.5f;
                StaticOcclusionCulling.smallestHole = 0.4f;
                bool ok = StaticOcclusionCulling.Compute();
                Log("occlusion bake " + (ok ? "ok" : "failed") + $" size={StaticOcclusionCulling.umbraDataSize}");
            }
            catch (Exception e)
            { Log("occlusion bake skipped: " + e.Message); }
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            foreach (var line in Report) Debug.Log("NSBUILD " + line);
            Debug.Log("NSBUILD DONE");
        }

public static string ZoneName(Vector3 p)
        {
            if (p.z > CorrZ1 - 0.05f)
            {
                for (int i = 0; i < RoomNames.Length; i++) if (p.x < RoomX[i + 1]) return RoomNames[i];
                return RoomNames[RoomNames.Length - 1];
            }
            if (p.z > CorrZ0 - 0.05f) return p.x < 0f ? "CorridorW" : "CorridorE";
            string col=p.x < -6.5f ? "W" : p.x < 6.5f ? "C" : "E";
            return "Farm" + col + (p.z < -4.2f ? "S" : "N");
        }

        public static Geo Z(Vector3 p)
        {
            string n = ZoneName(p);
            if (!Zones.TryGetValue(n, out var g)) { g = new Geo(); Zones[n] = g; }
            return g;
        }

        
        static void Put(Vector3 pos, float yaw, Action<Geo> fn)
        {
            var g = Z(pos);
            using (g.At(pos, yaw)) fn(g);
        }

        
        static void Col(Vector3 center, Vector3 size, float yaw = 0f, int layer = 0)
        {
            float y = Mathf.Repeat(yaw, 360f);
            bool axis = Mathf.Abs(y % 90f) < 0.5f || Mathf.Abs(y % 90f - 90f) < 0.5f;
            if (axis)
            {
                int quarter = Mathf.RoundToInt(y / 90f) % 4;
                if (quarter % 2 == 1) size = new Vector3(size.z, size.y, size.x);
                string key = ZoneName(center) + "_" + layer;
                if (!ColliderHosts.TryGetValue(key, out var host))
                {
                    host = new GameObject("Col_" + key) { layer=layer };
                    host.transform.SetParent(_colliders, false);
                    GameObjectUtility.SetStaticEditorFlags(host, StaticEditorFlags.BatchingStatic);
                    ColliderHosts[key]=host;
                }
                var bc=host.AddComponent<BoxCollider>();
                bc.center=center; bc.size=size;
            }
            else
            {
                var go=new GameObject("ColR") { layer = layer };
                go.transform.SetParent(_colliders,false);
                go.transform.SetPositionAndRotation(center,Quaternion.Euler(0,yaw,0));
                go.AddComponent<BoxCollider>().size = size;
            }
        }

        
        static void ColLocal(Vector3 pos,float yaw,Vector3 localCenter,Vector3 size,int layer = 0)
            => Col(pos + Quaternion.Euler(0,yaw,0) * localCenter,size,yaw,layer);

        struct BoxDef { public Vector3 c,s; public BoxDef(Vector3 c,Vector3 s) { this.c = c; this.s = s; } }

        
        static GameObject Dyn(string kind,Func<Geo> build,Vector3 pos,float yaw,BoxDef[] boxes,float mass,string clip,bool keepUpright,PhysicsMaterial pm)
        {
            var mesh = MeshStore.Shared("Dyn_" + kind, build, out var keys);
            var go = new GameObject(kind) { layer = LProp };
            go.transform.SetParent(_dynamic, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[keys.Length];
            for (int i = 0; i < keys.Length; i++) mats[i] = Mats.Get(keys[i]);
            mr.sharedMaterials = mats;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            foreach (var b in boxes) { var bc = go.AddComponent<BoxCollider>(); bc.center = b.c; bc.size = b.s; bc.sharedMaterial = pm; }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.linearDamping = keepUpright ? 2.2f : 0.4f;
            rb.angularDamping = keepUpright ? 3f : 0.6f;
            if (keepUpright) rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            var pp = go.AddComponent<PhysicsProp>(); pp.ImpactClip = clip;
            go.AddComponent<RewindableObject>();
            DynamicProps.Add(go);
            return go;
        }

        static PhysicsMaterial _pmSlick, _pmRough;
        static PhysicsMaterial Pm(bool slick)
        {
            if (_pmSlick == null)
            {
                Directory.CreateDirectory("Assets/Generated");
                _pmSlick = new PhysicsMaterial("Slick") { dynamicFriction=0.25f, staticFriction=0.3f, bounciness=0.05f, frictionCombine=PhysicsMaterialCombine.Minimum };
                _pmRough=new PhysicsMaterial("Rough") { dynamicFriction = 0.7f,staticFriction = 0.8f,bounciness = 0.1f };
                AssetDatabase.DeleteAsset("Assets/Generated/Slick.physicMaterial");
                AssetDatabase.DeleteAsset("Assets/Generated/Rough.physicMaterial");
                AssetDatabase.CreateAsset(_pmSlick, "Assets/Generated/Slick.physicMaterial");
                AssetDatabase.CreateAsset(_pmRough, "Assets/Generated/Rough.physicMaterial");
            }
            return slick ? _pmSlick : _pmRough;
        }

        static GameObject Chair(Vector3 pos,float yaw,string fabric = "fabricChair")
            => Dyn("Chair_" + fabric, () => { var g = new Geo(); Props.OfficeChair(g, fabric); return g; }, pos, yaw,
                new[] { new BoxDef(V(0, 0.06f, 0), V(0.56f, 0.1f, 0.56f)), new BoxDef(V(0, 0.44f, 0), V(0.5f, 0.12f, 0.48f)), new BoxDef(V(0, 0.78f, -0.25f), V(0.44f, 0.52f, 0.1f)) },
                9f, "thud", true, Pm(true));

        static GameObject SideChair(Vector3 pos, float yaw)
            => Dyn("SideChair",() => { var g = new Geo(); Props.SideChair(g); return g; },pos,yaw,
                new[] { new BoxDef(V(0,0.24f,0),V(0.42f,0.46f,0.42f)),new BoxDef(V(0,0.68f,-0.21f),V(0.4f,0.36f,0.05f)) },
                5f, "clatter", false, Pm(false));

        static GameObject Bin(Vector3 pos)
            => Dyn("Bin", () => { var g = new Geo(); Props.TrashBin(g); return g; }, pos, R(0, 360),
                new[] { new BoxDef(V(0, 0.17f, 0), V(0.26f, 0.34f, 0.26f)) }, 1.4f, "clatter", false, Pm(false));

        static GameObject Box(Vector3 pos, float yaw, float w, float h, float d)
            => Dyn($"Box_{w:0.00}_{h:0.00}_{d:0.00}",() => { var g = new Geo(); Props.CardboardBox(g,w,h,d); return g; },pos,yaw,
                new[] { new BoxDef(V(0,h / 2,0),V(w,h,d)) },3f, "thud", false, Pm(false));

        
        public static TextMeshPro Label(string text, Vector3 pos, Vector3 normal, float fontSize, Color color, float width = 2f, float height = 0.5f,
            FontStyles style = FontStyles.Bold, TextAlignmentOptions align = TextAlignmentOptions.Center, float spacing = 0f)
        {
            var go = new GameObject("Label_" + (text.Length > 14 ? text.Substring(0, 14) : text));
            go.transform.SetParent(_labels, false);
            go.transform.position = pos + normal.normalized * 0.004f;
            go.transform.rotation = Quaternion.LookRotation(-normal.normalized, Mathf.Abs(normal.y) > 0.9f ? Vector3.forward : Vector3.up);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text; tmp.fontSize = fontSize; tmp.color = color; tmp.fontStyle = style;
            tmp.alignment = align;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.characterSpacing = spacing;
            tmp.rectTransform.sizeDelta = new Vector2(width, height);
            var mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            return tmp;
        }

        
        static OfficeLight Fixture(Vector3 pos, bool lit, string group, bool flicker = false, float intensity = 26f, float range = 8f, Color? color = null, float yaw = 0f)
        {
            Put(pos, yaw, g => Props.TrofferFrame(g));
            var mesh = MeshStore.Shared("TrofferDiffuser", () => { var g=new Geo(); Props.TrofferDiffuser(g, "fixtureOn"); return g; },out _);
            var go = new GameObject("Fixture_" + group);
            go.transform.SetParent(_lights, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Get(lit ? "fixtureOn" : "fixtureOff");
            mr.shadowCastingMode=ShadowCastingMode.Off;
            var ol=go.AddComponent<OfficeLight>();
            ol.Panel=mr;
            ol.OnMat=Mats.Get("fixtureOn");
            ol.OffMat = Mats.Get("fixtureOff");
            ol.EmergencyMat = Mats.Get("fixtureRed");
            ol.Group = group; ol.Flickers = flicker; ol.FlickerEvery = 5f;
            if (lit)
            {
                var lgo = new GameObject("Lamp");
                lgo.transform.SetParent(go.transform, false);

lgo.transform.localPosition=V(0, -0.06f, 0);
                lgo.transform.localRotation=Quaternion.Euler(90f, 0f, 0f);
                var l=lgo.AddComponent<Light>();
                l.type=LightType.Spot; l.spotAngle=120f; l.innerSpotAngle=70f;
                l.range=range; l.intensity=intensity;
                l.color=color ?? new Color(0.86f, 0.93f, 1f);
                
                l.shadows=LightShadows.Soft;
                l.shadowBias=0.025f;
                l.shadowNormalBias=0.2f;
                ol.Lamp=l;
                
                var fill=new GameObject("CeilingFill");
                fill.transform.SetParent(go.transform,false);
                fill.transform.localPosition = V(0,-0.9f,0);
                var fl = fill.AddComponent<Light>();
                fl.type = LightType.Point;
                fl.range = 1.3f;
                fl.intensity = 0.25f;
                fl.color = l.color;
                fl.shadows = LightShadows.None;
                ol.Fill = fl;
            }
            else { ol.OnMat = Mats.Get("fixtureOff"); ol.EmergencyMat = Mats.Get("fixtureOff"); } 
            return ol;
        }

        
        static OfficeLight Glow(string name, Vector3 pos, Color color, float intensity, float range, string group, bool backup, bool accent, LightType type = LightType.Point, Vector3? dir = null, float spot = 80f)
        {
            var go = new GameObject("Glow_" + name);
            go.transform.SetParent(_lights, false);
            go.transform.position=pos;
            if (dir.HasValue) go.transform.rotation=Quaternion.LookRotation(dir.Value);
            var l=go.AddComponent<Light>();
            l.type=type;
            l.color=color;
            l.intensity=intensity;
            l.range=range;
            l.shadows=LightShadows.None;
            if (type == LightType.Spot) { l.spotAngle=spot; l.innerSpotAngle=spot * 0.55f; }
            var ol=go.AddComponent<OfficeLight>();
            ol.Lamp=l; ol.Group=group; ol.OnBackupPower=backup; ol.Accent=accent;
            return ol;
        }

static void Environment()
        {
            RenderSettings.skybox=null;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            
            RenderSettings.ambientSkyColor=new Color(0.22f, 0.25f, 0.32f);
            RenderSettings.ambientEquatorColor=new Color(0.16f, 0.17f, 0.19f);
            RenderSettings.ambientGroundColor=new Color(0.1f, 0.09f, 0.08f);
            RenderSettings.fog=true;
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=0.012f;
            RenderSettings.fogColor=new Color(0.03f, 0.04f, 0.055f);
            
            var moon=new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.14f;
            moon.color = new Color(0.55f,0.66f,0.9f);
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(28f,20f,0f);
            RenderSettings.sun = moon;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 0.35f;
            LightmapSettings.lightmaps = new LightmapData[0];
        }

        struct Opening { public float a,b,y0,y1; public Opening(float a,float b,float y0,float y1) { this.a = a; this.b = b; this.y0 = y0; this.y1 = y1; } }

        
        static void Wall(bool alongZ,float fixedCoord,float from,float to,string mat,Opening[] openings,bool solidCollider = false,float thick = WallT)
        {
            var list = new List<Opening>(openings ?? new Opening[0]);
            list.Sort((p,q) => p.a.CompareTo(q.a));
            Action<float,float,float,float,bool> seg = (s0,s1,y0,y1,collide) =>
            {
                if (s1 - s0 < 0.01f || y1 - y0 < 0.01f) return;
                Vector3 c = alongZ ? V(fixedCoord,(y0 + y1) / 2,(s0 + s1) / 2) : V((s0 + s1) / 2,(y0 + y1) / 2,fixedCoord);
                Vector3 size = alongZ ? V(thick,y1 - y0,s1 - s0) : V(s1 - s0,y1 - y0,thick);
                Z(c).Box(mat,c,size,0.004f);
                if (collide) Col(c,size);
            };
            float cur = from;
            foreach (var o in list)
            {
                seg(cur,o.a,0f,H,!solidCollider);
                seg(o.a,o.b,0f,o.y0,!solidCollider && o.y0 > 0.3f);
                seg(o.a,o.b,o.y1,H,!solidCollider && o.y1 < 2.0f);
                cur = o.b;
            }
            seg(cur,to,0f,H,!solidCollider);
            if (solidCollider)
            {
                Vector3 c = alongZ ? V(fixedCoord,H / 2,(from + to) / 2) : V((from + to) / 2,H / 2,fixedCoord);
                Col(c,alongZ ? V(thick,H,to - from) : V(to - from,H,thick));
            }
            
            cur = from;
            Action<float,float> skirt = (s0,s1) =>
            {
                if (s1 - s0 < 0.05f) return;
                foreach (float side in new[] { -1f,1f })
                {
                    Vector3 c = alongZ ? V(fixedCoord + side * (thick / 2 + 0.008f),0.05f,(s0 + s1) / 2) : V((s0 + s1) / 2,0.05f,fixedCoord + side * (thick / 2 + 0.008f));
                    Z(c).Box("baseboard", c, alongZ ? V(0.016f, 0.1f, s1 - s0) : V(s1 - s0, 0.1f, 0.016f), 0.004f);
                }
            };
            foreach (var o in list) { skirt(cur, o.a); if (o.y0 > 0.3f) skirt(o.a, o.b); cur = o.b; }
            skirt(cur, to);
        }

        static void FloorCeil(float x0, float x1, float z0, float z1, string floorMat)
        {
            
            float step = 6.7f;
            for (float x = x0; x < x1 - 0.01f; x += step)
                for (float z = z0; z < z1 - 0.01f; z += step)
                {
                    float xa = x, xb = Mathf.Min(x1, x + step), za = z, zb = Mathf.Min(z1, z + step);
                    Vector3 c = V((xa + xb) / 2, 0, (za + zb) / 2);
                    var g = Z(c + V(0, 0, 0.01f));
                    g.Quad(floorMat, c, V((xb - xa) / 2, 0, 0), V(0, 0, -(zb - za) / 2), false, true);          
                    g.Quad("ceiling", c + V(0, H, 0), V((xb - xa) / 2, 0, 0), V(0, 0, (zb - za) / 2), false, true); 
                }
        }

        static void Shell()
        {
            
            FloorCeil(XMin, XMax, ZMin, CorrZ0, "carpet");
            FloorCeil(XMin, XMax, CorrZ0, CorrZ1, "carpet");
            string[] roomFloor = { "carpet", "carpet", "carpet", "linoleum", "concrete", "linoleum" };
            for (int i = 0; i < RoomNames.Length; i++) FloorCeil(RoomX[i], RoomX[i + 1], CorrZ1, ZMax, roomFloor[i]);
            Col(V(0, -0.25f, 0), V(XMax - XMin + 8f, 0.5f, ZMax - ZMin + 4f));
            Col(V(0, H + 0.25f, 0), V(XMax - XMin + 8f, 0.5f, ZMax - ZMin + 4f), 0f, LNoNav);

            
            var southWin = new List<Opening>();
            for (float x = -17.5f; x <= 17.6f; x += 5f) southWin.Add(new Opening(x - 1.5f, x + 1.5f, 0.9f, 2.3f));
            Wall(false, ZMin, XMin, XMax, "wall", southWin.ToArray(), true);
            foreach (var o in southWin) Put(V((o.a + o.b) / 2, 1.6f, ZMin), 0f, g => Props.Window(g, 3f, 1.4f, WallT, 0.45f));
            var sideWin=new[] { new Opening(-11.5f, -8.5f, 0.9f, 2.3f), new Opening(-4.5f, -1.5f, 0.9f, 2.3f), new Opening(1.2f, 4.2f, 0.9f, 2.3f) };
            Wall(true, XMin, ZMin, CorrZ0, "wall",sideWin,true);
            Wall(true,XMax,ZMin,CorrZ0, "wall", sideWin, true);
            foreach (var o in sideWin)
            {
                Put(V(XMin, 1.6f, (o.a + o.b) / 2), 90f, g => Props.Window(g, 3f, 1.4f, WallT, 0.3f));
                Put(V(XMax, 1.6f, (o.a + o.b) / 2), -90f, g => Props.Window(g, 3f, 1.4f, WallT, 0.6f));
            }
            Wall(true, XMin, CorrZ0, ZMax, "wall", null, true);
            Wall(false, ZMax, XMin, XMax, "wall", null, true);

            
            var sky=Z(V(0, 0, ZMin + 1f));
            sky.Quad("skyline",V(0,2.2f,ZMin - 7f),V(30f,0,0),V(0,7f,0));
            Z(V(XMin + 1f,0,-8f)).Quad("skyline", V(XMin - 7f, 2.2f, -4.5f), V(0, 0, -14f), V(0, 7f, 0));
            Z(V(XMax - 1f, 0, -8f)).Quad("skyline", V(XMax + 7f, 2.2f, -4.5f), V(0, 0, 14f), V(0, 7f, 0));
            Z(V(0, 0, ZMin + 1f)).Quad("black", V(0, -1.4f, ZMin - 3.5f), V(30f, 0, 0), V(0, 0, -3.6f));
            Z(V(XMin + 1f, 0, -8f)).Quad("black",V(XMin - 3.5f,-1.4f,-4.5f),V(3.6f,0,0),V(0,0,-14f));
            Z(V(XMax - 1f,0,-8f)).Quad("black", V(XMax + 3.5f, -1.4f, -4.5f), V(3.6f, 0, 0), V(0, 0, -14f));

            
            var farmOpen = new List<Opening>();
            foreach (float cx in FarmOpenings) farmOpen.Add(new Opening(cx - 1.5f, cx + 1.5f, 0f, 2.45f));
            Wall(false, CorrZ0, XMin, XMax, "wallAccent", farmOpen.ToArray());

            
            var north = new List<Opening>();
            foreach (float dx in DoorXs) north.Add(new Opening(dx - DoorW / 2, dx + DoorW / 2, 0f, DoorH));
            north.Add(new Opening(LobbyOpenX0, LobbyOpenX1, 0f, 2.5f));
            Wall(false, CorrZ1, XMin, XMax, "wall", north.ToArray());
            
            Wall(true, XMax, CorrZ0, ZMax, "wall",new[] { new Opening(ElevZ[0] - 0.6f,ElevZ[0] + 0.6f,0f,2.15f),new Opening(ElevZ[1] - 0.6f,ElevZ[1] + 0.6f,0f,2.15f) });

            
            for (int i = 1; i < RoomX.Length - 1; i++) Wall(true,RoomX[i],CorrZ1,ZMax,i == 5 ? "wallAccent" : "wall", null);

            
            foreach (var p in Columns)
            {
                Z(p).Box("wallDark", p + V(0, H / 2, 0), V(0.55f, H, 0.55f), 0.02f);
                Z(p).Box("baseboard",p + V(0,0.06f,0),V(0.59f,0.12f,0.59f),0.006f);
                Col(p + V(0,H / 2,0),V(0.55f,H,0.55f));
            }
        }

        public static readonly float[] DoorXs = { -17.2f,-11.7f,-5.7f,0.8f,6.8f };  
        public static readonly float[] ElevZ = { 9.9f,12.3f };
        public static readonly Vector3[] Columns = { new Vector3(-6.5f,0,0.6f),new Vector3(6.5f,0,0.6f),new Vector3(-6.5f,0,-6.4f),new Vector3(6.5f,0,-6.4f) };

static void BakeNavMesh(BuildRefs refs)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            var bounds = new Bounds(V(0,1.2f,0),V(XMax - XMin + 2f,3.2f,ZMax - ZMin + 2f));
            UnityEngine.AI.NavMeshBuilder.CollectSources(bounds,1 << 0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.3f;
            settings.agentHeight = 1.9f;
            settings.agentClimb = 0.3f;
            settings.agentSlope = 40f;
            settings.minRegionArea = 4f;
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.075f;
            var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if (data == null) { Log("NAVMESH FAILED"); return; }
            data.name = "OfficeNavMesh";
            AssetDatabase.DeleteAsset("Assets/Generated/OfficeNavMesh.asset");
            AssetDatabase.CreateAsset(data, "Assets/Generated/OfficeNavMesh.asset");
            var loader = refs.Systems.AddComponent<NavMeshLoader>();
            loader.Data = data;
            loader.EnableAfterLoad = new[] { refs.MonsterAgent };

            
            var inst = NavMesh.AddNavMeshData(data);
            var tri = NavMesh.CalculateTriangulation();
            Log($"navmesh sources={sources.Count} tris={tri.indices.Length / 3}");
            Vector3 origin = refs.Player.transform.position;
            Action<string, Vector3> check = (label, p) =>
            {
                var path = new NavMeshPath();
                bool a = NavMesh.SamplePosition(origin, out var ha, 1.5f, NavMesh.AllAreas), b = NavMesh.SamplePosition(p, out var hb, 2.5f, NavMesh.AllAreas);
                bool ok = a && b && NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
                if (!ok) Log($"NAV UNREACHABLE {label} at {p.x:0.0},{p.z:0.0} sampleA={a} sampleB={b} status={path.status}");
            };
            foreach (var w in refs.Waypoints) check("waypoint " + w.name, w.position);
            foreach (var o in refs.Objectives) {
                
                
                bool reachable=false;
                for(int n=0;n<24 && !reachable;n++) {
                    float angle=n*Mathf.PI/12f;
                    var probe=new Vector3(o.MarkerPosition.x+Mathf.Cos(angle)*1.25f,0.05f,o.MarkerPosition.z+Mathf.Sin(angle)*1.25f);
                    if(!NavMesh.SamplePosition(probe,out var target,.5f,NavMesh.AllAreas) || target.position.y>.3f)continue;
                    if(!NavMesh.SamplePosition(origin,out var start,1.5f,NavMesh.AllAreas))continue;
                    var route=new NavMeshPath();
                    reachable=NavMesh.CalculatePath(start.position,target.position,NavMesh.AllAreas,route) && route.status==NavMeshPathStatus.PathComplete;
                }
                Log("objective approach "+o.Id+" reachable="+reachable);
                if (!reachable)check("objective "+o.Id,o.MarkerPosition);
            }
            check("exit", refs.Exit.position);
            check("monsterStart",refs.MonsterAgent.transform.position);
            inst.Remove();
        }

static void PostFx(BuildRefs refs)
        {
            const string path = "Assets/Generated/NightShiftVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            var tone = Add<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            var bloom = Add<Bloom>();
            bloom.intensity.Override(0.3f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.6f);
            bloom.highQualityFiltering.Override(true);
            bloom.tint.Override(new Color(0.9f, 0.95f, 1f));
            var vig = Add<Vignette>();
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);
            var col = Add<ColorAdjustments>();
            col.postExposure.Override(0.3f);
            col.contrast.Override(8f);
            col.saturation.Override(-6f);
            var wb = Add<WhiteBalance>(); wb.temperature.Override(-9f); wb.tint.Override(4f);
            var ca = Add<ChromaticAberration>(); ca.intensity.Override(0f);
            var lens = Add<LensDistortion>(); lens.intensity.Override(0f);
            var grain = Add<FilmGrain>();
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0f);
            grain.response.Override(0.85f);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostFX");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 1f; vol.sharedProfile = profile;
            go.AddComponent<PostFXDriver>();
        }

static void FloatCheck()
        {
            Physics.SyncTransforms();
            int bad = 0;
            foreach (var go in DynamicProps)
            {
                var mr = go.GetComponent<MeshRenderer>();
                if (mr == null) continue;
                Bounds b = mr.bounds;
                Vector3 from = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
                bool hit = Physics.Raycast(from, Vector3.down, out var h, 0.5f, (1 << 0) | (1 << LProp), QueryTriggerInteraction.Ignore);
                float gap = hit ? h.distance - 0.05f : 99f;
                if (gap > 0.03f || gap < -0.03f)
                { bad++; Log($"FLOAT {go.name} at {go.transform.position.x:0.0},{go.transform.position.z:0.0} gap={gap:0.000}"); }
                
                foreach (var bc in go.GetComponents<BoxCollider>())
                {
                    Vector3 c=go.transform.TransformPoint(bc.center);
                    if (Physics.CheckBox(c, bc.size * 0.5f * 0.9f, go.transform.rotation, 1 << 0, QueryTriggerInteraction.Ignore) && bc.center.y > 0.2f)
                    { bad++; Log($"OVERLAP {go.name} at {go.transform.position.x:0.0},{go.transform.position.z:0.0}"); break; }
                }
            }
            Log($"floatcheck dynamic={DynamicProps.Count} problems={bad}");
        }

public static void Shots()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string dir = "qa/shots";
            foreach (var a in Environment_GetArgs("-nsShots")) dir=a;
            Directory.CreateDirectory(dir);
            var poses=new (string name, Vector3 pos, Vector3 look, float fov)[]
            {
                ("01_lobby",V(11.5f,1.65f,9.2f),V(19.5f,1.3f,11.4f),70f),
                ("02_corridor_west", V(17f, 1.6f, 6.6f), V(-19f, 1.3f, 6.5f), 62f),
                ("03_farm_overview", V(-18.5f, 1.68f, 3.9f), V(2f, 1.0f, -6f), 72f),
                ("04_cubicle", V(0.2f, 1.55f, -9.2f), V(0f, 0.95f, -6.6f), 68f),
                ("05_mid_aisle",V(-9.5f,1.65f,-2.9f),V(12f,1.1f,-2.9f),70f),
                ("06_south_windows", V(4f, 1.65f, -9.5f), V(-8f, 1.5f, -14f), 72f),
                ("07_break_room", V(-2.0f, 1.65f, 8.6f), V(2.6f, 1.1f, 13.5f), 74f),
                ("08_meeting2", V(-8.4f, 1.65f, 8.6f), V(-4.5f, 1.0f, 12.5f), 74f),
                ("09_manager",V(-15.2f,1.65f,8.6f),V(-18.5f,1.0f,13f),74f),
                ("10_storage", V(4.6f, 1.65f, 8.6f), V(8.5f, 1.2f, 13.2f), 74f),
                ("11_copier", V(15.5f, 1.6f, 1.5f), V(18.3f, 0.9f, 4.4f), 66f),
                ("12_monster", V(0f, 0f, 0f), V(0f, 0f, 0f), 50f),
                ("13_top",V(0f,2.7f,-4f),V(0.01f,0f,-4.2f),100f),
            };
            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var monster = UnityEngine.Object.FindFirstObjectByType<MonsterAI>();
            foreach (var p in poses)
            {
                Vector3 pos = p.pos, look = p.look;
                if (p.name == "12_monster" && monster != null)
                {
                    look = monster.transform.position + Vector3.up * 1.35f;
                    pos = monster.transform.position + monster.transform.forward * 2.3f + monster.transform.right * 0.5f + Vector3.up * 1.5f;
                }
                camGo.transform.position = pos;
                camGo.transform.rotation = Quaternion.LookRotation(look - pos);
                cam.fieldOfView = p.fov;
                cam.targetTexture = rt;
                for (int i = 0; i < 2; i++) cam.Render(); 
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes($"{dir}/{p.name}.png", tex.EncodeToPNG());
                Debug.Log("NSSHOT " + p.name);
            }
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        static IEnumerable<string> Environment_GetArgs(string flag)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == flag) yield return a[i + 1];
        }
    }

    
    public class BuildRefs
    {
        public GameObject Systems,Player;
        public Camera PlayerCamera,MenuCamera;
        public NavMeshAgent MonsterAgent;
        public Transform Exit;
        public List<Transform> Waypoints = new List<Transform>();
        public List<Objective> Objectives = new List<Objective>();
        public UIManager Ui;
        public GameFlowUI Flow;
    }
}
#endif
