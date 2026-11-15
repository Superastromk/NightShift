var gm=NightShift.Core.GameManager.Instance;
gm.SetState(NightShift.Core.GameState.Playing);
var p=NightShift.Player.FirstPersonController.Instance;
var rec=p.GetComponent<NightShift.Rewind.TimelineRecorder>();
rec.Clear();
for(int i=0;i<120;i++){p.Warp(new UnityEngine.Vector3(-12f+i*.035f,0,6.5f),UnityEngine.Quaternion.identity);rec.PushSample();}
var expected=rec.GetSampleAgo(5f).pos;
var positions=new System.Collections.Generic.List<float>();
var rc=NightShift.Rewind.RewindController.Instance;
NightShift.Rewind.DeathManager.Instance.Kill("overhaul-verification");
double start=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
 if(gm.State==NightShift.Core.GameState.Rewinding)positions.Add(p.transform.position.x);
 if(!rc.IsRewinding || UnityEditor.EditorApplication.timeSinceStartup-start>20){
  UnityEditor.EditorApplication.update-=tick;
  int backwards=0,forwards=0;for(int i=1;i<positions.Count;i++){float d=positions[i]-positions[i-1];if(d<-.001f)backwards++;if(d>.001f)forwards++;}
  string report="samples="+positions.Count+" backward_steps="+backwards+" forward_steps="+forwards+" final="+p.transform.position+" expected="+expected+" timeline="+gm.TimelineIndex+" rewinding="+rc.IsRewinding;
  System.IO.File.WriteAllText("qa/rewind-verification.txt",report);UnityEngine.Debug.Log("VERIFY REWIND "+report);
 }
};UnityEditor.EditorApplication.update+=tick;
return "Rewind verification started";
