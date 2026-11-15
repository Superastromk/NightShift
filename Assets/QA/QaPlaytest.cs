#if QA_HAS_INPUTSYSTEM
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace QA
{
    [Preserve]
    public class QaPlaytest : MonoBehaviour
    {
        static readonly CultureInfo IC = CultureInfo.InvariantCulture;
        static QaPlaytest active;

        string steps, outDir, trackSpec;
        float telEvery = 0.25f, timeoutSec, nextTel, nextResolve, lastFrameT;
        bool quitAtEnd;
        StreamWriter qaLog, telLog;
        int shotN, errorsSeen, framesTotal, stepNo;
        float worstFrame, sumFrame;
        readonly List<Transform> tracked = new List<Transform>();
        readonly Dictionary<Transform, Vector3> lastPos = new Dictionary<Transform, Vector3>();
        readonly List<Vector3> travelled = new List<Vector3>();
        readonly Dictionary<Type, MethodInfo> telMethods = new Dictionary<Type, MethodInfo>();
        
        
        readonly List<KeyValuePair<long, string>> recentLogs = new List<KeyValuePair<long, string>>();
        long logSeq, stepStartSeq, prevStepSeq;

        
        Keyboard kb; Mouse mouse; Gamepad pad;
        readonly HashSet<Key> keysDown = new HashSet<Key>();
        bool keysDirty;
        Vector2 mousePos, lookPerFrame, scrollOnce; bool lmb, rmb, mouseDirty; int lookFrames;
        Vector2 lStick, rStick; float lTrig, rTrig; readonly HashSet<GamepadButton> padDown = new HashSet<GamepadButton>(); bool padDirty;

        bool AnyInput => keysDown.Count > 0 || lmb || rmb || lookFrames > 0 || lStick.sqrMagnitude > 0.01f ||
                         rStick.sqrMagnitude > 0.01f || lTrig > 0.05f || rTrig > 0.05f || padDown.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        [Preserve]
        static void Boot()
        {
            var a = Environment.GetCommandLineArgs();
            string s = Arg(a, "-qaSteps"), f = Arg(a, "-qaStepsFile");
            if (s == null && f != null && File.Exists(f)) s=File.ReadAllText(f);
            if (s == null) return;
            float.TryParse(Arg(a, "-qaTel") ?? "0.25", NumberStyles.Float, IC, out var tel);
            float.TryParse(Arg(a, "-qaTimeout") ?? "0", NumberStyles.Float, IC, out var to);
            Begin(s, Arg(a, "-qaOut"),Arg(a, "-qaTrack"), tel, to, !a.Contains("-qaStay"));
        }

        
        public static QaPlaytest Run(string stepScript, string outputDir = null, string track = null, bool quit = false)
            => Begin(stepScript, outputDir, track, 0.25f, 0f, quit);

        static QaPlaytest Begin(string s, string o, string track, float tel, float timeout, bool quit)
        {
            if (active != null) Destroy(active.gameObject);
            var go = new GameObject("QaPlaytest");
            DontDestroyOnLoad(go);
            var q=go.AddComponent<QaPlaytest>();
            q.steps=s; q.trackSpec=track; q.quitAtEnd=quit; q.timeoutSec=timeout;
            q.telEvery=tel > 0.02f ? tel : 0.25f;
            q.outDir=string.IsNullOrEmpty(o)
                ? Path.Combine(Application.persistentDataPath, "QA",DateTime.Now.ToString("yyyyMMdd-HHmmss", IC)) : o;
            active = q;
            return q;
        }

        static string Arg(string[] a, string name)
        {
            for (int i = 0; i < a.Length - 1; i++)
                if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
            return null;
        }

        void Awake() { Application.logMessageReceived += OnLog; }

        void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            ReleaseAll();
            foreach (InputDevice d in new InputDevice[] { kb, mouse, pad })
                if (d != null && d.added) InputSystem.RemoveDevice(d);
            qaLog?.Flush(); telLog?.Flush();
            if (active == this) active = null;
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            qaLog = new StreamWriter(Path.Combine(outDir, "qa.log"), false, new UTF8Encoding(false))
{ AutoFlush = true };
            telLog = new StreamWriter(Path.Combine(outDir, "tel.log"), false, new UTF8Encoding(false)) { AutoFlush=true };
            Application.runInBackground=true;   
            try
            {
                var st=Object.Instantiate(InputSystem.settings);   
                st.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                st.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings=st;
            }
            catch (Exception e) { Q("WARN input settings not changed: " + e.Message); }
            kb = InputSystem.AddDevice<Keyboard>("QA Keyboard");
            Q(string.Format(IC, "READY unity={0} platform={1} scene={2} screen={3}x{4} batchmode={5} out={6}",
                Application.unityVersion, Application.platform, SceneManager.GetActiveScene().name, Screen.width, Screen.height,
                Application.isBatchMode, outDir));
            if (Application.isBatchMode) Q("WARN batch mode renders nothing: shots will be black or missing; run the player windowed");
            Resolve();
            lastFrameT=Time.unscaledTime;
            var list=Parse(steps);
            float t0=Time.unscaledTime;
            for (int i=0; i < list.Count; i++)
            {
                stepNo=i + 1;
                if (timeoutSec > 0 && Time.unscaledTime - t0 > timeoutSec) { Q("TIMEOUT after " + timeoutSec + " s"); break; }
                Q(string.Format(IC, "STEP {0}/{1} t={2:F2} frame={3} scene={4} :: {5}", i + 1, list.Count,
                    Time.unscaledTime - t0, Time.frameCount, SceneManager.GetActiveScene().name, list[i]));
                lock (recentLogs)
                { prevStepSeq = stepStartSeq; stepStartSeq = logSeq + 1; }
                IEnumerator e = null;
                try { e = Exec(list[i]); }
                catch (Exception ex)
                { Q("ERROR step '" + list[i] + "': " + ex.Message); }
                if (e != null)
                {
                    while (true)
                    {
                        object cur;
                        try { if (!e.MoveNext()) break; cur = e.Current; }
                        catch (Exception ex) { Q("ERROR step '" + list[i] + "': " + ex.GetType().Name + ": " + ex.Message); break; }
                        yield return cur;
                    }
                }
                if (quitRequested) break;
            }
            ReleaseAll();
            yield return null;
            Q(string.Format(IC, "PERF frames={0} avg_fps={1:F1} worst_frame_ms={2:F0}",framesTotal,
                framesTotal > 0 ? framesTotal / Mathf.Max(0.001f,sumFrame) : 0,worstFrame * 1000));
            Q(string.Format(IC, "DONE steps={0} shots={1} errors_logged={2} frames={3}", list.Count, shotN, errorsSeen, Time.frameCount));
            qaLog.Flush(); telLog.Flush();
            if (quitAtEnd && !Application.isEditor) { yield return null; Application.Quit(); }
        }

        bool quitRequested;

        static List<string> Parse(string s)
        {
            
            
            var r = new List<string>();
            foreach (var line in s.Replace("\r", "\n").Split('\n'))
            {
                var parts = new List<string>(); var cur = new StringBuilder(); char q = '\0';
                foreach (char ch in line)
                {
                    if (q != '\0')
{ if (ch == q) q = '\0'; cur.Append(ch); continue; }
                    if (ch == ';') { parts.Add(cur.ToString()); cur.Clear(); continue; }
                    if (ch == '"' || (ch == '\'' && (cur.Length == 0 || char.IsWhiteSpace(cur[cur.Length - 1])))) q = ch;
                    cur.Append(ch);
                }
                parts.Add(cur.ToString());
                if (q != '\0') parts=line.Split(';').ToList();
                foreach (var p in parts) { var t = p.Trim(); if (t.Length > 0 && !t.StartsWith("#")) r.Add(t); }
            }
            return r;
        }

        
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (framesTotal > 2) { worstFrame = Mathf.Max(worstFrame, dt); }
            sumFrame += dt; framesTotal++;
            if (keysDirty && kb != null) { InputSystem.QueueStateEvent(kb, new KeyboardState(keysDown.ToArray())); keysDirty = false; }
            if (mouse != null && (mouseDirty || lookFrames > 0))
            {
                var ms = new MouseState { position = mousePos, delta = lookFrames > 0 ? lookPerFrame : Vector2.zero, scroll = scrollOnce }
                    .WithButton(MouseButton.Left, lmb).WithButton(MouseButton.Right, rmb);
                InputSystem.QueueStateEvent(mouse, ms);
                scrollOnce = Vector2.zero; mouseDirty = lookFrames > 0;
                if (lookFrames > 0) lookFrames--;
            }
            if (pad != null && padDirty)
            {
                var gs = new GamepadState { leftStick = lStick, rightStick = rStick, leftTrigger = lTrig, rightTrigger = rTrig };
                foreach (var b in padDown) gs = gs.WithButton(b);
                InputSystem.QueueStateEvent(pad, gs);
                padDirty = false;
            }
            if (Time.unscaledTime >= nextResolve) Resolve();
            if (Time.unscaledTime >= nextTel) { nextTel = Time.unscaledTime + telEvery; Tel(); }
        }

        void ReleaseAll()
        {
            if (keysDown.Count > 0) { keysDown.Clear(); keysDirty = true; }
            if (lmb || rmb) { lmb = rmb = false; mouseDirty = true; }
            lookFrames = 0;
            if (pad != null) { lStick = rStick = Vector2.zero; lTrig = rTrig = 0; padDown.Clear(); padDirty = true; }
            if (kb != null && keysDirty) { InputSystem.QueueStateEvent(kb, new KeyboardState()); keysDirty = false; }
        }

        
        void Q(string line)
        {
            string l = "QA " + line;
            try { qaLog?.WriteLine(l); } catch { }
            Debug.Log(l);
        }

        void OnLog(string msg, string stack, LogType type)
        {
            if (msg == null || msg.StartsWith("QA ")) return;
            if (msg.StartsWith("TEL ")) { try { telLog?.WriteLine(msg); } catch { } return; }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errorsSeen++;
                if (errorsSeen <= 30)
                {
                    string first = (stack ?? "").Split('\n').FirstOrDefault(x => x.Trim().Length > 0) ?? "";
                    try { qaLog?.WriteLine("QA SEEN-" + type.ToString().ToUpperInvariant() + " " + Clip(msg, 300) + (first.Length > 0 ? "  @ " + Clip(first.Trim(), 160) : "")); } catch { }
                }
            }
            lock (recentLogs) { recentLogs.Add(new KeyValuePair<long, string>(++logSeq, msg)); if (recentLogs.Count > 400) recentLogs.RemoveRange(0, 100); }
        }

        static string Clip(string s, int n) => s == null ? "" : (s.Length > n ? s.Substring(0,n) + "..." : s).Replace('\n', ' ').Replace('\r', ' ');

        
        void Resolve()
        {
            nextResolve = Time.unscaledTime + (tracked.Count == 0 || Time.unscaledTime < 15f ? 1.5f : 6f);
            var found = new List<Transform>();
            var spec = string.IsNullOrWhiteSpace(trackSpec) ? "tag:Player" : trackSpec;
            Transform[] all = null;
            foreach (var tok0 in spec.Split(','))
            {
                var tok=tok0.Trim();
                if (tok.Length == 0) continue;
                if (tok.StartsWith("tag:",StringComparison.OrdinalIgnoreCase))
                {
                    try { foreach (var g in GameObject.FindGameObjectsWithTag(tok.Substring(4))) found.Add(g.transform); }
                    catch (UnityException) { }
                    continue;
                }
                if (all == null) all = FindAll<Transform>();
                bool prefix = tok.EndsWith("*");
                string n = prefix ? tok.Substring(0, tok.Length - 1) : tok;
                foreach (var t in all)
                    if (prefix ? t.name.StartsWith(n, StringComparison.OrdinalIgnoreCase) : string.Equals(t.name, n, StringComparison.OrdinalIgnoreCase))
                        found.Add(t);
            }
            
            var set = new HashSet<Transform>(found);
            tracked.Clear();
            foreach (var t in found)
            {
                bool child = false;
                for (var p = t.parent; p != null; p = p.parent) if (set.Contains(p)) { child = true; break; }
                if (!child && !tracked.Contains(t)) tracked.Add(t);
                if (tracked.Count >= 16) break;
            }
        }

        bool IsTracked(Transform t)
        {
            foreach (var r in tracked) if (r != null && (t == r || t.IsChildOf(r))) return true;
            return false;
        }

        void Tel()
        {
            float now = Time.time;
            for (int i = 0; i < tracked.Count; i++)
            {
                var t = tracked[i];
                if (t == null) continue;
                var p = t.position;
                float v = Speed(t, p);
                lastPos[t] = p;
                if (i == 0 && (travelled.Count == 0 || (travelled[travelled.Count - 1] - p).sqrMagnitude > 1f)) travelled.Add(p);
                var sb = new StringBuilder();
                sb.AppendFormat(IC, "TEL t={0:F2} who={1} x={2:F2} y={3:F2} z={4:F2} v={5:F2} yaw={6:F0}", now, t.name.Replace(' ', '_'),p.x,p.y,p.z,v,t.eulerAngles.y);
                if (i == 0) sb.Append(AnyInput ? " in=1" : " in=0");
                var extra = Extra(t);
                if (!string.IsNullOrEmpty(extra)) sb.Append(' ').Append(extra.Replace('\n', ' '));
                try { telLog?.WriteLine(sb.ToString()); } catch { }
            }
        }

        float Speed(Transform t, Vector3 p)
        {
#if QA_HAS_PHYSICS
            var rb = t.GetComponentInChildren<Rigidbody>();
            if (rb != null)
#if UNITY_6000_0_OR_NEWER
                return rb.linearVelocity.magnitude;
#else
                return rb.velocity.magnitude;
#endif
            var cc = t.GetComponent<CharacterController>();
            if (cc != null) return cc.velocity.magnitude;
#endif
#if QA_HAS_PHYSICS2D
            var rb2 = t.GetComponentInChildren<Rigidbody2D>();
            if (rb2 != null)
#if UNITY_6000_0_OR_NEWER
                return rb2.linearVelocity.magnitude;
#else
                return rb2.velocity.magnitude;
#endif
#endif
            return lastPos.TryGetValue(t, out var lp) ? (p - lp).magnitude / Mathf.Max(0.001f, telEvery) : 0f;
        }

        string Extra(Transform t)
        {
            foreach (var c in t.GetComponents<MonoBehaviour>())
            {
                if (c == null) continue;
                var ty = c.GetType();
                if (!telMethods.TryGetValue(ty, out var m))
                {
                    m = ty.GetMethod("QaTel", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                    if (m != null && m.ReturnType != typeof(string)) m = null;
                    telMethods[ty] = m;
                }
                if (m != null)
                { try { return (string)m.Invoke(c, null); } catch (Exception e)
                { return "qatel_error=" + e.GetType().Name; } }
            }
            return null;
        }

        
        static float F(string s) => float.Parse(s, NumberStyles.Float, IC);

        IEnumerator Exec(string step)
        {
            var w=Split(step);
            string c=w[0].ToLowerInvariant();
            switch (c)
            {
                case "wait": return WaitSec(F(w[1]));
                case "waitlog": return WaitLog(Rest(step, 1, w.Length > 2 && IsNum(w[w.Length - 1]) ? 1 : 0), w.Length > 2 && IsNum(w[w.Length - 1]) ? F(w[w.Length - 1]) : 30f);
                case "waitscene": return WaitScene(w[1], w.Length > 2 ? F(w[2]) : 60f);
                case "timescale": Time.timeScale=F(w[1]); return null;
                case "scene": SceneManager.LoadScene(w[1]); return WaitFrames(3);
                case "log": Q("NOTE " + Rest(step, 1, 0)); return null;
                case "shot": return Shot(w.Length > 1 ? w[1] : "shot");
                case "shots": return Shots(int.Parse(w[1], IC), F(w[2]), w.Length > 3 ? w[3] : "seq");
                case "record": return Record(F(w[1]), F(w[2]), w.Length > 3 ? w[3] : "clip");
                case "key": return PressKeys(w.Skip(1).Select(ParseKey).ToArray(), 0.12f);
                case "down": SetKey(ParseKey(w[1]), true); return WaitFrames(1);
                case "up": SetKey(ParseKey(w[1]), false); return WaitFrames(1);
                case "hold": return Moving(PressKeys(new[] { ParseKey(w[1]) },F(w[2])),step);
                case "mouse": EnsureMouse(); mousePos = ScreenPt(w[1], w[2]); mouseDirty = true; return WaitFrames(2);
                case "click": return Click(step, w, false);
                case "rclick": return Click(step, w, true);
                case "mdown": EnsureMouse(); lmb = true; mouseDirty = true; return WaitFrames(2);
                case "mup": EnsureMouse(); lmb = false; mouseDirty = true; return WaitFrames(2);
                case "look": return Look(F(w[1]), F(w[2]), w.Length > 3 ? F(w[3]) : 0.3f);
                case "scroll": EnsureMouse(); scrollOnce=new Vector2(0, F(w[1])); mouseDirty=true; return WaitFrames(2);
                case "stick": return w.Length > 4 ? Moving(Stick(w[1],F(w[2]),F(w[3]),F(w[4])),step) : Stick(w[1],F(w[2]),F(w[3]),-1f);
                case "trigger": return w.Length > 3 ? Moving(Trigger(w[1], F(w[2]), F(w[3])), step) : Trigger(w[1], F(w[2]), -1f);
                case "pad": return PadButton(ParsePad(w[1]), 0.12f);
                case "padhold": return Moving(PadButton(ParsePad(w[1]), F(w[2])), step);
                case "tel": Tel(); return null;
                case "audit": return Audit(w);
                case "quit": quitRequested = true; return null;
                default: Q("ERROR unknown step '" + step + "'"); return null;
            }
        }

        static bool IsNum(string s) => float.TryParse(s,NumberStyles.Float,IC,out _);

        static string[] Split(string s)
        {
            
            var r = new List<string>(); var cur = new StringBuilder(); char q = '\0';
            foreach (char ch in s)
            {
                if (q != '\0')
{ if (ch == q) q = '\0'; else cur.Append(ch); continue; }
                if (ch == '"' || (ch == '\'' && cur.Length == 0)) { q = ch; continue; }
                if (char.IsWhiteSpace(ch)) { if (cur.Length > 0) { r.Add(cur.ToString()); cur.Clear(); } continue; }
                cur.Append(ch);
            }
            if (cur.Length > 0) r.Add(cur.ToString());
            return r.ToArray();
        }

        static string Rest(string step, int skipWords, int dropLast)
        {
            var w = Split(step);
            return string.Join(" ", w.Skip(skipWords).Take(Math.Max(0, w.Length - skipWords - dropLast)));
        }

        IEnumerator WaitSec(float s)
        { float end = Time.unscaledTime + s; while (Time.unscaledTime < end) yield return null; }
        IEnumerator WaitFrames(int n)
        { for (int i = 0; i < n; i++) yield return null; }

        IEnumerator WaitLog(string text, float timeout)
        {
            float end = Time.unscaledTime + timeout;
            while (Time.unscaledTime < end)
            {
                lock (recentLogs) if (recentLogs.Any(m => m.Key >= prevStepSeq && m.Value.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0))
                { Q("SEEN log '" + text + "'"); yield break; }
                yield return null;
            }
            Q("WARN waitlog timed out: '" + text + "'");
        }

        IEnumerator WaitScene(string name, float timeout)
        {
            float end = Time.unscaledTime + timeout;
            while (Time.unscaledTime < end && !string.Equals(SceneManager.GetActiveScene().name, name, StringComparison.OrdinalIgnoreCase)) yield return null;
            Q(string.Equals(SceneManager.GetActiveScene().name, name, StringComparison.OrdinalIgnoreCase) ? "SEEN scene " + name : "WARN waitscene timed out: " + name);
        }

        static readonly Dictionary<string,string> KeyAlias = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)
        {
            { "up", "UpArrow" }, { "down", "DownArrow" },{ "left", "LeftArrow" }, { "right", "RightArrow" },{ "shift", "LeftShift" },
            { "ctrl", "LeftCtrl" },{ "control", "LeftCtrl" }, { "alt", "LeftAlt" },{ "esc", "Escape" }, { "return", "Enter" },
            { "spacebar", "Space" }, { "0", "Digit0" },{ "1", "Digit1" }, { "2", "Digit2" },{ "3", "Digit3" }, { "4", "Digit4" },
            { "5", "Digit5" }, { "6", "Digit6" },{ "7", "Digit7" }, { "8", "Digit8" },{ "9", "Digit9" }, { "lshift", "LeftShift" },
            { "rshift", "RightShift" }, { "backspace", "Backspace" },{ "del", "Delete" },
        };

        static Key ParseKey(string s)
        {
            if (KeyAlias.TryGetValue(s, out var a)) s = a;
            if (Enum.TryParse(s, true, out Key k) && k != Key.None) return k;
            throw new ArgumentException("unknown key '" + s + "' (use Input System Key names)");
        }

        static GamepadButton ParsePad(string s)
        {
            if (Enum.TryParse(s,true,out GamepadButton b)) return b;
            throw new ArgumentException("unknown gamepad button '" + s + "'");
        }

        void SetKey(Key k, bool on)
        { if (on ? keysDown.Add(k) : keysDown.Remove(k)) keysDirty = true; }

IEnumerator Moving(IEnumerator inner, string step)
        {
            var t = tracked.Count > 0 ? tracked[0] : null;
            Vector3 p0 = t != null ? t.position : Vector3.zero, f0 = t != null ? t.forward : Vector3.forward;
            float y0 = t != null ? t.eulerAngles.y : 0f;
            string also = string.Join("+", keysDown.Select(k => k.ToString()));
            while (inner.MoveNext()) yield return inner.Current;     
            if (t == null) yield break;
            var d=t.position - p0;
            var fwd=new Vector3(f0.x, 0f, f0.z).normalized;
            var right=new Vector3(fwd.z, 0f, -fwd.x);
            float along=Vector3.Dot(d, fwd), side=Vector3.Dot(d, right), turn=Mathf.DeltaAngle(y0, t.eulerAngles.y);
            Q(string.Format(IC, "MOVE {0} during '{1}'{2}: {3:F1} m {4}, {5:F1} m to the {6} of its facing at the start; heading {7:+0;-0;0} deg ({8})",
                t.name,step.Trim(),also.Length > 0 ? " with " + also + " held" : "", Mathf.Abs(along), along < 0 ? "backward" : "forward",
                Mathf.Abs(side), side < 0 ? "left" : "right", turn, Mathf.Abs(turn) < 5f ? "straight" : turn > 0 ? "turned right" : "turned left"));
        }

        IEnumerator PressKeys(Key[] keys, float sec)
        {
            foreach (var k in keys) SetKey(k, true);
            yield return null; yield return null;
            float end = Time.unscaledTime + Mathf.Max(0.05f, sec);
            while (Time.unscaledTime < end) yield return null;
            foreach (var k in keys) SetKey(k, false);
            yield return null; yield return null;
        }

        void EnsureMouse()
        {
            if (mouse != null) return;
            mouse = InputSystem.AddDevice<Mouse>("QA Mouse");
            mousePos=new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            mouseDirty=true;
        }

        void EnsurePad() { if (pad == null) { pad=InputSystem.AddDevice<Gamepad>("QA Gamepad"); padDirty = true; } }

        
        static Vector2 ScreenPt(string xs,string ys)
        {
            float x = F(xs),y = F(ys);
            if (x <= 1f && y <= 1f) { x *= Screen.width; y *= Screen.height; }
            return new Vector2(x,Screen.height - y);
        }

        IEnumerator Click(string step,string[] w,bool right)
        {
            EnsureMouse();
            if (w.Length > 1 && w[1].StartsWith("@"))
            {
                string q = Rest(step, 1, 0).Substring(1);
                if (!FindUi(q, out var center, out var what)) { Q("ERROR click: no visible UI element named or labelled '" + q + "'"); yield break; }
                mousePos=center;
                Q(string.Format(IC, "CLICK '{0}' -> {1} at {2:F0},{3:F0} (pixels from the top-left)",q,what,center.x,Screen.height - center.y));
            }
            else mousePos = ScreenPt(w[1],w[2]);
            mouseDirty = true;
            yield return null; yield return null;
            if (right) rmb = true; else lmb = true;
            mouseDirty = true;
            yield return null; yield return null; yield return new WaitForSecondsRealtime(0.06f);
            if (right) rmb = false; else lmb = false;
            mouseDirty = true;
            yield return null; yield return null;
        }

        IEnumerator Look(float dx,float dy,float sec)
        {
            EnsureMouse();
            int frames = Mathf.Max(1,Mathf.RoundToInt(sec / Mathf.Max(0.005f,Time.unscaledDeltaTime)));
            lookPerFrame = new Vector2(dx / frames,dy / frames);
            lookFrames = frames; mouseDirty = true;
            while (lookFrames > 0) yield return null;
            yield return null;
        }

        IEnumerator Stick(string which,float x,float y,float sec)
        {
            EnsurePad();
            var v = Vector2.ClampMagnitude(new Vector2(x,y),1f);
            if (which.StartsWith("r", StringComparison.OrdinalIgnoreCase)) rStick = v; else lStick = v;
            padDirty = true;
            if (sec < 0) { yield return null; yield break; }
            yield return new WaitForSecondsRealtime(sec);
            if (which.StartsWith("r", StringComparison.OrdinalIgnoreCase)) rStick = Vector2.zero; else lStick = Vector2.zero;
            padDirty = true; yield return null;
        }

        IEnumerator Trigger(string which, float v, float sec)
        {
            EnsurePad();
            v = Mathf.Clamp01(v);
            if (which.StartsWith("r", StringComparison.OrdinalIgnoreCase)) rTrig=v; else lTrig=v;
            padDirty=true;
            if (sec < 0) { yield return null; yield break; }
            yield return new WaitForSecondsRealtime(sec);
            if (which.StartsWith("r",StringComparison.OrdinalIgnoreCase)) rTrig = 0; else lTrig = 0;
            padDirty = true; yield return null;
        }

        IEnumerator PadButton(GamepadButton b,float sec)
        {
            EnsurePad();
            padDown.Add(b); padDirty = true;
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(Mathf.Max(0.05f,sec));
            padDown.Remove(b); padDirty = true;
            yield return null; yield return null;
        }

        
        IEnumerator Shot(string name)
        {
            shotN++;
            string safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());
            string path=Path.Combine(outDir, string.Format(IC, "shot_{0:D2}_{1}.png",shotN,safe));
#if QA_HAS_SCREENCAPTURE
            if (Application.isBatchMode) { Q("WARN shot skipped in batch mode: " + path); yield break; }
            yield return new WaitForEndOfFrame();
#if QA_HAS_IMAGECONVERSION
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try { File.WriteAllBytes(path, tex.EncodeToPNG()); }
            finally { Destroy(tex); }
#else
            ScreenCapture.CaptureScreenshot(path);
            float end = Time.unscaledTime + 3f;
            while (!File.Exists(path) && Time.unscaledTime < end) yield return null;
#endif
            Q(string.Format(IC, "SHOT {0} frame={1} scene={2} {3}", path, Time.frameCount, SceneManager.GetActiveScene().name, ShotContext()));
#else
            Q("ERROR shot: the project has no Screen Capture module (Package Manager > Built-in > Screen Capture)");
            yield break;
#endif
        }

        string ShotContext()
        {
            if (tracked.Count == 0 || tracked[0] == null) return "";
            var p = tracked[0].position;
            return string.Format(IC, "{0}={1:F1},{2:F1},{3:F1}", tracked[0].name.Replace(' ', '_'), p.x, p.y, p.z);
        }

        IEnumerator Shots(int n, float interval, string name)
        {
            for (int i=0; i < n; i++)
            {
                var e=Shot(name + "_" + i);
                while (e.MoveNext()) yield return e.Current;
                if (i < n - 1) yield return new WaitForSecondsRealtime(interval);
            }
        }

        IEnumerator Record(float sec,float fps,string name)
        {
#if QA_HAS_SCREENCAPTURE
            if (Application.isBatchMode) { Q("WARN record skipped in batch mode"); yield break; }
            shotN++;
            string safe = new string(name.Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_').ToArray());
            string prefix = Path.Combine(outDir,string.Format(IC, "rec_{0:D2}_{1}_", shotN, safe));
            fps = Mathf.Max(1f, fps);
            int want = Mathf.Max(1, Mathf.RoundToInt(sec * fps)), got = 0;
            float t0 = Time.unscaledTime;
            for (int i = 0; i < want; i++)
            {
                float due = t0 + i / fps;
                while (Time.unscaledTime < due) yield return null;
                yield return new WaitForEndOfFrame();
#if QA_HAS_IMAGECONVERSION
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                try { File.WriteAllBytes(prefix + i.ToString("D4", IC) + ".png", tex.EncodeToPNG()); got++; }
                finally { Destroy(tex); }
#else
                ScreenCapture.CaptureScreenshot(prefix + i.ToString("D4",IC) + ".png"); got++;
                yield return null;
#endif
            }
            Q(string.Format(IC, "RECORD {0}*.png frames={1} fps={2} seconds={3:F1} captured_fps={4:F1}", prefix, got, fps, got / fps, got / Mathf.Max(0.001f, Time.unscaledTime - t0)));
#else
            Q("ERROR record: the project has no Screen Capture module (Package Manager > Built-in > Screen Capture)");
            yield break;
#endif
        }

        
        IEnumerator Audit(string[] w)
        {
            string what=w.Length > 1 ? w[1].ToLowerInvariant() : "";
            switch (what)
            {
#if QA_HAS_PHYSICS
                case "colliders": AuditColliders(w.Length > 2 ? F(w[2]) : 250f); return null;
                case "near": AuditNear(ParseV3(w[2]), w.Length > 3 ? F(w[3]) : 4f); return null;
                case "route": AuditRoute(w); return null;
#else
                case "colliders": case "near": case "route": Q("ERROR audit " + what + ": the project has no 3D Physics module"); return null;
#endif
                case "hud": return AuditHudAll(w.Length > 2 ? w[2] : null);
                default: Q("ERROR audit needs colliders | near | route | hud"); return null;
            }
        }

        static Vector3 ParseV3(string s)
        {
            var p = s.Split(',');
            return new Vector3(F(p[0]), F(p[1]), F(p[2]));
        }

        static T[] FindAll<T>() where T : Object
        {
#if UNITY_2022_2_OR_NEWER
#pragma warning disable CS0618 
            return Object.FindObjectsByType<T>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#pragma warning restore CS0618
#else
            return Object.FindObjectsOfType<T>();
#endif
        }

        static string PathOf(Transform t)
        {
            var s=t.name;
            for (var p=t.parent; p != null && s.Length < 90; p=p.parent) s=p.name + "/" + s;
            return s.Replace(' ', '_');
        }

        Vector3 Ref() => tracked.Count > 0 && tracked[0] != null ? tracked[0].position : Vector3.zero;

#if QA_HAS_PHYSICS
        
        static bool IsTerrain(Collider c) => c != null && c.GetType().Name == "TerrainCollider";
        static bool IsWheel(Collider c) => c != null && c.GetType().Name == "WheelCollider";

        static bool IsHit(Collider c) => c != null && c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy;

bool SolidAt(Bounds b)
        {
            float[] hs = { 0.25f,0.5f,0.8f };
            Vector3[] dirs = { Vector3.right,Vector3.left,Vector3.forward,Vector3.back };
            foreach (var hf in hs)
            {
                float y = Mathf.Min(b.min.y + Mathf.Max(0.5f,b.size.y * hf),b.max.y - 0.05f);
                var c = new Vector3(b.center.x,y,b.center.z);
                foreach (var d in dirs)
                {
                    float ext = Mathf.Abs(Vector3.Dot(b.extents,d)) + 0.5f;
                    var o = c - d * ext;
                    foreach (var h in Physics.RaycastAll(o,d,ext,~0,QueryTriggerInteraction.Ignore))
                        if (IsHit(h.collider) && !IsTerrain(h.collider) && !IsTracked(h.collider.transform) &&
                            h.collider.bounds.Intersects(b)) return true;
                }
            }
            
            for (int k = 0; k < 5; k++)
            {
                var off = k == 0 ? Vector3.zero : new Vector3((k % 2 == 0 ? 0.25f : -0.25f) * b.size.x,0,(k < 3 ? 0.25f : -0.25f) * b.size.z);
                var top = new Vector3(b.center.x,b.max.y + 0.5f,b.center.z) + off;
                foreach (var h in Physics.RaycastAll(top,Vector3.down,b.size.y + 1f,~0,QueryTriggerInteraction.Ignore))
                    if (IsHit(h.collider) && !IsTerrain(h.collider) && !IsTracked(h.collider.transform) && h.collider.bounds.Intersects(b)) return true;
            }
            return false;
        }

        void AuditColliders(float maxDist)
        {
            var rf = Ref();
            var see = new List<KeyValuePair<float,string>>();
            int checkedN = 0;
            foreach (var r in FindAll<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r is SpriteRenderer || r is BillboardRenderer) continue;
                if (IsTracked(r.transform)) continue;
                var b = r.bounds; var sz = b.size;
                if (Mathf.Max(sz.x,sz.z) < 0.4f || sz.y < 0.3f) continue;   
                float dist = Vector3.Distance(rf,b.ClosestPoint(rf));
                if (dist > maxDist) continue;
                checkedN++;
                if (!SolidAt(b))
                    see.Add(new KeyValuePair<float,string>(dist,string.Format(IC, "SEE-NOT-HIT {0} dist={1:F0}m size={2:F1}x{3:F1}x{4:F1} at {5:F1},{6:F1},{7:F1}",
                        PathOf(r.transform), dist, sz.x, sz.y, sz.z, b.center.x, b.center.y, b.center.z)));
            }
            var hidden = new List<KeyValuePair<float, string>>();
            var renderers = FindAll<Renderer>().Where(x => x.enabled).Select(x => x.bounds).ToList();
            int neg = 0;
            foreach (var c in FindAll<Collider>())
            {
                if (!IsHit(c) || IsTerrain(c) || c is CharacterController || IsWheel(c) || IsTracked(c.transform)) continue;
                var s = c.transform.lossyScale;
                if (s.x < 0 || s.y < 0 || s.z < 0) { neg++; if (neg <= 10) Q("AUDIT NEGATIVE-SCALE " + PathOf(c.transform) + " scale=" + s.ToString("F2")); }
                var b = c.bounds;
                if (b.size.magnitude < 0.6f) continue;
                if (renderers.Any(rb => rb.Intersects(b))) continue;
                float dist = Vector3.Distance(rf,b.ClosestPoint(rf));
                if (dist > maxDist) continue;
                hidden.Add(new KeyValuePair<float,string>(dist,string.Format(IC, "HIDDEN-BLOCKER {0} {1} dist={2:F0}m size={3:F1}x{4:F1}x{5:F1}",
                    PathOf(c.transform), c.GetType().Name, dist, b.size.x, b.size.y, b.size.z)));
            }
            foreach (var kv in see.OrderBy(k => k.Key).Take(40)) Q("AUDIT " + kv.Value);
            foreach (var kv in hidden.OrderBy(k => k.Key).Take(20)) Q("AUDIT " + kv.Value);
            Q(string.Format(IC, "AUDIT colliders: {0} renderers within {1:F0} m checked, SEE-NOT-HIT={2}, HIDDEN-BLOCKER={3} (boundaries may be intended), NEGATIVE-SCALE={4}",
                checkedN,maxDist,see.Count,hidden.Count,neg));
        }

        string Describe(Collider c)
        {
            var sb = new StringBuilder();
            var b = c.bounds;
            sb.AppendFormat(IC, "{0} {1}{2} layer={3} size={4:F1}x{5:F1}x{6:F1} centre={7:F1},{8:F1},{9:F1}", PathOf(c.transform), c.GetType().Name,
                c.isTrigger ? " trigger" : "", LayerMask.LayerToName(c.gameObject.layer), b.size.x, b.size.y, b.size.z, b.center.x, b.center.y, b.center.z);
            var m=c.sharedMaterial;
            if (m != null) sb.AppendFormat(IC, " material={0} friction={1:F2}/{2:F2} {3} bounce={4:F2}",m.name,m.dynamicFriction,m.staticFriction,m.frictionCombine,m.bounciness);
            else sb.Append(" material=default(friction 0.6)");
            if (c is MeshCollider mc) sb.Append(mc.convex ? " convex" : " concave");
            var rb=c.attachedRigidbody;
            if (rb != null) sb.AppendFormat(IC, " rigidbody={0}{1} mass={2:F0}",rb.name,rb.isKinematic ? " kinematic" : "", rb.mass);
            return sb.ToString();
        }

        void AuditNear(Vector3 p, float r)
        {
            var cols = Physics.OverlapSphere(p, r, ~0, QueryTriggerInteraction.Collide);
            foreach (var c in cols.OrderBy(c => Vector3.Distance(p, c.ClosestPointOnBounds(p))).Take(30))
                Q(string.Format(IC, "AUDIT NEAR {0:F1}m {1}{2}", Vector3.Distance(p, c.ClosestPointOnBounds(p)), IsTracked(c.transform) ? "[tracked] " : "", Describe(c)));
            int bare = 0;
            foreach (var rd in FindAll<Renderer>())
            {
                if (!rd.enabled || rd is ParticleSystemRenderer) continue;
                var b = rd.bounds;
                if ((b.ClosestPoint(p) - p).magnitude > r || b.size.y < 0.3f || IsTracked(rd.transform)) continue;
                if (!SolidAt(b)) { bare++; if (bare <= 10) Q("AUDIT NEAR SEE-NOT-HIT " + PathOf(rd.transform)); }
            }
            Q(string.Format(IC, "AUDIT near {0:F1},{1:F1},{2:F1} r={3}: {4} colliders, {5} drawn objects without collision", p.x, p.y, p.z, r, cols.Length, bare));
        }

        void AuditRoute(string[] w)
        {
            string src=w.Length > 2 ? w[2] : "tel";
            float width = w.Length > 3 && IsNum(w[3]) ? F(w[3]) : 4f;
            float height = w.Length > 4 && IsNum(w[4]) ? F(w[4]) : 1.5f;
            bool loop = w.Any(x => x.Equals("loop", StringComparison.OrdinalIgnoreCase));
            var pts = new List<Vector3>();
            if (src.Equals("tel", StringComparison.OrdinalIgnoreCase)) pts.AddRange(travelled);
            else if (src.StartsWith("pts:", StringComparison.OrdinalIgnoreCase))
                foreach (var s in src.Substring(4).Split('|')) pts.Add(ParseV3(s));
            else
            {
                var go = FindAll<Transform>().FirstOrDefault(t => string.Equals(t.name,src,StringComparison.OrdinalIgnoreCase));
                if (go == null) { Q("ERROR audit route: no active object named " + src); return; }
                var lr = go.GetComponent<LineRenderer>();
                if (lr != null && lr.positionCount > 1)
                {
                    var a = new Vector3[lr.positionCount]; lr.GetPositions(a);
                    pts.AddRange(lr.useWorldSpace ? a : a.Select(x => go.TransformPoint(x)));
                    loop |= lr.loop;
                }
                else foreach (Transform ch in go) pts.Add(ch.position);
            }
            if (pts.Count < 2) { Q("ERROR audit route: fewer than 2 points from " + src); return; }
            int n = loop ? pts.Count : pts.Count - 1;
            var seen = new Dictionary<Collider, string>();
            for (int i = 0; i < n; i++)
            {
                Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
                var d = b - a; float len = d.magnitude;
                if (len < 0.05f) continue;
                var rot = Quaternion.LookRotation(d / len, Vector3.up);
                var center = (a + b) * 0.5f + Vector3.up * (0.35f + height * 0.5f);   
                var half = new Vector3(width * 0.5f, height * 0.5f, len * 0.5f);
                foreach (var c in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!IsHit(c) || IsTracked(c.transform) || seen.ContainsKey(c)) continue;
                    if (c.bounds.max.y < Mathf.Min(a.y, b.y) + 0.35f) continue;   
                    var cp = c.ClosestPointOnBounds((a + b) * 0.5f);
                    var off = Vector3.Cross(d / len, Vector3.up);
                    float side = Vector3.Dot(cp - a, off);
                    seen[c] = string.Format(IC, "BLOCK seg={0} at {1:F1},{2:F1},{3:F1} offset={4:F1}m {5}", i, cp.x, cp.y, cp.z, side, Describe(c));
                }
            }
            foreach (var kv in seen.Take(40)) Q("AUDIT " + kv.Value);
            Q(string.Format(IC, "AUDIT route {0}: {1} points, {2} segments, corridor {3:F1} m wide x {4:F1} m high, BLOCK={5}", src, pts.Count, n, width, height, seen.Count));
        }
#endif

        
        struct UiText { public string who, text; public Rect r; public Transform t; public bool overflow, nofont; public float px; }
        int hudSkipInactive, hudSkipEmpty, hudSkipClear;

        IEnumerator AuditHudAll(string sizes)
        {
            AuditHud("current");
            if (string.IsNullOrEmpty(sizes) || Application.isEditor) yield break;
            int w0 = Screen.width, h0 = Screen.height; var mode = Screen.fullScreenMode;
            foreach (var s in sizes.Split(','))
            {
                var p=s.ToLowerInvariant().Split('x');
                if (p.Length != 2) continue;
                Screen.SetResolution(int.Parse(p[0],IC),int.Parse(p[1],IC),FullScreenMode.Windowed);
                for (int i = 0; i < 6; i++) yield return null;
                AuditHud(Screen.width + "x" + Screen.height);
            }
            Screen.SetResolution(w0, h0, mode);
            for (int i = 0; i < 4; i++) yield return null;
        }

        void AuditHud(string label)
        {
            var items = new List<UiText>();
            hudSkipInactive = hudSkipEmpty = hudSkipClear = 0;
            CollectUgui(items);
#if QA_HAS_UIELEMENTS
            CollectUitk(items);
#endif
            var scr = new Rect(0, 0, Screen.width, Screen.height);
            int bad = 0;
            foreach (var it in items)
            {
                string pos = string.Format(IC, "{0:F0},{1:F0} {2:F0}x{3:F0}", it.r.x, Screen.height - it.r.yMax, it.r.width, it.r.height);
                Q(string.Format(IC, "HUD [{0}] TEXT \"{1}\" at {2} ({3}){4}", label, Clip(it.text, 60), pos, it.who, it.px > 0 ? string.Format(IC, " {0:F0}px",it.px) : ""));
                if (it.r.xMin < scr.xMin - 2 || it.r.yMin < scr.yMin - 2 || it.r.xMax > scr.xMax + 2 || it.r.yMax > scr.yMax + 2) { bad++; Q("HUD [" + label + "] OFFSCREEN \"" + Clip(it.text, 40) + "\" " + pos); }
                if (it.nofont) { bad++; Q("HUD [" + label + "] NOFONT \"" + Clip(it.text, 40) + "\" has no font asset and draws nothing (TextMeshPro: import TMP Essential Resources, QaBuild.ImportTmpEssentials) " + pos); }
                if (it.overflow) { bad++; Q("HUD [" + label + "] OVERFLOW \"" + Clip(it.text, 40) + "\" text does not fit its box " + pos); }
                if (it.px > 0 && it.px * 1080f / Mathf.Max(1, Screen.height) < 13f)
                { bad++; Q(string.Format(IC, "HUD [{0}] TINY \"{1}\" {2:F0}px (under ~13 px at 1080p)", label, Clip(it.text, 40), it.px)); }
            }
            for (int i=0; i < items.Count; i++)
                for (int j=i + 1; j < items.Count; j++)
                {
                    var a=items[i]; var b=items[j];
                    if (a.t != null && b.t != null && (a.t.IsChildOf(b.t) || b.t.IsChildOf(a.t))) continue;
                    float ix=Mathf.Min(a.r.xMax, b.r.xMax) - Mathf.Max(a.r.xMin, b.r.xMin), iy=Mathf.Min(a.r.yMax, b.r.yMax) - Mathf.Max(a.r.yMin, b.r.yMin);
                    if (ix <= 0 || iy <= 0) continue;
                    float small=Mathf.Min(a.r.width * a.r.height, b.r.width * b.r.height);
                    if (small > 1 && ix * iy / small > 0.15f) { bad++; Q("HUD [" + label + "] OVERLAP \"" + Clip(a.text, 30) + "\" and \"" + Clip(b.text, 30) + "\""); }
                }
            Q(string.Format(IC, "HUD [{0}] {1} visible texts, {2} problem(s), screen {3}x{4}{5}",label,items.Count,bad,Screen.width,Screen.height,
                hudSkipInactive + hudSkipEmpty + hudSkipClear > 0 ? string.Format(IC, " (not counted: {0} inactive, {1} empty, {2} transparent)", hudSkipInactive, hudSkipEmpty, hudSkipClear) : ""));
        }

        static Type graphicType; static bool graphicLooked;
        static Type GraphicType()
        {
            if (!graphicLooked)
            {
                graphicLooked = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("UnityEngine.UI.Graphic", false);
                    if (t != null) { graphicType=t; break; }
                }
            }
            return graphicType;
        }

        static bool HasProp(object o, string name)
        {
            try { return o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public) != null; }
            catch (AmbiguousMatchException) { return true; }
        }

        static object Prop(object o, string name)
        {
            try { return o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public)?.GetValue(o, null); }
            catch { return null; }   
        }

        void CollectUgui(List<UiText> items)
        {
            var gt=GraphicType();
            if (gt == null) return;
            var corners=new Vector3[4];
            foreach (var canvas in FindAll<Canvas>())
            {
                if (!canvas.isRootCanvas || !canvas.enabled || canvas.renderMode == RenderMode.WorldSpace) continue;
                var cam=canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
                foreach (Component g in canvas.GetComponentsInChildren(gt, false))
                {
                    if (g.GetType().Name.IndexOf("Text",StringComparison.Ordinal) < 0) continue;
                    if (!(g is Behaviour beh) || !beh.isActiveAndEnabled) { hudSkipInactive++; continue; }
                    var text = Prop(g, "text") as string;
                    if (string.IsNullOrWhiteSpace(text)) { hudSkipEmpty++; continue; }
                    if (Prop(g, "color") is Color col && col.a < 0.05f)
{ hudSkipClear++; continue; }
                    var cg = g.GetComponentInParent<CanvasGroup>();
                    if (cg != null && cg.alpha < 0.05f)
                    { hudSkipClear++; continue; }
                    bool nofont = HasProp(g, "font") && Prop(g, "font") == null;
                    var rt = (RectTransform)g.transform;
                    rt.GetWorldCorners(corners);
                    Vector2 a = RectTransformUtility.WorldToScreenPoint(cam,corners[0]),c = RectTransformUtility.WorldToScreenPoint(cam,corners[2]);
                    var r = Rect.MinMaxRect(Mathf.Min(a.x,c.x),Mathf.Min(a.y,c.y),Mathf.Max(a.x,c.x),Mathf.Max(a.y,c.y));
                    bool overflow = Prop(g, "isTextOverflowing") is bool ov && ov;
                    float px = 0;
                    var fs = Prop(g, "fontSize");
                    if (fs is float f1) px = f1 * Mathf.Abs(rt.lossyScale.y) * (cam == null ? 1f : Screen.height / Mathf.Max(0.01f, 2f * cam.orthographicSize));
                    else if (fs is int i1) px = i1 * Mathf.Abs(rt.lossyScale.y);
                    if (cam != null && !cam.orthographic) px = 0;   
                    items.Add(new UiText { who = g.GetType().Name + " " + PathOf(g.transform), text=text, r=r, t=g.transform, overflow=overflow, nofont=nofont, px=px });
                }
            }
        }

#if QA_HAS_UIELEMENTS
        void CollectUitk(List<UiText> items)
        {
            foreach (var doc in FindAll<UnityEngine.UIElements.UIDocument>())
            {
                if (!doc.isActiveAndEnabled) continue;
                var root=doc.rootVisualElement;
                if (root == null || root.panel == null) continue;
                var panelRect=root.panel.visualTree.worldBound;
                if (panelRect.width < 1 || panelRect.height < 1) continue;
                float sx=Screen.width / panelRect.width, sy=Screen.height / panelRect.height;
                foreach (var l in UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.TextElement>(root).ToList())
                {
                    if (string.IsNullOrWhiteSpace(l.text) || !l.visible || l.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None || l.resolvedStyle.opacity < 0.05f) continue;
                    bool hidden=false;
                    for (var p=l.parent; p != null; p=p.parent) if (!p.visible || p.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None || p.resolvedStyle.opacity < 0.05f) { hidden=true; break; }
                    if (hidden) continue;
                    var wb=l.worldBound;
                    var r=new Rect(wb.x * sx, Screen.height - (wb.y + wb.height) * sy, wb.width * sx, wb.height * sy);
                    var need=l.MeasureTextSize(l.text, 0, UnityEngine.UIElements.VisualElement.MeasureMode.Undefined, 0, UnityEngine.UIElements.VisualElement.MeasureMode.Undefined);
                    bool wrap=l.resolvedStyle.whiteSpace != UnityEngine.UIElements.WhiteSpace.NoWrap;
                    bool overflow=!wrap && need.x > wb.width + 1.5f;
                    items.Add(new UiText { who="UITK " + l.GetType().Name + " " + (l.name ?? ""), text = l.text, r = r, t = null, overflow = overflow, px = l.resolvedStyle.fontSize * sy });
                }
            }
        }
#endif

        bool FindUi(string q, out Vector2 center, out string what)
        {
            center = default; what = null;
            var gt = GraphicType();
            var corners = new Vector3[4];
            if (gt != null)
                foreach (var canvas in FindAll<Canvas>())
                {
                    if (!canvas.isRootCanvas || !canvas.enabled || canvas.renderMode == RenderMode.WorldSpace) continue;
                    var cam = canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
                    foreach (Component g in canvas.GetComponentsInChildren(gt, false))
                    {
                        if (!(g is Behaviour beh) || !beh.isActiveAndEnabled) continue;
                        var text = Prop(g, "text") as string;
                        bool hit=g.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || (text != null && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!hit) continue;
                        
                        Transform target=g.transform;
                        for (var p=g.transform; p != null; p=p.parent)
                            if (p.GetComponents<Component>().Any(x => x != null && x.GetType().Name is string n && (n == "Button" || n == "Toggle" || n.EndsWith("Button"))))
{ target = p; break; }
                        ((RectTransform)target).GetWorldCorners(corners);
                        center = RectTransformUtility.WorldToScreenPoint(cam, (corners[0] + corners[2]) * 0.5f);
                        what = "uGUI " + PathOf(target);
                        return true;
                    }
                }
#if QA_HAS_UIELEMENTS
            foreach (var doc in FindAll<UnityEngine.UIElements.UIDocument>())
            {
                var root=doc.isActiveAndEnabled ? doc.rootVisualElement : null;
                if (root == null || root.panel == null) continue;
                var pr=root.panel.visualTree.worldBound;
                foreach (var e in UnityEngine.UIElements.UQueryExtensions.Query<UnityEngine.UIElements.VisualElement>(root).ToList())
                {
                    string text=(e as UnityEngine.UIElements.TextElement)?.text;
                    bool hit=(!string.IsNullOrEmpty(e.name) && e.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) || (text != null && text.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!hit || !e.visible || e.resolvedStyle.display == UnityEngine.UIElements.DisplayStyle.None) continue;
                    var wb=e.worldBound;
                    center=new Vector2((wb.x + wb.width / 2) * Screen.width / pr.width, Screen.height - (wb.y + wb.height / 2) * Screen.height / pr.height);
                    what="UITK " + e.GetType().Name + " " + e.name;
                    return true;
                }
            }
#endif
            return false;
        }
    }
}
#endif
