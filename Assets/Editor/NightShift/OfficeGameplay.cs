#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
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
        static readonly Vector3[] PatrolRoute =
        {
            new Vector3(-17f, 0, 6.5f), new Vector3(-14f, 0, 6.5f), new Vector3(-14f, 0, 3.9f), new Vector3(-6.5f, 0, 3.9f),
            new Vector3(-6.6f, 0, -2.9f), new Vector3(0f, 0, -2.9f), new Vector3(7.6f, 0, -2.9f), new Vector3(7.6f, 0, -10f),
            new Vector3(14f, 0, -10.2f), new Vector3(18.3f, 0, -3f), new Vector3(14f, 0, 3.9f), new Vector3(14f, 0, 6.5f),
            new Vector3(7f, 0, 6.5f), new Vector3(0f, 0, 6.5f), new Vector3(-8f, 0, 6.5f),
        };

        static BuildRefs Gameplay()
        {
            var refs=new BuildRefs();

            
            var sys=new GameObject("GameSystems");
            refs.Systems = sys;
            sys.AddComponent<GameManager>();
            var rc = sys.AddComponent<RewindController>();
            rc.WorldMask = (1 << 0) | (1 << LNoNav);
            sys.AddComponent<DeathManager>();
            var om = sys.AddComponent<ObjectiveManager>();
            refs.Ui = sys.AddComponent<UIManager>();
            refs.Flow = sys.AddComponent<GameFlowUI>();
            sys.AddComponent<ComposureMeter>();
            var audio = sys.AddComponent<AudioDirector>();
            var fk = sys.AddComponent<FirstKillDirector>();
            var director = sys.AddComponent<ShiftDirector>();
            sys.AddComponent<RecoveryAnnouncer>();
            sys.AddComponent<HumanQaView>();

            foreach (var path in Directory.GetFiles("Assets/Audio", "*.wav"))
            {
                string p = path.Replace('\\', '/');
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                if (clip == null) { AssetDatabase.ImportAsset(p,ImportAssetOptions.ForceSynchronousImport); clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p); }
                if (clip == null) { Log("AUDIO missing " + p); continue; }
                audio.ClipIds.Add(Path.GetFileNameWithoutExtension(p)); audio.ClipClips.Add(clip);
                var imp = AssetImporter.GetAtPath(p) as AudioImporter;
                if (imp != null)
                {
                    var audioSettings = imp.defaultSampleSettings;
                    bool longClip = clip.length > 5f;
                    var want = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                    if (audioSettings.loadType != want || !imp.forceToMono)
                    {
                        audioSettings.loadType = want;
                        audioSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                        audioSettings.quality = 0.6f;
                        imp.defaultSampleSettings = audioSettings;
                        imp.forceToMono = true;
                        imp.SaveAndReimport();
                    }
                }
            }
            Log("audio clips " + audio.ClipIds.Count);

            
            var player = new GameObject("Player") { tag="Player",layer = LPlayer };
            player.transform.SetPositionAndRotation(SpawnPos,Quaternion.Euler(0,-90f,0));
            var cc = player.AddComponent<CharacterController>();
            cc.radius = 0.3f;
            cc.height = 1.8f;
            cc.center = V(0,0.93f,0);
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.03f;
            cc.slopeLimit = 45f;
            cc.minMoveDistance = 0f;
            var fpc = player.AddComponent<FirstPersonController>();
            fpc.HeadroomMask = (1 << 0) | (1 << LNoNav) | (1 << LProp);
            fpc.PushMask = 1 << LProp;
            var inter = player.AddComponent<PlayerInteractor>();
            inter.Mask = ~((1 << LPlayer) | (1 << 2));
            var prec = player.AddComponent<TimelineRecorder>();
            prec.HistorySeconds = 8f;
            prec.SampleRate = 20f;
            var camGo = new GameObject("PlayerCamera") { tag = "MainCamera", layer = LPlayer };
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = V(0, 1.68f, 0);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.06f;
            cam.farClipPlane = 70f;
            cam.fieldOfView = 70f;
            cam.allowHDR = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.useOcclusionCulling = true;
            var cd = camGo.AddComponent<UniversalAdditionalCameraData>();
            cd.renderPostProcessing = true;
            cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            cd.renderShadows = true;
            camGo.AddComponent<AudioListener>();
            fpc.PlayerCamera = cam; inter.PlayerCamera = cam;
            var torchGo = new GameObject("Flashlight") { layer=LPlayer };
            torchGo.transform.SetParent(camGo.transform, false);
            torchGo.transform.localPosition=V(0.18f, -0.16f, 0.1f);
            var beam=torchGo.AddComponent<Light>();
            beam.type=LightType.Spot;
            beam.range=17f;
            beam.spotAngle=62f;
            beam.innerSpotAngle=26f;
            beam.intensity=8f;
            beam.color=new Color(1f, 0.95f, 0.84f);
            beam.shadows=LightShadows.Hard;
            beam.shadowStrength=0.85f;
            beam.shadowNearPlane=0.25f;
            beam.enabled=false;
            var torch=torchGo.AddComponent<Flashlight>(); torch.Beam=beam;
            refs.Player=player; refs.PlayerCamera=cam;

            
            var menuGo=new GameObject("MenuCamera");
            menuGo.transform.position = V(16.5f,1.45f,6.9f);
            menuGo.transform.rotation = Quaternion.LookRotation(V(-19f,1.25f,6.45f) - menuGo.transform.position);
            var mcam = menuGo.AddComponent<Camera>();
            mcam.nearClipPlane = 0.06f;
            mcam.farClipPlane = 70f;
            mcam.fieldOfView = 50f;
            mcam.clearFlags = CameraClearFlags.SolidColor;
            mcam.backgroundColor = Color.black;
            mcam.depth = -1;
            var mcd = menuGo.AddComponent<UniversalAdditionalCameraData>();
            mcd.renderPostProcessing = true;
            mcd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            menuGo.AddComponent<MenuCameraRig>();
            refs.MenuCamera = mcam;
            refs.Flow.MenuCamera = mcam; refs.Flow.PlayerCamera = cam;
            cam.enabled = false; 

            
            var route = new GameObject("MonsterRoute").transform;
            var wps = new List<Transform>();
            for (int i = 0; i < PatrolRoute.Length; i++)
            {
                var w = new GameObject("Waypoint" + i).transform;
                w.SetParent(route, false); w.position = PatrolRoute[i];
                wps.Add(w);
            }
            refs.Waypoints = wps;
            var monster = new GameObject("Monster") { layer=LMonster };
            monster.transform.SetPositionAndRotation(PatrolRoute[5], Quaternion.Euler(0, 90f, 0));
            var agent=monster.AddComponent<NavMeshAgent>();
            agent.radius=0.3f;
            agent.height=1.9f;
            agent.speed=1.3f;
            agent.acceleration=14f;
            agent.angularSpeed=300f;
            agent.stoppingDistance=0.1f;
            agent.obstacleAvoidanceType=ObstacleAvoidanceType.NoObstacleAvoidance;
            agent.autoBraking=true;
            agent.enabled=false; 
            var ai=monster.AddComponent<MonsterAI>();
            ai.Waypoints=wps.ToArray();
            ai.PatrolSpeed=1.25f;
            ai.StalkSpeed=1.7f;
            ai.ChaseSpeed=3.9f;
            ai.LungeSpeed=5.2f;
            ai.SightRange=13f;
            ai.FovAngle=110f;
            ai.LoseSightAfter=3f;
            ai.ChaseTriggerRange=8f;
            ai.StalkHoldRange=11f;
            ai.KillRange=1.05f;
            ai.RequirePlayerLooking=false;
            ai.LungeTriggerRange=2.3f;
            ai.LungeDuration=0.7f;
            ai.WaypointDwellSeconds=2.2f; ai.ScreamSeconds=2.4f;
            ai.LosMask=(1 << 0) | (1 << LNoNav);
            monster.AddComponent<MonsterMemory>();
            var mrec=monster.AddComponent<TimelineRecorder>();
            mrec.HistorySeconds=8f;
            mrec.SampleRate=20f;
            var mcol=monster.AddComponent<CapsuleCollider>();
            mcol.radius=0.28f;
            mcol.height=1.9f;
            mcol.center=V(0, 0.95f, 0);
            var mrb=monster.AddComponent<Rigidbody>();
            mrb.isKinematic=true;
            mrb.useGravity=false;
            var mfig=Figures.Build("MonsterFigure",monster.transform,new FigureSpec
            {
                Skin = "skinPale", Top = "suit", Bottom = "trousersDark", Hair="hair",Scale = 1.04f,ArmLength = 1.14f,
                MouthWidth = 0.06f,LongFingers = true,EyeSize = 0.011f,SunkenEyes = true,Build = 0.3f
            },LMonster);
            mfig.Brain = ai;
            HumanModelLibrary.Attach(mfig, "Boss", LMonster);
            refs.MonsterAgent = agent;

            
            var stalker = new GameObject("Stalker")
{ layer = LMonster };
            stalker.transform.SetPositionAndRotation(V(-19.1f, 0, 6.4f), Quaternion.Euler(0, 90f, 0));
            var sfig = Figures.Build("StalkerFigure", stalker.transform, new FigureSpec
            {
                Skin="skin",Top = "cardigan", Bottom = "trousers", Hair = "hairBrown", Jacket=false, HasTie=false, Scale=0.97f, MouthWidth=0.042f, Build=0.7f
            }, LMonster);
            sfig.HeadTrack=1f; sfig.ForcePose=FigureAnimator.Pose.Idle;
            HumanModelLibrary.Attach(sfig, "Colleague",LMonster);
            var st = stalker.AddComponent<StalkerEmployee>();
            st.ScareId = "stalker";
            st.Figure = sfig;
            st.SightMask = (1 << 0) | (1 << LNoNav);
            st.ComposureDamage = 12f;

            
            var intern = Figures.Build("Intern", null, new FigureSpec
            {
                Skin = "skin", Top="internShirt",Bottom = "trousers", Hair = "hairBrown", Jacket = false, HasTie = true, Tie = "posterBlue", Scale=0.95f, MouthWidth=0.048f, Build=0.4f
            }, LMonster);
            HumanModelLibrary.Attach(intern, "Intern",LMonster);
            intern.transform.SetPositionAndRotation(InternPos,Quaternion.Euler(0,-90f,0));
            intern.ForcePose = FigureAnimator.Pose.Idle;
            var elevScare = new GameObject("ElevatorScare").AddComponent<ElevatorScare>();
            elevScare.transform.position = V(XMax - 0.2f, 0, ElevZ[0]);
            elevScare.ScareId = "elevator";
            elevScare.DoorLeft = _elevDoorL0;
            elevScare.DoorRight = _elevDoorR0;
            elevScare.Intern = intern;
            elevScare.CabLight = _cabLight0;
            elevScare.ArmAfterObjectiveId = "breaker";
            elevScare.ComposureDamage=18f;
            elevScare.TriggerRadius=4.2f;

            
            var seq=new List<Objective>();
            Objective Obj(string id, string title, string prompt, Vector3 center, Vector3 size, float hold, string line, GameObject host=null, float noise=0f)
            {
                GameObject go=host;
                if (go == null)
                {
                    go=new GameObject("Objective_" + id) { layer = LNoNav };
                    go.transform.position = center;
                    go.AddComponent<BoxCollider>().size = size;
                }
                var o = go.AddComponent<Objective>();
                o.Id = id;
                o.Title = title;
                o.PromptText = prompt;
                o.InteractTime = hold;
                o.CompleteLine = line;
                o.WorkNoise = noise;
                var anchor = new GameObject("Marker").transform;
                anchor.SetParent(go.transform, false);
                anchor.position = host != null ? center : center + V(0, size.y / 2 + 0.1f, 0);
                o.MarkerAnchor = anchor;
                seq.Add(o);
                return o;
            }
            Quaternion deskRot = Quaternion.Euler(0, 180f, 0);
            Obj("clock_in", "Clock in", "Hold [E]  Clock in",ClockInPos + V(0,0,-0.07f),V(0.3f,0.4f,0.16f),0.8f, "02:47 AM. You're clocked in. Again.");
            Obj("login", "Log in at your desk", "Hold [E]  Log in",PlayerDeskPos + deskRot * V(-0.12f,1.06f,-0.14f),V(0.6f,0.44f,0.16f),1.0f, "Fourteen unread emails. Every one is from you.");
            var phoneObj = Obj("phone", "Answer the phone at your desk", "Hold [E]  Answer",PlayerDeskPos + deskRot * V(0.55f,0.82f,-0.18f),V(0.4f,0.3f,0.42f),0.7f, "");
            var print = Obj("print_report", "Print the quarterly report", "Hold [E]  Print report",CopierPos + V(0,1.15f,0),Vector3.zero,1.6f, "Printing page 1 of 1...  page 2 of 1...", _copier);
            Put(ReportSpot + V(0, -0.01f, 0), 0f, g => g.Box("plasticBlack", V(0, 0.012f, 0), V(0.3f, 0.024f, 0.38f), 0.006f));
            Obj("deliver_report", "Leave the report in Meeting Room 2", "Hold [E]  Leave report", ReportSpot + V(0, 0.05f, 0), V(0.4f, 0.14f, 0.46f), 1.0f, "Not that anyone will read it.");
            Obj("coffee", "Get a coffee. You've earned it.", "Hold [E]  Pour coffee", CoffeePos + V(0, 0.22f, -0.02f), V(0.34f, 0.5f, 0.4f), 1.5f, "");
            var breaker = Obj("breaker", "Restore the power: main breaker in Storage", "Hold [E]  Reset main breaker", BreakerPos + BreakerRotation * V(0, 0, 0.24f), V(0.78f, 0.95f, 0.46f), 2.2f, "Power's back. So much for keeping quiet.", null, 5f);
            {
                
                var feedback = breaker.gameObject.AddComponent<ObjectiveFeedback>();
                feedback.Task = breaker;
                var lg = new Geo();
                lg.Box("extinguisher", V(0, -0.07f, 0), V(0.05f, 0.14f, 0.05f), 0.01f);
                lg.Box("metal",V(0,0.0f,-0.02f),V(0.07f,0.03f,0.04f),0.006f);
                var lever = lg.Emit("BreakerLever", null, false);
                lever.transform.SetPositionAndRotation(BreakerPos + BreakerRotation * V(-0.25f, -0.24f, 0.14f), BreakerRotation);
                feedback.Moving = lever.transform;
                feedback.DoneLocalEuler = new Vector3(-150f, 180f, 0f);
                var led = new Geo();
                led.Box("ledRed", Vector3.zero, V(0.03f, 0.03f, 0.012f), 0.004f);
                var ledGo = led.Emit("BreakerFaultLight", null, false, false);
                ledGo.transform.SetPositionAndRotation(BreakerPos + BreakerRotation * V(0.22f, 0.3f, 0.135f), BreakerRotation);
                feedback.Indicator=ledGo.GetComponent<MeshRenderer>();
                feedback.DoneMaterial=Mats.Get("ledGreen");
            }
            Obj("parcel", "Collect the parcel from the lift lobby", "Hold [E]  Take parcel", ParcelPos + V(0, 0.17f, 0), V(0.5f, 0.36f, 0.44f), 1.0f, "It's addressed to you. And it's from you.");
            Obj("incident", "File an incident report: Manager's office", "Hold [E]  File report", V(-17.45f, 0.86f, 11.85f), V(0.34f, 0.2f, 0.4f), 2.5f, "Incident type:  [x] I'd rather not say.");
            Obj("plant", "Last one: water the plant in the far corner", "Hold [E]  Water plant", PlantPos + V(0, 0.8f, 0), V(0.7f, 1.5f, 0.7f), 2.0f, "I can't tell if it's grateful or hungry.");
            om.Sequence = seq.ToArray();
            refs.Objectives = seq;

            
            var ps = _copier.AddComponent<PrinterScare>();
            ps.ScareId = "printer";
            ps.FacePage = _facePage;
            ps.ArmAfterObjectiveId = "login";
            ps.WorldMask = (1 << 0) | (1 << LNoNav) | (1 << LProp);
            ps.ComposureDamage = 14f;
            var phone = phoneObj.gameObject.AddComponent<PhoneCallScare>();
            phone.ScareId = "phone"; phone.ComposureDamage=16f;
            var coffee=new GameObject("CoffeeScare").AddComponent<CoffeeMachineScare>();
            coffee.transform.position = CoffeePos;
            coffee.ScareId = "coffee"; coffee.ComposureDamage = 22f;
            var reveal = new GameObject("CoffeeRevealPoint").transform;
            reveal.position = CoffeeReveal;
            coffee.RevealPoint = reveal;

            
            var exit = new GameObject("ElevatorExit").transform; exit.position=ExitPos;
            refs.Exit=exit;
            director.ExitPoint=exit; director.ExitRadius=1.6f;
            director.DoorsLeft=new[] { _elevDoorL0, _elevDoorL1 };
            director.DoorsRight=new[] { _elevDoorR0, _elevDoorR1 };
            director.Act3Barrier=_shutter;
            fk.AfterObjectiveId="deliver_report";

            return refs;
        }
    }
}
#endif

