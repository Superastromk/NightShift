using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace NightShift.EditorTools
{
    public static class BuildNightShift
    {
        
        const string ScenePath  = "Assets/Scenes/OfficeFloor.unity";
        const float  WallH     =3f;
        static readonly List<string> _errors    =new List<string>();
        static readonly List<string> _mismatches=new List<string>();
        static readonly List<string> _wired     =new List<string>();
        static readonly Dictionary<string, Material> _mats=new Dictionary<string, Material>();

        static int _walls, _props, _lights, _colliders, _cubicles, _rooms, _doors;
        static int _navTris=-1;
        static Light _breakRoomLight, _breakRoomLight2;
        static GameObject _monsterGO, _playerGO, _systemsGO;

        
        const float XMin=-20f, XMax=20f, ZMin=-14f, ZMax=14f;
        const float CorridorZ0=5f, CorridorZ1=8f;

        
        public static string Build()
        {
            _errors.Clear(); _mismatches.Clear(); _wired.Clear(); _mats.Clear();
            _walls=_props=_lights=_colliders=_cubicles=_rooms=_doors=0;
            _navTris=-1; _breakRoomLight=_breakRoomLight2=null;

            Section("FoldersAndMaterials",FoldersAndMaterials);
            Section("Scene", () =>
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            });
            Section("RenderSettings", RenderSettingsSetup);
            Section("Shell", Shell);                       
            Section("NorthRoomsAndCorridor",NorthRoomsAndCorridor);
            Section("CubicleFarm", CubicleFarm);
            Section("PrinterAlcove", PrinterAlcove);
            Section("BreakRoomProps", BreakRoomProps);
            Section("StorageProps",StorageProps);
            Section("ElevatorLobby", ElevatorLobby);
            Section("CorridorProps", CorridorProps);       
            Section("Lights", Lights);
            Section("Player",PlayerRig);
            Section("Monster", MonsterRig);
            Section("GameSystems", GameSystemsRig);
            Section("Objectives", ObjectivesRig);
            Section("Scares",ScaresRig);
            Section("KillZones", KillZonesRig);
            Section("Rewindables", RewindablesRig);
            Section("NavMesh", NavMeshRig);
            Section("PostFX",PostFXRig);
            Section("Save", () =>
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                    _errors.Add("SaveScene returned false");
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            });

            var sb = new StringBuilder();
            sb.AppendLine("=== BuildNightShift summary ===");
            sb.AppendLine($"scene={ScenePath} saved, added to EditorBuildSettings");
            sb.AppendLine($"counts: walls={_walls} props={_props} cubicles={_cubicles} " +
                          $"rooms={_rooms} doors={_doors} colliders={_colliders} realtimeLights={_lights} (<=14: {(_lights <= 14)})");
            sb.AppendLine($"navmeshTris={_navTris}");
            sb.AppendLine("wired: " + (_wired.Count > 0 ? string.Join(", ", _wired) : "(none)"));
            if (_mismatches.Count > 0) sb.AppendLine("CONTRACT MISMATCHES: " + string.Join(" | ",_mismatches));
            if (_errors.Count > 0) sb.AppendLine("ERRORS: " + string.Join(" | ", _errors));
            sb.Append("Polish gap: no light flicker (runtime flicker script intentionally omitted).");
            return sb.ToString();
        }

        public static string ReportScene()
        {
            try
            {
                var sb=new StringBuilder("=== OfficeFloor hierarchy ===\n");
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    Walk(root.transform,0,sb);
                return sb.ToString();
            }
            catch (Exception e) { return "ReportScene failed: " + e.Message; }
        }

        static void Walk(Transform t, int depth, StringBuilder sb)
        {
            sb.AppendLine(new string(' ', depth * 2) + t.name +
                          (t.childCount > 0 ? $"  ({t.childCount} children)" : ""));
            if (depth >= 2) return;
            foreach (Transform c in t) Walk(c,depth + 1,sb);
        }

        static void Section(string name,Action act)
        {
            try { act(); }
            catch (Exception e) { _errors.Add($"{name}: {e.GetType().Name}: {e.Message}"); }
        }

static void FoldersAndMaterials()
        {
            EnsureFolder("Assets/Materials");
            EnsureFolder("Assets/Scenes");

            var carpetNoise=NoiseTexture("carpetNoise",0.85f,1.15f);
            var deskNoise   = NoiseTexture("deskNoise", 0.90f, 1.10f);

            Mat("carpetFloor",    new Color32(0x2E, 0x34, 0x40, 255), 0.00f, 0.15f, tex: carpetNoise);
            Mat("wallPaint",      new Color32(0xD8, 0xD3, 0xC8, 255), 0.00f, 0.25f);
            Mat("cubicleFabric",  new Color32(0x4C,0x4F,0x56,255),0.00f,0.40f);
            Mat("deskLaminate",   new Color32(0xC8, 0xA8, 0x7C, 255), 0.00f, 0.35f, tex: deskNoise);
            Mat("ceilingMat",     new Color32(0xEC, 0xEA, 0xE4, 255), 0.00f, 0.20f);
            Mat("accentRed",      new Color32(0xFF, 0x2A, 0x1A, 255), 0.10f, 0.50f, emission: new Color32(0xFF, 0x2A, 0x1A, 255), emitInt: 2.5f);
            Mat("exitGreen",      new Color32(0x39,0xFF,0x6A,255),0.00f,0.50f,emission: new Color32(0x39,0xFF,0x6A,255),emitInt: 2.0f);
            Mat("metalDark",      new Color32(0x3A, 0x3D, 0x42, 255), 0.60f, 0.55f);
            Mat("screenGlow",     new Color32(0x88, 0xC0, 0xD0, 255), 0.00f, 0.60f, emission: new Color32(0x88, 0xC0, 0xD0, 255), emitInt: 1.6f);
            Mat("fixtureEmissive",new Color32(0xFF, 0xF4, 0xE0, 255), 0.00f, 0.50f, emission: new Color32(0xFF, 0xF4, 0xE0, 255), emitInt: 1.8f);
            Mat("plantGreen",     new Color32(0x3E,0x6B,0x45,255),0.00f,0.20f);
            Mat("potClay",        new Color32(0x8A, 0x5A, 0x3C, 255), 0.00f, 0.30f);
            Mat("folderManila",   new Color32(0xD9, 0xC0, 0x7A, 255), 0.00f, 0.30f);
            Mat("paperWhite",     new Color32(0xE8, 0xE6, 0xE0, 255), 0.00f, 0.20f);
            Mat("chairDark",      new Color32(0x2A,0x2C,0x31,255),0.10f,0.40f);
            Mat("stalkerBeige",   new Color32(0xC8, 0xB4, 0x9A, 255), 0.00f, 0.30f);
            Mat("monsterBlack",   new Color32(0x0A, 0x0A, 0x0C, 255), 0.20f, 0.60f);
            Mat("eyePale",        new Color32(0xD8, 0xE4, 0xE8, 255), 0.00f, 0.50f, emission: new Color32(0xD8, 0xE4, 0xE8, 255), emitInt: 1.4f);
            Mat("coffeeBlack",    new Color32(0x1A,0x12,0x0C,255),0.20f,0.60f);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Mat(string name, Color color, float metallic, float smooth,
                        Color? emission = null, float emitInt = 1f, Texture2D tex = null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Materials/{name}.mat");
            if (existing != null) { _mats[name]=existing; return; }
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP Lit shader not found — is URP installed?");
            var m = new Material(shader) { name = name, color = color };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (tex != null) { m.mainTexture=tex; m.SetTextureScale("_BaseMap",new Vector2(6,6)); }
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value * emitInt);
            }
            AssetDatabase.CreateAsset(m, $"Assets/Materials/{name}.mat");
            _mats[name]=m;
        }

        static Texture2D NoiseTexture(string name, float lo, float hi, int size=64)
        {
            var path=$"Assets/Materials/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            var rng = new System.Random(name.GetHashCode());
            const int g = 8; 
            var lattice = new float[g + 1,g + 1];
            for (int y = 0; y <= g; y++) for (int x = 0; x <= g; x++)
                lattice[x,y] = lo + (float)rng.NextDouble() * (hi - lo);

            var tex = new Texture2D(size,size,TextureFormat.RGBA32,false);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float fx = (float)x / size * g,fy = (float)y / size * g;
                int x0 = (int)fx,y0 = (int)fy;
                float tx = Smooth(fx - x0),ty = Smooth(fy - y0);
                float v = Mathf.Lerp(
                    Mathf.Lerp(lattice[x0,y0],lattice[x0 + 1,y0],tx),
                    Mathf.Lerp(lattice[x0,y0 + 1],lattice[x0 + 1,y0 + 1],tx),ty);
                tex.SetPixel(x,y,new Color(v,v,v,1f));
            }
            tex.Apply();
            File.WriteAllBytes(path,tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            imported.wrapMode = TextureWrapMode.Repeat;
            imported.filterMode = FilterMode.Bilinear;
            EditorUtility.SetDirty(imported);
            AssetDatabase.SaveAssets();
            return imported;
        }

        static float Smooth(float t) => t * t * (3f - 2f * t);

static Mesh _unitCube;
        static Mesh UnitCube()
        {
            if (_unitCube != null) return _unitCube;
            var v = new Vector3[8]
            {
                new(-.5f,-.5f,-.5f),new(.5f,-.5f,-.5f),new(.5f,.5f,-.5f),new(-.5f,.5f,-.5f),
                new(-.5f,-.5f,.5f),new(.5f,-.5f,.5f),new(.5f,.5f,.5f),new(-.5f,.5f,.5f)
            };
            
            int[][] faces =
            {
                new[]{4,5,6,7}, 
                new[]{1,0,3,2}, 
                new[]{5,1,2,6}, 
                new[]{0,4,7,3}, 
                new[]{7,6,2,3}, 
                new[]{0,1,5,4}, 
            };
            var verts = new List<Vector3>(); var norms = new List<Vector3>();
            var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int f = 0; f < 6; f++)
            {
                var q = faces[f];
                var n = Vector3.Cross(v[q[1]] - v[q[0]],v[q[2]] - v[q[0]]).normalized;
                int b = verts.Count;
                verts.Add(v[q[0]]); verts.Add(v[q[1]]); verts.Add(v[q[2]]); verts.Add(v[q[3]]);
                for (int i = 0; i < 4; i++) norms.Add(n);
                uvs.Add(new Vector2(0,0)); uvs.Add(new Vector2(1,0));
                uvs.Add(new Vector2(1,1)); uvs.Add(new Vector2(0,1));
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            var m = new Mesh { name = "UnitCube" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return _unitCube = m;
        }

        
        static GameObject Box(string name, Vector3 center, Vector3 size, string mat,
                              Transform parent = null, bool navStatic = true, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.position = center;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = UnitCube();
            go.AddComponent<MeshRenderer>().sharedMaterial = _mats[mat];
            if (collider)
            {
                go.AddComponent<BoxCollider>();
                _colliders++;
            }
            if (navStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
            if (parent != null) go.transform.SetParent(parent, true); 
            _props++;
            return go;
        }

        static GameObject Wall(string name, Vector3 center, Vector3 size, Transform parent = null)
        {
            _walls++;
            return Box(name, center, size, "wallPaint", parent);
        }

        
        static GameObject CombineBoxes(string name, string mat, Transform parent,
                                       bool navStatic, params Vector3[] parts)
        {
            var combine = new CombineInstance[parts.Length / 2];
            for (int i = 0; i < parts.Length; i += 2)
                combine[i / 2] = new CombineInstance
                {
                    mesh = UnitCube(),
                    transform = Matrix4x4.TRS(parts[i], Quaternion.identity, parts[i + 1])
                };
            var mesh = new Mesh { name = name + "_mesh" };
            mesh.CombineMeshes(combine, true, false);
            var go=new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=_mats[mat];
            go.AddComponent<MeshCollider>().sharedMesh=mesh;
            _colliders++;
            if (navStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
            if (parent != null) go.transform.SetParent(parent, true);
            _props++;
            return go;
        }

        static GameObject Empty(string name, Vector3 pos, Transform parent=null)
        {
            var go=new GameObject(name);
            go.transform.position=pos; 
            if (parent != null) go.transform.SetParent(parent, true);
            return go;
        }

static void RenderSettingsSetup()
        {
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=new Color32(0x1B, 0x20, 0x30, 255);
            RenderSettings.skybox=null;
            RenderSettings.fog=true;
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogDensity=0.045f;
            RenderSettings.fogColor=new Color32(0x14, 0x18, 0x1F, 255);
        }

        static void Shell()
        {
            var root=Empty("Environment",Vector3.zero);
            Box("Floor", new Vector3(0, -0.1f, 0),
                new Vector3(XMax - XMin, 0.2f, ZMax - ZMin), "carpetFloor", root.transform);
            Box("Ceiling", new Vector3(0, WallH + 0.1f, 0),
                new Vector3(XMax - XMin, 0.2f, ZMax - ZMin), "ceilingMat",root.transform);
            Wall("Wall_North", new Vector3(0, WallH / 2, ZMax), new Vector3(XMax - XMin + 0.6f, WallH, 0.3f), root.transform);
            Wall("Wall_South", new Vector3(0, WallH / 2, ZMin), new Vector3(XMax - XMin + 0.6f, WallH, 0.3f), root.transform);
            Wall("Wall_West",  new Vector3(XMin, WallH / 2, 0), new Vector3(0.3f, WallH, ZMax - ZMin + 0.6f), root.transform);
            Wall("Wall_East",  new Vector3(XMax,WallH / 2,0),new Vector3(0.3f,WallH,ZMax - ZMin + 0.6f),root.transform);
        }

        
        static readonly (string name,float x0,float x1,string door)[] Rooms =
        {
            ("MeetingRoom1", -20f, -16f, "table"),
            ("MeetingRoom2", -16f, -12f, "table"),
            ("MeetingRoom3", -12f,  -8f, "table"),
            ("MeetingRoom4",  -8f,  -4f, "table"),
            ("BreakRoom",     -4f,   0f, "props"),
            ("Storage",        0f,   6f, "shelves"),
            ("ElevatorLobby",  6f,  20f, "elevators"),
        };

        static void NorthRoomsAndCorridor()
        {
            var root = Empty("NorthWing", Vector3.zero);

            
            for (int i=0; i < Rooms.Length - 1; i++)
            {
                float x=Rooms[i].x1;
                Wall($"Partition_{Rooms[i].name}",new Vector3(x,WallH / 2,11f),
                    new Vector3(0.2f,WallH,ZMax - CorridorZ1),root.transform);
            }

            
            var gaps = new List<(float a,float b)>();
            foreach (var r in Rooms)
            {
                float dx = r.x0 + 0.7f; 
                gaps.Add((dx,dx + 1.2f));
            }
            gaps.Add((7.4f,8.6f)); 
            WallWithGaps("CorridorWallNorth", CorridorZ1, XMin, XMax, gaps, root.transform);

            
            WallWithGaps("CorridorWallSouth", CorridorZ0, XMin, XMax,
                new List<(float a, float b)> { (-0.7f, 0.7f), (-11f, -9f) }, root.transform);

            
            foreach (var r in Rooms)
            {
                if (!r.name.StartsWith("MeetingRoom")) continue;
                _rooms++;
                float cx=(r.x0 + r.x1) / 2f;
                var room=Empty(r.name, new Vector3(cx, 0, 11f));
                room.transform.SetParent(root.transform, false);
                Box($"{r.name}_Table",new Vector3(cx,0.72f,11f),new Vector3(2.4f,0.06f,1.1f), "deskLaminate", room.transform);
                for (int i = 0; i < 6; i++)
                {
                    float ang = i * 60f * Mathf.Deg2Rad;
                    Box($"{r.name}_Chair{i}", new Vector3(cx + Mathf.Cos(ang) * 1.7f, 0.45f, 11f + Mathf.Sin(ang) * 1.3f),
                        new Vector3(0.45f, 0.9f, 0.45f), "chairDark", room.transform);
                }
                
                float dx=r.x0 + 0.7f + 0.6f;
                var door=Box($"{r.name}_Door",new Vector3(dx,1.05f,CorridorZ1),
                    new Vector3(1.16f,2.1f,0.08f), "wallPaint", room.transform);
                door.name = r.name + "_Door"; 
                _doors++;
            }

            
            foreach (var (nm, gx) in new[]{ ("BreakRoom", -3.0f), ("Storage",3.0f) })
            {
                var d = Box(nm + "_Door", new Vector3(gx, 1.05f, CorridorZ1),
                    new Vector3(1.16f, 2.1f, 0.08f), "wallPaint", root.transform);
                d.name = nm + "_Door";
                _doors++;
            }
        }

        static void WallWithGaps(string name, float z, float x0, float x1,
                                 List<(float a, float b)> gaps, Transform parent)
        {
            gaps.Sort((p, q) => p.a.CompareTo(q.a));
            float cur=x0;
            int seg=0;
            foreach (var (a, b) in gaps)
            {
                float s=Mathf.Max(cur, x0), e=Mathf.Min(a, x1);
                if (e - s > 0.01f)
                    Wall($"{name}_seg{seg++}",new Vector3((s + e) / 2,WallH / 2,z),
                        new Vector3(e - s,WallH,0.2f),parent);
                cur = Mathf.Max(cur,b);
            }
            if (x1 - cur > 0.01f)
                Wall($"{name}_seg{seg}", new Vector3((cur + x1) / 2, WallH / 2, z),
                    new Vector3(x1 - cur, WallH, 0.2f), parent);
        }

        
        static void CubicleFarm()
        {
            var root = Empty("CubicleFarm", Vector3.zero);
            var cubicles = new (float cx, float cz, bool on)[]
            {
                (-12f,  0f, true ), (-6f,  0f, false), (6f,  0f, false), (12f,  0f, true),
                (-12f, -6f, false), (-6f, -6f, true ), (6f, -6f, true ), (12f, -6f, false),
            };
            foreach (var (cx, cz, on) in cubicles) MakeCubicle(cx, cz, on, root.transform);

            
            var desk = Empty("YourDesk", new Vector3(0, 0, -12.6f));
            desk.transform.SetParent(root.transform, false);
            Box("YourDesk_Slab",new Vector3(0,0.74f,-12.6f),new Vector3(1.8f,0.05f,0.8f), "deskLaminate", desk.transform);
            Box("YourDesk_Modesty", new Vector3(0, 0.37f, -12.95f), new Vector3(1.8f, 0.74f, 0.04f), "deskLaminate", desk.transform);
            Box("YourDesk_Monitor",new Vector3(0.35f,1.05f,-12.85f),new Vector3(0.55f,0.36f,0.04f), "screenGlow", desk.transform);
            Box("YourDesk_Stand", new Vector3(0.35f, 0.86f, -12.85f), new Vector3(0.08f, 0.2f, 0.08f), "metalDark", desk.transform);
            MakeChair(new Vector3(0, 0, -11.9f), desk.transform);
            Box("FolderProp",new Vector3(-0.45f,0.79f,-12.55f),new Vector3(0.32f,0.03f,0.24f), "folderManila", desk.transform);
            Box("YourDesk_Papers", new Vector3(-0.1f, 0.78f, -12.4f), new Vector3(0.3f, 0.015f, 0.21f), "paperWhite", desk.transform);
            Box("YourDesk_Mug",new Vector3(0.65f,0.81f,-12.45f),new Vector3(0.08f,0.1f,0.08f), "accentRed", desk.transform);

            
            MakePlant(new Vector3(3.5f, 0, -2.5f), root.transform, name: "WateringPlant");
        }

        static void MakeCubicle(float cx, float cz, bool monitorOn, Transform parent)
        {
            _cubicles++;
            var u = Empty($"Cubicle_{cx}_{cz}", new Vector3(cx, 0, cz));
            u.transform.SetParent(parent, false);
            
            Box("Part_N",new Vector3(cx,0.75f,cz + 1.5f),new Vector3(3f,1.5f,0.08f), "cubicleFabric", u.transform);
            Box("Part_W", new Vector3(cx - 1.5f, 0.75f, cz), new Vector3(0.08f, 1.5f, 3f), "cubicleFabric", u.transform);
            Box("Part_E",new Vector3(cx + 1.5f,0.75f,cz),new Vector3(0.08f,1.5f,3f), "cubicleFabric", u.transform);
            
            Box("Desk", new Vector3(cx, 0.74f, cz + 1.0f), new Vector3(2.4f, 0.05f, 0.8f), "deskLaminate", u.transform);
            Box("DeskLegs",new Vector3(cx,0.37f,cz + 1.0f),new Vector3(2.2f,0.74f,0.06f), "metalDark", u.transform);
            
            Box("MonitorStand", new Vector3(cx - 0.4f, 0.86f, cz + 1.25f), new Vector3(0.08f, 0.2f, 0.08f), "metalDark", u.transform);
            Box("Monitor",new Vector3(cx - 0.4f,1.05f,cz + 1.25f),new Vector3(0.55f,0.36f,0.04f),
                monitorOn ? "screenGlow" : "coffeeBlack", u.transform);
            MakeChair(new Vector3(cx, 0, cz + 0.1f), u.transform);
            
            Box("Papers1", new Vector3(cx + 0.5f, 0.78f, cz + 0.9f), new Vector3(0.3f, 0.015f, 0.21f), "paperWhite",u.transform);
            Box("Papers2", new Vector3(cx + 0.6f, 0.78f, cz + 1.15f), new Vector3(0.28f, 0.015f, 0.2f), "paperWhite", u.transform);
            Box("Mug", new Vector3(cx + 0.9f, 0.81f, cz + 0.75f), new Vector3(0.08f, 0.1f, 0.08f), "paperWhite",u.transform);
            if (cx == 6 && cz == 0) MakePlant(new Vector3(cx + 1.1f,0,cz + 1.0f),u.transform);
            if (cx == -6 && cz == -6) MakePlant(new Vector3(cx - 1.1f,0,cz + 1.0f),u.transform);
        }

        static void MakeChair(Vector3 pos,Transform parent)
        {
            Box("ChairSeat", pos + new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.06f, 0.5f), "chairDark", parent);
            Box("ChairBack", pos + new Vector3(0, 0.75f, -0.22f), new Vector3(0.5f, 0.65f, 0.06f), "chairDark",parent);
            Box("ChairPost", pos + new Vector3(0, 0.22f, 0), new Vector3(0.08f, 0.44f, 0.08f), "metalDark", parent);
            Box("ChairBase", pos + new Vector3(0, 0.03f, 0), new Vector3(0.5f, 0.05f, 0.5f), "metalDark",parent);
        }

        static void MakePlant(Vector3 pos,Transform parent,string name = "Plant")
        {
            Box(name + "_Pot", pos + new Vector3(0, 0.18f, 0), new Vector3(0.3f, 0.36f, 0.3f), "potClay", parent);
            Box(name + "_Foliage",pos + new Vector3(0,0.6f,0),new Vector3(0.55f,0.55f,0.55f), "plantGreen", parent);
        }

        
        static GameObject _printerGO, _coffeeGO, _phoneGO;

        static void PrinterAlcove()
        {
            var root = Empty("PrinterAlcove", new Vector3(-10, 0, 4.2f));
            
            Wall("Alcove_Back", new Vector3(-10, WallH / 2, 3.4f), new Vector3(2.2f, WallH, 0.15f), root.transform);
            Box("Alcove_W",new Vector3(-11,0.75f,4.2f),new Vector3(0.08f,1.5f,1.7f), "cubicleFabric", root.transform);
            Box("Alcove_E", new Vector3(-9, 0.75f, 4.2f), new Vector3(0.08f, 1.5f, 1.7f), "cubicleFabric", root.transform);
            Box("PrinterStand",new Vector3(-10,0.4f,4.3f),new Vector3(0.9f,0.8f,0.7f), "metalDark", root.transform);
            _printerGO = Box("Printer", new Vector3(-10, 0.95f, 4.3f), new Vector3(0.8f, 0.35f, 0.6f), "paperWhite", root.transform);
            Box("Printer_Slot",new Vector3(-10,0.98f,4.0f),new Vector3(0.6f,0.05f,0.02f), "coffeeBlack", root.transform);
        }

        static void BreakRoomProps()
        {
            _rooms++;
            var root = Empty("BreakRoom", new Vector3(-2, 0, 11f));
            
            Box("Counter", new Vector3(-2, 0.45f, 13.5f), new Vector3(3.4f, 0.9f, 0.7f), "deskLaminate",root.transform);
            _coffeeGO = Box("CoffeeMachine", new Vector3(-3, 1.2f, 13.45f), new Vector3(0.5f, 0.6f, 0.5f), "coffeeBlack", root.transform);
            Box("Coffee_Light", new Vector3(-3, 1.35f, 13.18f), new Vector3(0.1f, 0.05f, 0.02f), "accentRed",root.transform);
            
            Box("Watercooler_Base", new Vector3(-0.6f, 0.6f, 13.5f), new Vector3(0.4f, 1.2f, 0.4f), "metalDark", root.transform);
            Box("Watercooler_Jug", new Vector3(-0.6f, 1.45f, 13.5f), new Vector3(0.35f, 0.5f, 0.35f), "screenGlow",root.transform);
            
            Box("BreakTable", new Vector3(-2, 0.72f, 10.6f), new Vector3(1.4f, 0.06f, 1.4f), "deskLaminate", root.transform);
            MakeChair(new Vector3(-3.1f, 0, 10.6f), root.transform);
            MakeChair(new Vector3(-0.9f, 0, 10.6f), root.transform);
        }

        static void StorageProps()
        {
            _rooms++;
            var root = Empty("Storage", new Vector3(3, 0, 11f));
            
            for (int row=0; row < 2; row++)
            {
                float z=9.8f + row * 2.2f;
                CombineBoxes($"Shelves_Row{row}", "metalDark", root.transform, true,
                    new Vector3(3f, 0.4f, z),  new Vector3(4f, 0.08f, 0.6f),
                    new Vector3(3f, 1.1f, z),  new Vector3(4f, 0.08f, 0.6f),
                    new Vector3(3f, 1.8f, z),  new Vector3(4f, 0.08f, 0.6f),
                    new Vector3(1.05f, 0.9f, z), new Vector3(0.08f, 1.8f, 0.6f),
                    new Vector3(4.95f, 0.9f, z), new Vector3(0.08f, 1.8f, 0.6f));
            }
            
            int n = 0;
            for (int i = 0; i < 8; i++)
            {
                float x = 1.3f + (i % 4) * 1.1f, z = (i < 4) ? 9.8f : 12.0f;
                float s = 0.45f + 0.1f * (i % 3);
                Box($"Box{n++}", new Vector3(x, s / 2, z), new Vector3(s, s, s), "folderManila", root.transform);
                if (i % 3 == 0) Box($"Box{n++}",new Vector3(x,s + 0.2f,z),new Vector3(0.4f,0.4f,0.4f), "folderManila", root.transform);
            }
        }

        static void ElevatorLobby()
        {
            _rooms++;
            var root = Empty("ElevatorLobby", new Vector3(13, 0, 11f));
            for (int i = 0; i < 2; i++)
            {
                float z = 10.5f + i * 2.4f;
                
                Box($"Elevator{i}_FrameT", new Vector3(19.75f, 2.35f, z), new Vector3(0.25f, 0.3f, 1.5f), "metalDark",root.transform);
                Box($"Elevator{i}_FrameL", new Vector3(19.75f, 1.1f, z - 0.65f), new Vector3(0.25f, 2.2f, 0.2f), "metalDark", root.transform);
                Box($"Elevator{i}_FrameR", new Vector3(19.75f, 1.1f, z + 0.65f), new Vector3(0.25f, 2.2f, 0.2f), "metalDark",root.transform);
                
                var d1 = Box($"Elevator{i}_DoorL", new Vector3(19.85f, 1.1f, z - 0.28f), new Vector3(0.1f, 2.2f, 0.52f), "metalDark", root.transform);
                var d2 = Box($"Elevator{i}_DoorR", new Vector3(19.85f, 1.1f, z + 0.28f), new Vector3(0.1f, 2.2f, 0.52f), "metalDark",root.transform);
                d1.name = $"Elevator{i}_DoorL"; d2.name = $"Elevator{i}_DoorR";
                _doors += 2;
                
                Box($"Elevator{i}_CallButton", new Vector3(19.6f, 1.2f, z + 0.85f), new Vector3(0.12f, 0.18f, 0.12f), "accentRed",root.transform);
                
                Box($"Elevator{i}_Shaft", new Vector3(19.95f, 1.5f, z), new Vector3(0.05f, 3f, 1.3f), "monsterBlack", root.transform, collider: false);
            }
        }

        static GameObject _stalkerGO;

        static void CorridorProps()
        {
            var root = Empty("CorridorProps", Vector3.zero);
            
            foreach (var x in new[] { XMin + 0.4f, XMax - 0.4f })
            {
                Box("ExitSign_Housing",new Vector3(x,2.55f,6.5f),new Vector3(0.15f,0.35f,0.8f), "accentRed", root.transform, collider: false);
                Box("ExitSign_Face", new Vector3(x + (x < 0 ? 0.08f : -0.08f), 2.55f, 6.5f), new Vector3(0.02f, 0.25f, 0.65f), "exitGreen", root.transform, collider: false);
            }
            
            foreach (var x in new[] { -16f, -4f, 14f })
                CombineBoxes($"FilingCabinet_{x}", "metalDark", root.transform, true,
                    new Vector3(x, 0.65f, 5.35f), new Vector3(0.9f, 1.3f, 0.55f),
                    new Vector3(x, 1.31f, 5.35f), new Vector3(0.92f, 0.02f, 0.57f));
            
            _stalkerGO = Empty("Stalker", new Vector3(2.5f, 0, 6.2f));
            _stalkerGO.transform.SetParent(root.transform, false);
            var body = MakePrimitive("Body", PrimitiveType.Capsule, new Vector3(2.5f, 0.95f, 6.2f),
                new Vector3(0.6f, 0.9f, 0.4f), "stalkerBeige",_stalkerGO.transform);
            MakePrimitive("Head", PrimitiveType.Sphere, new Vector3(2.5f, 1.75f, 6.2f),
                Vector3.one * 0.3f, "stalkerBeige", _stalkerGO.transform);
            
            foreach (var p in LightPositions())
                Box("FluoroFixture", new Vector3(p.x, WallH - 0.06f, p.z),
                    new Vector3(1.6f, 0.12f, 0.4f), "fixtureEmissive",root.transform,collider: false);
        }

        static GameObject MakePrimitive(string name,PrimitiveType type,Vector3 pos,Vector3 scale,
                                        string mat,Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = _mats[mat];
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.NavigationStatic);
            go.transform.SetParent(parent,true);
            _props++;
            return go;
        }

        
        static Vector3[] LightPositions() => new[]
        {
            new Vector3(-16,2.8f,6.5f),new Vector3(-8,2.8f,6.5f),new Vector3(0,2.8f,6.5f),
            new Vector3(8,2.8f,6.5f),   new Vector3(16,2.8f,6.5f),
            new Vector3(-2,2.8f,11f),   new Vector3(3,2.8f,11f),
            new Vector3(0,2.8f,-4f),    new Vector3(-12,2.8f,-8f),new Vector3(12,2.8f,-2f),
        };

        static Light PointLight(string name,Vector3 pos,Color c,float intensity,float range,Transform parent)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.SetParent(parent,false);
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            _lights++;
            return l;
        }

        static void Lights()
        {
            var root = Empty("Lights", Vector3.zero);
            var warm = new Color32(0xFF, 0xF2, 0xDC, 255);
            foreach (var p in LightPositions())
                PointLight("Fluoro_" + p.x + "_" + p.z, p, warm, 0.9f, 6.5f, root.transform);
            _breakRoomLight=PointLight("BreakRoomLight",new Vector3(-2,2.8f,11f),warm,0.9f,6.5f,root.transform);
            _breakRoomLight2 = null; 
            
            var red = new Color32(0xFF,0x30,0x20,255);
            PointLight("EmergencyWest", new Vector3(XMin + 1f, 2.4f, 6.5f), red, 1.1f, 7f, root.transform);
            PointLight("EmergencyEast", new Vector3(XMax - 1f, 2.4f, 6.5f), red, 1.1f, 7f, root.transform);
            
            PointLight("MonitorGlow", new Vector3(-12.4f, 1.1f, 1.2f),
                new Color32(0x88, 0xC0, 0xD0, 255), 0.5f, 3.5f, root.transform);
        }

        
        static void PlayerRig()
        {
            var tFpc   =FindType("NightShift.Player.FirstPersonController");
            var tInter  = FindType("NightShift.Player.PlayerInteractor");
            var tRec    = FindType("NightShift.Rewind.TimelineRecorder");
            _playerGO = new GameObject("Player");
            _playerGO.tag="Player";
            _playerGO.transform.position = new Vector3(0,0.05f,-11.4f); 
            var cc = _playerGO.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0,0.9f,0);
            if (tFpc != null) _playerGO.AddComponent(tFpc); else _mismatches.Add("MISSING NightShift.Player.FirstPersonController");
            if (tInter != null) _playerGO.AddComponent(tInter); else _mismatches.Add("MISSING NightShift.Player.PlayerInteractor");
            if (tRec != null) _playerGO.AddComponent(tRec); else _mismatches.Add("MISSING NightShift.Rewind.TimelineRecorder");

            var camGO=new GameObject("PlayerCamera");
            camGO.transform.SetParent(_playerGO.transform,false);
            camGO.transform.localPosition = new Vector3(0,1.62f,0);
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.fieldOfView = 68f;
            cam.nearClipPlane = 0.08f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0B, 0x0E, 0x14, 255);
            camGO.AddComponent<AudioListener>();
        }

        static void MonsterRig()
        {
            var tAI   = FindType("NightShift.Monster.MonsterAI");
            var tMem  = FindType("NightShift.Monster.MonsterMemory");
            var tRec =FindType("NightShift.Rewind.TimelineRecorder");
            _monsterGO = new GameObject("Monster");
            _monsterGO.transform.position = new Vector3(-18f, 0.05f, 6.5f); 
            var agent = _monsterGO.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f; agent.height = 1.8f; agent.speed = 1.6f;
            Component ai = null;
            if (tAI != null) ai = _monsterGO.AddComponent(tAI); else _mismatches.Add("MISSING NightShift.Monster.MonsterAI");
            if (tMem != null) _monsterGO.AddComponent(tMem); else _mismatches.Add("MISSING NightShift.Monster.MonsterMemory");
            if (tRec != null) _monsterGO.AddComponent(tRec); else _mismatches.Add("MISSING NightShift.Rewind.TimelineRecorder (monster)");

            
            var vis = Empty("MonsterVisual", _monsterGO.transform.position);
            vis.transform.SetParent(_monsterGO.transform, true);
            vis.transform.localPosition = Vector3.zero;
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(cap.GetComponent<Collider>());
            cap.name = "Body";
            cap.transform.SetParent(vis.transform, false);
            cap.transform.localPosition = new Vector3(0, 0.95f, 0);
            cap.transform.localScale = new Vector3(0.5f, 0.95f, 0.35f);
            cap.GetComponent<MeshRenderer>().sharedMaterial = _mats["monsterBlack"];
            foreach (var sx in new[] { -0.09f, 0.09f })
            {
                var eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.name=sx < 0 ? "EyeL" : "EyeR";
                eye.transform.SetParent(vis.transform, false);
                eye.transform.localPosition = new Vector3(sx, 1.68f, 0.18f);
                eye.transform.localScale = Vector3.one * 0.07f;
                eye.GetComponent<MeshRenderer>().sharedMaterial = _mats["eyePale"];
            }

            
            var wps = new[]
            {
                new Vector3(-18, 0.05f, 6.5f), new Vector3(-10, 0.05f, 6.5f), new Vector3(-2, 0.05f, 6.5f),
                new Vector3(8, 0.05f, 6.5f),   new Vector3(16, 0.05f, 6.5f),  new Vector3(13, 0.05f, 9.5f),
                new Vector3(0, 0.05f, 3f),     new Vector3(-6, 0.05f, -3f),
            };
            var wpTransforms = new Transform[wps.Length];
            for (int i = 0; i < wps.Length; i++)
                wpTransforms[i] = Empty($"Waypoint{i}", wps[i], _monsterGO.transform).transform;

            if (ai != null) TryWireWaypoints(ai, wpTransforms);
        }

        static void TryWireWaypoints(Component ai, Transform[] wps)
        {
            var fields=ai.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                var n=f.Name.ToLowerInvariant();
                if (!n.Contains("waypoint") && !n.Contains("patrol")) continue;
                if (f.FieldType == typeof(Transform[]))
                {
                    f.SetValue(ai, wps);
                    _wired.Add($"MonsterAI.{f.Name} (Transform[{wps.Length}])");
                    return;
                }
                if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                    && f.FieldType.GetGenericArguments()[0] == typeof(Transform))
                {
                    var list = (IList)Activator.CreateInstance(f.FieldType);
                    foreach (var w in wps) list.Add(w);
                    f.SetValue(ai, list);
                    _wired.Add($"MonsterAI.{f.Name} (List<Transform> x{wps.Length})");
                    return;
                }
            }
            _mismatches.Add("MonsterAI: no public waypoint/patrol field found in contract — waypoints left as named children Waypoint0..7");
        }

        
        static void GameSystemsRig()
        {
            _systemsGO = new GameObject("GameSystems");
            string[] systems =
            {
                "NightShift.Core.GameManager", "NightShift.Core.ComposureMeter",
                "NightShift.Rewind.RewindController", "NightShift.Rewind.DeathManager",
                "NightShift.Objectives.ObjectiveManager", "NightShift.Audio.AudioDirector",
            };
            foreach (var s in systems)
            {
                var t=FindType(s);
                if (t == null) { _mismatches.Add("MISSING " + s); continue; }
                _systemsGO.AddComponent(t);
                _wired.Add(s);
            }
            WireAudioClips();
        }

        static void WireAudioClips()
        {
            var tDir = FindType("NightShift.Audio.AudioDirector");
            if (tDir == null) return;
            var director = _systemsGO.GetComponent(tDir);
            if (director == null) return;

            
            FieldInfo dictField = null;
            foreach (var f in tDir.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType.IsGenericType &&
                    f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
                    f.FieldType.GetGenericArguments()[0] == typeof(string) &&
                    f.FieldType.GetGenericArguments()[1] == typeof(AudioClip))
                { dictField = f; break; }
            }
            if (dictField == null)
            {
                _mismatches.Add("AudioDirector: no public Dictionary<string,AudioClip> field found — clips not wired");
                return;
            }

            var dict = (IDictionary)(dictField.GetValue(director) ?? Activator.CreateInstance(dictField.FieldType));

            
            const string clipmapPath = "Assets/Audio/clipmap.json";
            if (!File.Exists(clipmapPath))
            {
                _mismatches.Add("Assets/Audio/clipmap.json not found — AudioDirector clips left empty (GenerateAudio.cs not run yet?)");
                dictField.SetValue(director,dict);
                return;
            }
            var json = File.ReadAllText(clipmapPath);
            var rx = new Regex(@"[""']([A-Za-z0-9_]+)[""']\s*:\s*[""']([^""']+)[""']");
            int loaded = 0;
            foreach (Match m in rx.Matches(json))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(m.Groups[2].Value);
                if (clip == null) { _mismatches.Add($"clip '{m.Groups[1].Value}' path missing: {m.Groups[2].Value}"); continue; }
                dict[m.Groups[1].Value] = clip;
                loaded++;
            }
            dictField.SetValue(director, dict);
            _wired.Add($"AudioDirector.{dictField.Name} ({loaded} clips)");
        }

        
        static void ObjectivesRig()
        {
            var tObj=FindType("NightShift.Objectives.Objective");
            var tMgr = FindType("NightShift.Objectives.ObjectiveManager");
            if (tObj == null || tMgr == null)
            {
                if (tObj == null) _mismatches.Add("MISSING NightShift.Objectives.Objective");
                if (tMgr == null) _mismatches.Add("MISSING NightShift.Objectives.ObjectiveManager");
                return;
            }

            
            var phone=Box("Phone",new Vector3(-11.4f,0.82f,1.0f),new Vector3(0.25f,0.09f,0.2f),
                "coffeeBlack"); 
            _phoneGO = phone;

            var defs = new (string id, string title, string prompt, GameObject target)[]
            {
                ("print_report",  "Print the quarterly report",           "Hold E — Print",       _printerGO),
                ("deliver_folder","Deliver the folder to Meeting Room 2", "Hold E — Drop folder", GameObject.Find("MeetingRoom2_Table")),
                ("refill_coffee", "Refill your coffee",                   "Hold E — Refill cup",  _coffeeGO),
                ("water_plant",   "Water the plant",                      "Hold E — Water plant", GameObject.Find("WateringPlant_Foliage")),
                ("answer_phone",  "Answer the phone",                     "Hold E — Answer",      phone),
            };

            var created = new List<Component>();
            foreach (var d in defs)
            {
                if (d.target == null)
                { _mismatches.Add($"objective '{d.id}': target prop missing"); continue; }
                var c=d.target.AddComponent(tObj);
                SetMember(c, "Id",d.id);
                SetMember(c, "Title", d.title);
                SetMember(c, "PromptText", d.prompt);
                created.Add(c);
                _wired.Add($"Objective[{d.id}] on {d.target.name}");
            }

            
            var mgr=_systemsGO != null ? _systemsGO.GetComponent(tMgr) : null;
            if (mgr == null) { _mismatches.Add("ObjectiveManager component not present on GameSystems"); return; }
            foreach (var f in tMgr.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(List<>)
                    && f.FieldType.GetGenericArguments()[0] == tObj)
                {
                    var list = (IList)(f.GetValue(mgr) ?? Activator.CreateInstance(f.FieldType));
                    list.Clear();
                    foreach (var c in created) list.Add(c);
                    f.SetValue(mgr,list);
                    _wired.Add($"ObjectiveManager.{f.Name} (sequence x{created.Count})");
                    return;
                }
                if (f.FieldType == tObj.MakeArrayType())
                {
                    f.SetValue(mgr, created.ToArray());
                    _wired.Add($"ObjectiveManager.{f.Name} (Objective[{created.Count}])");
                    return;
                }
            }
            _mismatches.Add("ObjectiveManager: no public List<Objective>/Objective[] field found (contract Sequence is get-only) — sequence NOT wired");
        }

        
        static void ScaresRig()
        {
            
            AddScare("NightShift.Scares.PrinterScare", "printer", _printerGO, new Vector3(-10, 1f, 4.3f), new Vector3(3, 2, 3));
            
            var elevZone = Empty("ElevatorScareZone", new Vector3(18.8f, 1f, 11.7f));
            AddScare("NightShift.Scares.ElevatorScare", "elevator",elevZone,Vector3.zero,new Vector3(3f,2.4f,4.5f));
            
            AddScare("NightShift.Scares.StalkerEmployee", "stalker", _stalkerGO, new Vector3(2.5f, 1f, 6.2f), new Vector3(4, 2.4f, 4));
            
            AddScare("NightShift.Scares.PhoneCallScare", "phone",_phoneGO,new Vector3(-11.4f,1f,1f),new Vector3(2.5f,2,2.5f));
            
            var coffee = AddScare("NightShift.Scares.CoffeeMachineScare", "coffee", _coffeeGO, new Vector3(-2.5f, 1f, 12f), new Vector3(4, 2.4f, 4));
            if (coffee != null)
            {
                if (_breakRoomLight != null)
                {
                    if (!SetMember(coffee, "RoomLights", new[] { _breakRoomLight }))
                        _mismatches.Add("CoffeeMachineScare.RoomLights field not found — runtime default used");
                    else _wired.Add("CoffeeMachineScare.RoomLights[1]");
                }
                else _mismatches.Add("CoffeeMachineScare: no break room light available for RoomLights");
            }
        }

        static Component AddScare(string typeName, string scareId, GameObject host, Vector3 triggerCenter, Vector3 triggerSize)
        {
            var t = FindType(typeName);
            if (t == null)
            { _mismatches.Add("MISSING " + typeName); return null; }
            if (host == null) { _mismatches.Add($"{typeName}: host prop missing"); return null; }
            var c = host.AddComponent(t);
            SetMember(c, "ScareId", scareId);
            SetMember(c, "ComposureDamage", 15f);
            
            
            var trig = Empty(host.name + "_Trigger", triggerCenter, host.transform);
            var bc=trig.AddComponent<BoxCollider>();
            bc.isTrigger=true;
            bc.size=triggerSize; 
            _wired.Add($"{typeName} (id={scareId})");
            return c;
        }

        
        static void KillZonesRig()
        {
            var tKz = FindType("NightShift.Rewind.KillZone");
            if (tKz == null) { _mismatches.Add("MISSING NightShift.Rewind.KillZone"); return; }
            MakeKillZone(tKz, "KillZone_ElevatorShaft_0", new Vector3(19.8f, 1.1f, 10.5f), new Vector3(1.2f, 2.2f, 0.9f), "elevator_shaft");
            MakeKillZone(tKz, "KillZone_ElevatorShaft_1", new Vector3(19.8f, 1.1f, 12.9f), new Vector3(1.2f, 2.2f, 0.9f), "elevator_shaft");
            MakeKillZone(tKz, "KillZone_StorageCrush",   new Vector3(3f, 1.1f, 10.9f),    new Vector3(3.5f, 2.2f, 1.2f), "storage_crush");
        }

        static void MakeKillZone(Type tKz,string name,Vector3 center,Vector3 size,string cause)
        {
            var go = Empty(name,center);
            var bc = go.AddComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size;
            var c = go.AddComponent(tKz);
            SetMember(c, "Cause", cause);
            _wired.Add($"{name} (cause={cause})");
        }

        
        static void RewindablesRig()
        {
            var tRw = FindType("NightShift.Rewind.RewindableObject");
            if (tRw == null) { _mismatches.Add("MISSING NightShift.Rewind.RewindableObject"); return; }
            string[] names =
            {
                "Printer", "CoffeeMachine", "Phone", "Stalker", "Monster",
                "MeetingRoom1_Door", "MeetingRoom2_Door", "MeetingRoom3_Door", "MeetingRoom4_Door",
                "BreakRoom_Door", "Storage_Door",
                "Elevator0_DoorL", "Elevator0_DoorR", "Elevator1_DoorL", "Elevator1_DoorR",
            };
            int added=0;
            foreach (var n in names)
            {
                var go=GameObject.Find(n);
                if (go == null) continue;
                if (go.GetComponent(tRw) != null) continue;
                go.AddComponent(tRw);
                added++;
            }
            _wired.Add($"RewindableObject x{added}");
        }

        
        static void NavMeshRig()
        {
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh(); 
            var tri = NavMesh.CalculateTriangulation();
            _navTris = tri.indices.Length / 3;
        }

        
        static void PostFXRig()
        {
            const string profilePath = "Assets/Settings/NightShiftPostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.28f);
            var ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.Override(0.15f);
            var col = profile.Add<ColorAdjustments>(true);
            col.postExposure.Override(-0.4f);
            col.saturation.Override(-8f);
            col.contrast.Override(6f);
            var grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.15f);
            foreach (var comp in profile.components)
                if (!AssetDatabase.Contains(comp)) AssetDatabase.AddObjectToAsset(comp, profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("PostFX");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 0f;
            vol.profile = profile;
            _wired.Add("PostFX Volume (ACES, Vignette 0.28, CA 0.15, ColorAdj, Grain 0.15)");
        }

        
        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t=asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        static bool SetMember(object target, string name, object value)
        {
            if (target == null) return false;
            var t=target.GetType();
            var f=t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) { f.SetValue(target, value); return true; }
            var p=t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && p.CanWrite) { p.SetValue(target, value); return true; }
            return false;
        }
    }
}
