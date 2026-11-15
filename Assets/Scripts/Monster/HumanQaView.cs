using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using NightShift.Core;
using NightShift.Player;
using NightShift.Rewind;
using NightShift.Scares;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
#endif

namespace NightShift.Monster
{
    public class HumanQaView : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly string[] FigureNames={"StalkerFigure","Intern","MonsterFigure"};
        int _index=-1;
        int _pose,_view;
        bool _close,_walking;
        FigureAnimator _figure;
        MonsterAI _frozenBrain;
        NavMeshAgent _frozenAgent;
        bool _agentWasEnabled;
        float _headTrack=1f;
        readonly System.Collections.Generic.List<MonoBehaviour> _frozenControls=new System.Collections.Generic.List<MonoBehaviour>();
        readonly System.Collections.Generic.List<FirstKillDirector> _directors=new System.Collections.Generic.List<FirstKillDirector>();
        Camera _camera;
        Light _inspectionLight;
        DeathManager _deaths;
        bool _deathsWereEnabled;
        Vector3 _forward=Vector3.forward,_stage,_homePosition,_figureHomeLocalPosition;
        Quaternion _homeRotation,_figureHomeLocalRotation;
        Transform _movedRoot;
        Light _flashlight;
        bool _flashlightWasEnabled;

        void Update()
        {
            if(GameManager.Instance!=null && GameManager.Instance.State!=GameState.Playing)return;
            var keyboard=Keyboard.current;
            if(keyboard==null)return;
            if(keyboard.f11Key.wasPressedThisFrame)
            {
                bool face=keyboard.leftShiftKey.isPressed && !keyboard.leftCtrlKey.isPressed && _figure!=null;
                bool orbit=keyboard.leftCtrlKey.isPressed && !keyboard.leftShiftKey.isPressed && _figure!=null;
                if(keyboard.leftShiftKey.isPressed && keyboard.leftCtrlKey.isPressed && _figure!=null)
                {
                    _close=!_close;
                    _view=0;
                }
                else if(!face && !orbit)
                {
                    RestorePosition();
                    ResumeBrain();
                    _index=(_index+1)%FigureNames.Length;
                    _figure=null;
                    _view=0;
                    _close=false;
                    var host=GameObject.Find(FigureNames[_index]);
                    if(host!=null)_figure=host.GetComponent<FigureAnimator>();
                    if(_figure==null){Debug.Log("TEL QA F11 missing "+FigureNames[_index]);return;}
                    _pose=0;
                    _walking=false;
                    _figure.ForcePose=FigureAnimator.Pose.Idle;
                    EnsureInspection();
                    FreezeBrain();
                    PlaceFigure();
                }
                else
                {
                    _view=face?0:(_view==0?2:_view==2?3:0);
                    if(face)_close=true;
                }
                PlaceCamera();
                Report(ViewName());
            }
            if(keyboard.f12Key.wasPressedThisFrame && _figure!=null)
            {
                _pose=(_pose+1)%3;
                _walking=_pose==0 && _figure.Brain!=null;
                if(_walking)_close=false;
                _figure.ForcePose=_pose==0?FigureAnimator.Pose.Auto:_pose==1?FigureAnimator.Pose.Scream:FigureAnimator.Pose.Reach;
                if(_walking)BeginWalk();
                else
                {
                    ResumeBrain();
                    FreezeBrain();
                }
                PlaceCamera();
                Report(_walking?"walk":_pose==0?"idle":"pose");
            }
        }

        void LateUpdate()
        {
            if(_figure!=null && _camera!=null)
            {
                if(!_walking)_forward=_figure.transform.forward;
                PlaceCamera();
            }
            if(_walking && _frozenBrain!=null && Time.frameCount%60==0)
                Debug.Log("TEL QA walk_sample "+_frozenBrain.QaTel()+" position="+_frozenBrain.transform.position+" agent_enabled="+_frozenAgent.enabled+" on_navmesh="+_frozenAgent.isOnNavMesh+" destination="+_frozenAgent.destination+" velocity="+_frozenAgent.velocity);
        }

        void EnsureInspection()
        {
            var fpc=FirstPersonController.Instance;
            if (fpc==null)return;
            fpc.InputLocked=true;
            if (_deaths==null)
            {
                _deaths=FindAnyObjectByType<DeathManager>();
                if (_deaths!=null)_deathsWereEnabled=_deaths.DeathsEnabled;
            }
            if (_deaths!=null)_deaths.DeathsEnabled=false;
            foreach (var director in FindObjectsByType<FirstKillDirector>(FindObjectsSortMode.None))
            {
                if (!director.enabled)continue;
                _directors.Add(director);
                director.enabled=false;
            }
            _stage=new Vector3(12f,.03f,9.8f);
            if (_camera!=null)return;
            var original=fpc.PlayerCamera;
            _flashlight=original.GetComponentInChildren<Light>(true);
            if (_flashlight!=null)
            {
                _flashlightWasEnabled=_flashlight.enabled;
                _flashlight.enabled=false;
            }
            var host=new GameObject("Human inspection camera");
            _camera=host.AddComponent<Camera>();
            _camera.clearFlags=original.clearFlags;
            _camera.backgroundColor=original.backgroundColor;
            _camera.cullingMask=original.cullingMask;
            _camera.nearClipPlane=0.035f;
            _camera.farClipPlane=original.farClipPlane;
            _camera.fieldOfView=48f;
            _camera.allowHDR=original.allowHDR;
            _camera.allowMSAA=original.allowMSAA;
            var data=host.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing=true;
            data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            _inspectionLight=host.AddComponent<Light>();
            _inspectionLight.type=LightType.Point;
            _inspectionLight.range=7f;
            _inspectionLight.intensity=1.5f;
            _inspectionLight.color=new Color(1f,.95f,.88f);
            _inspectionLight.shadows=LightShadows.None;
            original.enabled=false;
        }

        void PlaceFigure()
        {
            _movedRoot=_figure.Brain!=null?_figure.Brain.transform:_figure.transform;
            _homePosition=_movedRoot.position;
            _homeRotation=_movedRoot.rotation;
            _figureHomeLocalPosition=_figure.transform.localPosition;
            _figureHomeLocalRotation=_figure.transform.localRotation;
            _movedRoot.SetPositionAndRotation(_stage,Quaternion.LookRotation(Vector3.forward));
            if(_movedRoot!=_figure.transform)_figure.transform.localPosition=Vector3.zero;
            _figure.transform.localRotation=Quaternion.identity;
            _forward=_figure.transform.forward;
            Debug.Log("TEL QA inspection_position="+_stage+" home="+_homePosition+" root="+_movedRoot.position+" figure="+_figure.transform.position);
        }

        void RestorePosition()
        {
            if(_movedRoot==null || _figure==null)return;
            _movedRoot.SetPositionAndRotation(_homePosition,_homeRotation);
            _figure.transform.localPosition=_figureHomeLocalPosition;
            _figure.transform.localRotation=_figureHomeLocalRotation;
            _movedRoot=null;
        }

        void FreezeBrain()
        {
            if(_figure==null || _frozenControls.Count>0)return;
            _frozenBrain=_figure.Brain;
            _frozenAgent=_frozenBrain!=null?_frozenBrain.GetComponent<NavMeshAgent>():null;
            _agentWasEnabled=_frozenAgent!=null && _frozenAgent.enabled;
            if(_frozenAgent!=null && _frozenAgent.enabled && _frozenAgent.isOnNavMesh)_frozenAgent.isStopped=true;
            foreach(var control in _figure.GetComponentsInParent<MonoBehaviour>(true))
            {
                if(control==null || control==_figure || control is HumanRigBinder || !control.enabled)continue;
                _frozenControls.Add(control);
                control.enabled=false;
            }
            if(_frozenAgent!=null)_frozenAgent.enabled=false;
            _headTrack=_figure.HeadTrack;
            _figure.HeadTrack=0f;
            _forward=_figure.transform.forward;
        }

        void BeginWalk()
        {
            ResumeBrain();
            FreezeBrain();
            if(_frozenAgent==null)return;
            _view=0;
            _stage=new Vector3(0f,0f,6.5f);
            if(NavMesh.SamplePosition(_stage,out var start,3f,NavMesh.AllAreas))_stage=start.position;
            _figure.transform.localPosition=Vector3.zero;
            _figure.transform.localRotation=Quaternion.identity;
            _frozenAgent.enabled=true;
            _frozenAgent.Warp(_stage);
            _frozenAgent.isStopped=false;
            _frozenAgent.speed=1.8f;
            _frozenAgent.angularSpeed=240f;
            _frozenAgent.updateRotation=false;
            _frozenAgent.transform.rotation=Quaternion.LookRotation(Vector3.right);
            var destination=new Vector3(14f,0f,6.5f);
            if(NavMesh.SamplePosition(destination,out var nav,3f,NavMesh.AllAreas))destination=nav.position;
            var path=new NavMeshPath();
            if(NavMesh.CalculatePath(_stage,destination,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete)_frozenAgent.SetPath(path);
            else _frozenAgent.SetDestination(destination);
            _forward=Vector3.right;
            _walking=true;
            Debug.Log("TEL QA walk_begin stage="+_stage+" root="+_frozenAgent.transform.position+" figure="+_figure.transform.position+" destination="+destination);
        }

        void ResumeBrain()
        {
            foreach(var control in _frozenControls)if(control!=null)control.enabled=true;
            _frozenControls.Clear();
            if(_figure!=null)_figure.HeadTrack=_headTrack;
            if(_frozenAgent!=null)
            {
                if(_frozenAgent.enabled)_frozenAgent.isStopped=true;
                _frozenAgent.enabled=_agentWasEnabled;
            }
            _frozenBrain=null;
            _frozenAgent=null;
            _walking=false;
        }

        void OnDisable()
        {
            RestorePosition();
            ResumeBrain();
            if(_deaths!=null)_deaths.DeathsEnabled=_deathsWereEnabled;
            foreach(var director in _directors)if(director!=null)director.enabled=true;
            _directors.Clear();
            if(FirstPersonController.Instance!=null)
            {
                FirstPersonController.Instance.InputLocked=false;
                if(FirstPersonController.Instance.PlayerCamera!=null)FirstPersonController.Instance.PlayerCamera.enabled=true;
            }
            if(_flashlight!=null)_flashlight.enabled=_flashlightWasEnabled;
            if(_camera!=null)Destroy(_camera.gameObject);
        }

        string ViewName()=>_close?(_view==2?"three_quarter":_view==3?"back":"face")+"_close":"full";

        void PlaceCamera()
        {
            if(_figure==null)return;
            if(_camera==null)EnsureInspection();
            if(_camera==null)return;
            Vector3 target=_figure.transform.position+Vector3.up*0.98f;
            Vector3 direction=Quaternion.Euler(0f,_view==2?45f:_view==3?180f:0f,0f)*_forward;
            if(_close)
            {
                var binder=_figure.GetComponent<HumanRigBinder>();
                var head=binder!=null && binder.Visual!=null?binder.Visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="head"):_figure.Head;
                if (head!=null)target=head.position+Vector3.up*0.14f+_forward*0.025f;
            }
            float distance=_close?0.9f:2.65f;
            _camera.transform.position=target+direction*distance+Vector3.up*(_close?0.025f:0.1f);
            _camera.transform.rotation=Quaternion.LookRotation(target-_camera.transform.position,Vector3.up);
        }

        void Report(string view)
        {
            string rig=_figure.TryGetComponent<HumanRigBinder>(out var binder)?binder.QaTel():"humanrig=absent";
            string ai=_figure.Brain!=null?" ai_enabled="+(_figure.Brain.enabled?1:0):"";
            string agent=_frozenAgent!=null?" agent_enabled="+(_frozenAgent.enabled?1:0)+" on_navmesh="+(_frozenAgent.isOnNavMesh?1:0):"";
            Debug.Log("TEL QA "+view+" figure="+_figure.name+" pose="+_figure.ForcePose+" "+FigureInfo(_figure)+" "+rig+ai+agent+" forward="+_forward);
        }

        static string FigureInfo(FigureAnimator figure)
        {
            var meshes=figure.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var proxies=figure.GetComponentsInChildren<MeshRenderer>(true).Count(r=>r.enabled);
            return "skinned="+meshes.Length+" vertices="+meshes.Sum(m=>m.sharedMesh!=null?m.sharedMesh.vertexCount:0)+
                " visible_proxy_meshes="+proxies+" bounds="+string.Join(";",meshes.Select(m=>m.bounds.size.ToString("F2")));
        }
#endif
    }
}
