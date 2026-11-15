#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class QaBuild
{
    static readonly System.Text.StringBuilder Out = new System.Text.StringBuilder();
    static void Q(string s)
    { Debug.Log("QABUILD " + s); Out.AppendLine("QABUILD " + s); }

    static string Arg(string name)
    {
        var a = Environment.GetCommandLineArgs();
        for (int i = 0; i < a.Length - 1; i++) if (string.Equals(a[i],name,StringComparison.OrdinalIgnoreCase)) return a[i + 1];
        return null;
    }

    static bool Flag(string name) => Environment.GetCommandLineArgs().Any(x => string.Equals(x,name,StringComparison.OrdinalIgnoreCase));

static void Exit(int code) { if (Application.isBatchMode && Flag("-executeMethod")) EditorApplication.Exit(code); }

    public static void Report()
    {
        try { ReportCore(true); Exit(0); }
        catch (Exception e) { Debug.LogError("QABUILD REPORT failed: " + e); Exit(1); }
    }

    
    public static string ReportText()
    {
        Out.Clear();
        try { ReportCore(true); } catch (Exception e)
        { Q("REPORT failed: " + e.Message); }
        return Out.ToString();
    }

    
    public static string PlayerTo(string outPath, string target="",bool dev = false,string scenes = "", bool strict = false)
    {
        Out.Clear();
        try { PlayerCore(outPath, target, dev, scenes, strict); } catch (Exception e) { Q("RESULT failed: " + e.Message); }
        return Out.ToString();
    }

    static string PipelineKind(RenderPipelineAsset rp)
    {
        if (rp == null) return "Built-in";
        var n=rp.GetType().FullName ?? "";
        if (n.Contains("Universal")) return "URP";
        if (n.Contains("HDRenderPipeline") || n.Contains("HighDefinition")) return "HDRP";
        return "custom SRP (" + n + ")";
    }

    static string[] BuildScenes(string arg)
    {
        if (!string.IsNullOrEmpty(arg)) return arg.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        return EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
    }

    static IEnumerable<string> RuntimeScripts() =>
        Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
            .Select(p => p.Replace('\\', '/'))
            .Where(p => !p.Contains("/Editor/") && !p.StartsWith("Assets/QA/") && !p.Contains("/Tests/"));

    static void ReportCore(bool full)
    {
        string kind=PipelineKind(GraphicsSettings.currentRenderPipeline);
        if (full)
        {
            Q("unity=" + Application.unityVersion + " target=" + EditorUserBuildSettings.activeBuildTarget + " product=\"" + PlayerSettings.productName + "\" company=\"" + PlayerSettings.companyName + "\"");
            var rp = GraphicsSettings.currentRenderPipeline;
            Q("render_pipeline=" + kind + (rp != null ? " asset=" + AssetDatabase.GetAssetPath(rp) : ""));
            for (int i=0; i < QualitySettings.names.Length; i++)
            {
                var q=QualitySettings.GetRenderPipelineAssetAt(i);
                Q(string.Format("quality[{0}] {1}{2} pipeline={3}",i,QualitySettings.names[i],i == QualitySettings.GetQualityLevel() ? " (active)" : "",
                    q == null ? "(default)" : PipelineKind(q) + " " + AssetDatabase.GetAssetPath(q)));
            }
            Q("color_space=" + PlayerSettings.colorSpace + " scripting_backend=" + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone));
        }
        string ps = File.Exists("ProjectSettings/ProjectSettings.asset") ? File.ReadAllText("ProjectSettings/ProjectSettings.asset") : "";
        var m = Regex.Match(ps, @"activeInputHandler:\s*(\d)");
        string ih = m.Success ? m.Groups[1].Value : "?";
        if (full) Q("input_handling=" + (ih == "0" ? "legacy Input Manager" : ih == "1" ? "Input System only" : ih == "2" ? "both" : "?"));
        var scenes=BuildScenes(null);
        if (full) Q("build_scenes=" + (scenes.Length == 0 ? "NONE (add them: EditorBuildSettings.scenes)" : string.Join(", ", scenes)));
        string man = File.Exists("Packages/manifest.json") ? File.ReadAllText("Packages/manifest.json") : "";
        var pkgs = Regex.Matches(man, "\"(com\\.[^\"]+)\"\\s*:\\s*\"([^\"]+)\"").Cast<Match>().Select(x => x.Groups[1].Value + "@" + x.Groups[2].Value)
            .Where(x => !x.StartsWith("com.unity.modules.")).ToList();
        if (full) Q("packages=" + string.Join(", ", pkgs));
        var extras = pkgs.Where(p => Regex.IsMatch(p, @"^com\.unity\.(purchasing|ads|analytics|services\.|multiplayer\.center|visualscripting|collab-proxy|2d\.|feature\.2d)")).ToList();
        if (extras.Count > 0) Q("PACKAGE-EXTRA " + string.Join(", ", extras) + " - remove what this game does not use (Packages/manifest.json), then reimport");
        bool tmp = TmpReady();
        if (full) Q("tmp_essentials=" + (tmp ? "yes" : "no (QaBuild.ImportTmpEssentials, or Window > TextMeshPro > Import TMP Essential Resources)"));

        
        var always = new HashSet<string>();
        try
        {
            var gso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var arr = gso.FindProperty("m_AlwaysIncludedShaders");
            for (int i=0; arr != null && i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue is Shader s) always.Add(s.name);
        }
        catch (Exception e) { Q("WARN could not read Always Included Shaders: " + e.Message); }
        var roots = new List<string>(scenes);
        if (GraphicsSettings.currentRenderPipeline != null) roots.Add(AssetDatabase.GetAssetPath(GraphicsSettings.currentRenderPipeline));
        for (int i = 0; i < QualitySettings.names.Length; i++) { var q = QualitySettings.GetRenderPipelineAssetAt(i); if (q != null) roots.Add(AssetDatabase.GetAssetPath(q)); }
        var resources = AssetDatabase.GetAllAssetPaths().Where(p => p.Contains("/Resources/")).ToArray();
        roots.AddRange(resources);
        var deps = new HashSet<string>(AssetDatabase.GetDependencies(roots.Where(r => !string.IsNullOrEmpty(r)).ToArray(), true));
        var findRx = new Regex("Shader\\.Find\\(\\s*\"([^\"]+)\"\\s*\\)");
        int risks = 0, finds = 0;
        foreach (var f in RuntimeScripts())
        {
            var src = File.ReadAllText(f);
            foreach (Match fm in findRx.Matches(src))
            {
                finds++;
                string name = fm.Groups[1].Value;
                var sh = Shader.Find(name);
                if (sh == null)
                { risks++; Q("SHADER-MISSING \"" + name + "\" (" + f + ") does not exist in this project/pipeline (" + kind + ")"); continue; }
                if ((kind == "URP" || kind == "HDRP") && (name == "Standard" || name.StartsWith("Legacy Shaders/") || name.StartsWith("Mobile/")))
                { risks++; Q("SHADER-PIPELINE \"" + name + "\" (" + f + ") is a Built-in shader; under " + kind + " it renders pink. Use the pipeline's shaders (render-pipelines.md 3; currentRenderPipeline.defaultMaterial is null in a player)"); }
                if (kind == "Built-in" && (name.StartsWith("Universal Render Pipeline/") || name.StartsWith("HDRP/")))
                { risks++; Q("SHADER-PIPELINE \"" + name + "\" (" + f + ") needs a render pipeline this project does not use"); }
                string path = AssetDatabase.GetAssetPath(sh);
                if (always.Contains(name) || deps.Contains(path)) continue;
                risks++;
                Q("SHADER-RISK \"" + name + "\" (" + f + ") is found by name at runtime but nothing in the build references it: a player build strips it and Shader.Find returns null. "
                  + "Fix: reference a Material using it from a build scene or a Resources folder, or add it to Graphics Settings > Always Included Shaders");
            }
        }
        Q(string.Format("shaders: {0} Shader.Find call(s) in runtime scripts, {1} problem(s)", finds, risks));
        
        
        var kwRx = new Regex(@"\.(EnableKeyword|SetKeyword)\s*\(|\.shaderKeywords\s*=|SetFloat\s*\(\s*""_(Surface|SurfaceType|AlphaClip|AlphaCutoffEnable)""");
        var kwFiles=RuntimeScripts().Where(f => kwRx.IsMatch(File.ReadAllText(f))).ToList();
        if (kwFiles.Count > 0)
            Q("SHADER-KEYWORDS-RUNTIME " + string.Join(", ", kwFiles.Take(6)) + " set material keywords or surface type at runtime: a player keeps only the keyword variants some saved material in the build uses and silently draws the closest one otherwise. "
              + "Use saved materials (in a scene or Resources) or a ShaderVariantCollection; check with a strict build (PlayerTo(..., strict: true) / -qaStrict): a missing variant then draws magenta and unity-playtest.py names it");

        
        var legacyRx=new Regex(@"(?<![\w.])Input\.(GetKey|GetKeyDown|GetKeyUp|GetAxis|GetAxisRaw|GetButton|GetButtonDown|GetMouseButton|GetMouseButtonDown|mousePosition|anyKey|GetTouch|touchCount)\b");
        var legacy = RuntimeScripts().Where(f => legacyRx.IsMatch(File.ReadAllText(f))).ToList();
        if (legacy.Count > 0)
            Q("INPUT-LEGACY " + string.Join(", ", legacy.Take(8)) + (ih == "1" ? " - Input handling is 'Input System only': these calls throw at runtime" : " - legacy UnityEngine.Input: QaPlaytest's virtual devices cannot drive it; read input through the Input System"));
        var imgui = RuntimeScripts().Where(f => Regex.IsMatch(File.ReadAllText(f), @"void\s+OnGUI\s*\(")).ToList();
        if (imgui.Count > 0) Q("IMGUI-RUNTIME " + string.Join(", ",imgui.Take(8)) + " - OnGUI is debug/editor UI; build game menus and HUD with uGUI or UI Toolkit (ui skill)");
        bool cine = pkgs.Any(p => p.StartsWith("com.unity.cinemachine@"));
        var camScripts = RuntimeScripts().Where(f => { var s = File.ReadAllText(f); return Regex.IsMatch(s, @"Camera\.main|GetComponent<Camera>") && Regex.IsMatch(s, @"(Lerp|SmoothDamp)\s*\(") && !s.Contains("Cinemachine"); }).ToList();
        if (camScripts.Count > 0) Q("CAMERA hand-written camera motion in " + string.Join(", ", camScripts.Take(5)) + (cine ? " while Cinemachine is installed - use it or remove the package" : " - consider Cinemachine (follow, damping, collision, blends)"));
        else if (cine && !RuntimeScripts().Any(f => File.ReadAllText(f).Contains("Cinemachine")) && !AssetDatabase.FindAssets("t:Prefab").Any())
            Q("CAMERA Cinemachine is installed; check a CinemachineCamera exists in the scenes or remove the package");

        
        if (full && scenes.Length > 0 && !EditorApplication.isPlaying)
        {
            var cur = EditorSceneManager.GetActiveScene().path;
            foreach (var sp in scenes)
            {
                var sc = EditorSceneManager.OpenScene(sp,OpenSceneMode.Single);
                var all = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Component>(true)).Where(c => c != null).ToList();
                int canvases = all.Count(c => c is Canvas),cams = all.Count(c => c is Camera);
                var modules = all.Where(c => c.GetType().Name.EndsWith("InputModule")).Select(c => c.GetType().Name).Distinct().ToList();
                bool cm = all.Any(c => c.GetType().Name == "CinemachineCamera" || c.GetType().Name == "CinemachineVirtualCamera");
                Q(string.Format("scene {0}: cameras={1} canvases={2} input_modules={3} cinemachine={4} lights={5}",sp,cams,canvases,
                    modules.Count == 0 ? "none" : string.Join("/", modules), cm ? "yes" : "no",all.Count(c => c is Light)));
                if (canvases > 0 && modules.Count == 0) Q("INPUT-MISMATCH " + sp + " has a Canvas but no EventSystem/input module: buttons cannot be clicked");
                if (ih == "1" && modules.Contains("StandaloneInputModule")) Q("INPUT-MISMATCH " + sp + " uses StandaloneInputModule but only the Input System is active: replace it with InputSystemUIInputModule");
                Census(sp, sc);
            }
            if (!string.IsNullOrEmpty(cur) && File.Exists(cur)) EditorSceneManager.OpenScene(cur, OpenSceneMode.Single);
        }
    }

static void Census(string path, UnityEngine.SceneManagement.Scene sc)
    {
        var roots = sc.GetRootGameObjects();
        var rends = roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(false))
            .Where(r => r.enabled && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToList();
        var files = new Dictionary<string, int>();
        var prims = new Dictionary<string, int>();
        var areas = new Dictionary<(Mesh, int, int, int), double>();
        var plain = new List<KeyValuePair<double, string>>();
        var plainMats = new Dictionary<string, int>();
        var mats = new HashSet<Material>();
        int drawn = 0, plainCount = 0, generated = 0, fromFiles = 0, texturedMats = 0;
        foreach (var r in rends)
        {
            Mesh mesh = r is SkinnedMeshRenderer sk ? sk.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null) continue;
            drawn++;
            string mp = AssetDatabase.GetAssetPath(mesh);
            if (mp.StartsWith("Library/unity default resources") || mp.StartsWith("Resources/unity_builtin_extra"))
                prims[mesh.name] = prims.TryGetValue(mesh.name,out var n0) ? n0 + 1 : 1;
            else if (string.IsNullOrEmpty(mp) || mp.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) generated++;
            else { fromFiles++; files[mp] = files.TryGetValue(mp, out var n1) ? n1 + 1 : 1; }
            bool textured = false;
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                bool t = Textured(m);
                if (mats.Add(m) && t) texturedMats++;
                textured |= t;
            }
            if (textured) continue;
            plainCount++;
            string mn = r.sharedMaterial ? r.sharedMaterial.name : "no material";
            plainMats[mn] = plainMats.TryGetValue(mn, out var n2) ? n2 + 1 : 1;
            var s = r.transform.lossyScale;
            var key = (mesh, Mathf.RoundToInt(s.x * 100), Mathf.RoundToInt(s.y * 100), Mathf.RoundToInt(s.z * 100));
            if (!areas.TryGetValue(key, out double area)) areas[key] = area = Area(mesh, s);
            plain.Add(new KeyValuePair<double, string>(area, r.name + " [" + mn + "]"));
        }
        foreach (var t in roots.SelectMany(g => g.GetComponentsInChildren<Component>(false)).Where(c => c != null && c.GetType().Name == "Terrain"))
        {   
            var td = t.GetType().GetProperty("terrainData")?.GetValue(t);
            if (td == null) continue;
            drawn++;
            var layers = td.GetType().GetProperty("terrainLayers")?.GetValue(td) as Array;
            if (layers != null && layers.Length > 0) continue;
            var size=(Vector3)td.GetType().GetProperty("size").GetValue(td);
            plainCount++;
            plain.Add(new KeyValuePair<double,string>(size.x * size.z,t.name + " [terrain without layers]"));
        }
        int primCount = prims.Values.Sum();
        double share = drawn > 0 ? (double)plainCount / drawn : 0;
        Q(string.Format("content {0}: {1} mesh renderers - from model files {2} ({3} files), Unity primitives {4}, made by code {5}; materials {6} ({7} textured); {8} drawn objects ({9:F0}%) use only plain-colour materials",
            path, rends.Count, fromFiles, files.Count, primCount, generated, mats.Count, texturedMats, plainCount, share * 100));
        if (files.Count > 0)
            Q("content " + path + " model files: " + string.Join(", ", files.OrderByDescending(f => f.Value).Take(8).Select(f => Path.GetFileName(f.Key) + " x" + f.Value)));
        if (plain.Count > 0)
            Q("content " + path + " largest plain-colour surfaces: " + string.Join(", ", plain.OrderByDescending(p => p.Key).Take(3).Select(p => p.Value + " " + (p.Key >= 10000 ? (p.Key / 10000).ToString("F1") + " ha" : p.Key.ToString("F0") + " m2")))
              + " - whether they show on screen is a question for the frames (compare-sheet: flat), not for this list");
        if (primCount >= 12 && primCount * 5 >= rends.Count)
            Q("CONTENT-PRIMITIVES " + path + ": " + primCount + " renderers draw Unity primitive meshes (" + string.Join(", ", prims.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value))
              + ") - greybox: what the camera sees comes from sourced or authored models (unity-content-sourcing); primitives stay for colliders and hidden helpers");
        if (drawn >= 12 && share >= 0.5)
            Q(string.Format("CONTENT-UNTEXTURED {0}: {1:F0}% of the drawn objects use only plain-colour materials (most common: {2}) - unless flat colour is the chosen art style this reads as placeholder: give surfaces materials (unity-content-sourcing) and compare frames with the references",
                path, share * 100, string.Join(", ", plainMats.OrderByDescending(p => p.Value).Take(4).Select(p => p.Key + " " + p.Value))));
        FxAudit(path,roots);
    }

static readonly Regex IgnoresVertexColour = new Regex(@"^(HDRP/(Lit|LitTessellation|Unlit)|Universal Render Pipeline/(Lit|Simple Lit|Complex Lit|Baked Lit|Unlit)|Standard( \(Specular setup\))?|Unlit/(Color|Texture|Transparent|Transparent Cutout))$");
    static readonly Regex KnownFamily = new Regex(@"^(Universal Render Pipeline/|HDRP/|Standard|Unlit/|Particles/|Legacy Shaders/Particles|Mobile/Particles|Sprites/Default)");

    static void FxAudit(string path, GameObject[] roots)
    {
        var rends = new List<KeyValuePair<Renderer, string>>();
        foreach (var r in roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true))) rends.Add(new KeyValuePair<Renderer, string>(r, "scene"));
        foreach (var dep in AssetDatabase.GetDependencies(path, true).Where(d => d.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)))
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(dep);
            if (pf != null) foreach (var r in pf.GetComponentsInChildren<Renderer>(true)) rends.Add(new KeyValuePair<Renderer,string>(r,Path.GetFileName(dep)));
        }
        var found = new Dictionary<Material,(HashSet<string> why,int n,HashSet<string> places)>();
        int systems = 0,lines = 0;
        foreach (var kv in rends)
        {
            var r = kv.Key;
            bool quads;
            if (r is ParticleSystemRenderer pr) { systems++; quads = pr.renderMode != ParticleSystemRenderMode.Mesh && pr.renderMode != ParticleSystemRenderMode.None; }
            else if (r is TrailRenderer || r is LineRenderer) { lines++; quads = false; }
            else continue;
            var mats = r.sharedMaterials.ToList();
            if (r is ParticleSystemRenderer p2 && p2.trailMaterial != null) mats.Add(p2.trailMaterial);
            foreach (var m in mats)
            {
                var why = new List<string>();
                if (m == null) why.Add("no material (magenta)");
                else
                {
                    string sh = m.shader != null ? m.shader.name : "";
                    if (IgnoresVertexColour.IsMatch(sh)) why.Add("its shader ignores vertex colour: start colour and fades do nothing");
                    if (quads && m.renderQueue < 2450) why.Add("opaque (queue " + m.renderQueue + "): hard-edged cards");
                    if (quads && KnownFamily.IsMatch(sh) && !new[] { "_BaseMap", "_MainTex", "_UnlitColorMap", "_BaseColorMap" }.Any(t => m.HasProperty(t) && m.GetTexture(t) != null))
                        why.Add("no texture: every particle is a solid quad");
                }
                if (why.Count == 0) continue;
                var key = m;
                if (key == null)
                { Q("FX-QUADS " + path + ": " + PathOf(r.transform) + " (" + kv.Value + ") has an empty material slot - it draws magenta"); continue; }
                if (!found.TryGetValue(key, out var e)) e = (new HashSet<string>(), 0, new HashSet<string>());
                foreach (var w in why) e.why.Add(w);
                e.places.Add(kv.Value);
                found[key] = (e.why, e.n + 1, e.places);
            }
        }
        if (systems + lines == 0) return;
        Q(string.Format("fx {0}: {1} particle systems, {2} trails/lines (scene and referenced prefabs), {3} material(s) with problems", path, systems, lines, found.Count));
        foreach (var f in found.OrderByDescending(x => x.Value.n).Take(12))
            Q("FX-QUADS " + f.Key.name + " (" + (f.Key.shader != null ? f.Key.shader.name : "?") + ") on " + f.Value.n + " renderer(s) in " + string.Join(", ", f.Value.places.Take(3)) + ": "
              + string.Join("; ", f.Value.why) + " - a soft texture, a transparent surface and a shader that reads vertex colour (render-pipelines.md 3, Particles)");
    }

    static string PathOf(Transform t)
    {
        var s = t.name;
        for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
        return s;
    }

    static double Area(Mesh mesh, Vector3 s)
    {
        try
        {
            var v = mesh.vertices; var t = mesh.triangles; double a = 0;
            for (int i = 0; i + 2 < t.Length; i += 3)
                a += Vector3.Cross(Vector3.Scale(v[t[i + 1]] - v[t[i]], s), Vector3.Scale(v[t[i + 2]] - v[t[i]], s)).magnitude * 0.5;
            return a;
        }
        catch { var e = Vector3.Scale(mesh.bounds.size, s); return 2.0 * (Math.Abs(e.x * e.y) + Math.Abs(e.y * e.z) + Math.Abs(e.x * e.z)); }
    }

static bool Textured(Material m)
    {
        if (m.shader == null || !Regex.IsMatch(m.shader.name, @"^(Universal Render Pipeline/(Lit|Simple Lit|Complex Lit|Baked Lit|Unlit)|HDRP/(Lit|Unlit)|Standard|Unlit/Color|Legacy Shaders/)"))
            return true;
        foreach (var n in m.GetTexturePropertyNames())
            if (m.GetTexture(n) != null) return true;
        return false;
    }

    public static void Player()
    {
        try { Exit(PlayerCore(Arg("-qaBuildOut"), Arg("-qaTarget"),Flag("-qaDev"), Arg("-qaScenes"), Flag("-qaStrict")) ? 0 : 1); }
        catch (Exception e) { Debug.LogError("QABUILD RESULT failed: " + e); Exit(1); }
    }

    static bool PlayerCore(string buildOut,string targetArg,bool dev,string scenesArg,bool strict = false)
    {
        {
            ReportCore(false);
            var scenes = BuildScenes(scenesArg);
            if (scenes.Length == 0) { Q("RESULT failed: no scenes (add them to EditorBuildSettings.scenes or pass -qaScenes)"); return false; }
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            switch ((targetArg ?? "").ToLowerInvariant())
            {
                case "win64": case "windows": target = BuildTarget.StandaloneWindows64; break;
                case "osx": case "mac": case "macos": target=BuildTarget.StandaloneOSX; break;
                case "linux64": case "linux": target = BuildTarget.StandaloneLinux64; break;
                case "": if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64)
                        target = Application.platform == RuntimePlatform.OSXEditor ? BuildTarget.StandaloneOSX : Application.platform == RuntimePlatform.LinuxEditor ? BuildTarget.StandaloneLinux64 : BuildTarget.StandaloneWindows64;
                    break;
                default: Q("RESULT failed: unknown target " + targetArg); return false;
            }
            string product=Regex.Replace(PlayerSettings.productName ?? "Game",@"[^\w\- ]", "").Trim();
            if (product.Length == 0) product = "Game";
            string ext=target == BuildTarget.StandaloneWindows64 ? ".exe" : target == BuildTarget.StandaloneOSX ? ".app" : "";
            string outPath = string.IsNullOrEmpty(buildOut) ? Path.Combine("Builds", target.ToString(), product + ext) : buildOut;
            var opts=new BuildPlayerOptions
            {
                scenes=scenes, locationPathName=outPath, target=target, targetGroup=BuildPipeline.GetBuildTargetGroup(target),
                options=dev ? BuildOptions.Development : BuildOptions.None
            };
            Q("building " + target + " -> " + outPath + " scenes=" + string.Join(",", scenes) + (dev ? " (development)" : "") + (strict ? " (strict shader variant matching)" : ""));
            bool strictWas=PlayerSettings.strictShaderVariantMatching;
            PlayerSettings.strictShaderVariantMatching=strict || strictWas;
            BuildReport rep;
            try { rep=BuildPipeline.BuildPlayer(opts); }
            finally { PlayerSettings.strictShaderVariantMatching=strictWas; }
            var s=rep.summary;
            int shown=0;
            foreach (var step in rep.steps)
                foreach (var msg in step.messages)
                    if ((msg.type == LogType.Error || msg.type == LogType.Exception) && shown++ < 20) Q("BUILD-ERROR " + msg.content.Split('\n')[0]);
            Q(string.Format("RESULT {0} out={1} size_mb={2:F0} time_s={3:F0} errors={4} warnings={5}", s.result, Path.GetFullPath(outPath), s.totalSize / 1048576.0,
                s.totalTime.TotalSeconds, s.totalErrors, s.totalWarnings));
            return s.result == BuildResult.Succeeded;
        }
    }

public static bool TmpReady()
    {
        var res = Resources.Load("TMP Settings");
        return res != null && AssetDatabase.GetAssetPath(res).StartsWith("Assets/",StringComparison.Ordinal);
    }

    
    public static string ImportTmpEssentialsText()
    {
        Out.Clear();
        try { ImportTmpEssentials(); } catch (Exception e) { Q("RESULT failed: " + e.Message); }
        return Out.ToString();
    }

    public static void ImportTmpEssentials()
    {
        try
        {
            if (TmpReady()) { Q("tmp_essentials already present"); Exit(0); return; }
            
            
            string pkg = null;
            foreach (var name in new[] { "com.unity.ugui", "com.unity.textmeshpro" })
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/" + name);
                if (info == null) continue;
                var f = Path.Combine(info.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (File.Exists(f)) { pkg=f; break; }
            }
            if (pkg == null) { Q("RESULT failed: TMP Essential Resources.unitypackage not found (is com.unity.ugui / TextMeshPro installed?)"); Exit(1); return; }
            Q("importing " + pkg);
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Q("RESULT " + (TmpReady() ? "ok: TMP essentials imported" : "pending: import started; check again with QaBuild.TmpReady() after the import finishes"));
            Exit(0);
        }
        catch (Exception e) { Debug.LogError("QABUILD RESULT failed: " + e); Exit(1); }
    }
}
#endif
