using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BoneAssembly.Editor
{
    public static class BoneComfortVerification
    {
        static IEnumerator routine;
        static double next;
        static LabPlayer player;
        static BoneAssemblyGame game;
        static readonly System.Text.StringBuilder report=new System.Text.StringBuilder();
        public static void Start()
        {
            if(routine!=null)throw new Exception("Verification already running.");
            game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            player=game.rig.GetComponent<LabPlayer>();player.enabled=false;game.verificationMode=true;
            report.Clear();routine=Run();next=0;EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try{
                if(routine.MoveNext()){next=EditorApplication.timeSinceStartup+(routine.Current is float seconds?seconds:.03f);return;}
                Finish("PASS\n"+report);
            }catch(Exception e){Finish("FAIL\n"+report+"\n"+e);Debug.LogException(e);}
        }
        static void Finish(string result)
        {
            EditorApplication.update-=Tick;routine=null;
            if(game){game.verificationMode=false;game.assignmentMode=false;game.practice=true;game.StartStage(4);}
            if(player)player.enabled=true;
            File.WriteAllText("tmp/comfort-verification.txt",result);
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);report.AppendLine(message);}
        static IEnumerator Run()
        {
            Application.runInBackground=true;
            game.assignmentMode=false;game.practice=true;game.StartStage(4);game.ResetDesktopSeat();
            yield return .3f;
            Check(game.Workspace&&game.Workspace.Board,"Upright workspace initialized");
            var console=game.Workspace.Console;
            Check(console.GetComponentInChildren<Canvas>() && !console.GetComponentsInChildren<Collider>().Any(),"World canvas has no physics colliders");
            Check(Vector3.Dot(game.Workspace.Board.forward,Vector3.up)>.99f,"Skeleton longitudinal axis points up");
            Check(game.pieces.All(p=>p.Body&&p.GetComponentsInChildren<Collider>().Count(c=>!c.isTrigger)>0),"All pieces have bodies and compound contact colliders");
            var tray=game.Workspace.Station.leftTray;
            var authoredPosition=tray.position;
            var authoredRotation=tray.rotation;
            var first=game.ActivePieces.First();
            var localHome=tray.InverseTransformPoint(first.home);
            tray.SetPositionAndRotation(authoredPosition+new Vector3(.08f,.1f,.07f),Quaternion.Euler(0,18,0)*authoredRotation);
            game.StartStage(4);
            Check(Vector3.Distance(first.home,tray.TransformPoint(localHome))<.001f,"Retry follows authored tray position and yaw");
            first.Teleport(first.home+Vector3.up*.5f,first.homeRotation);
            game.ResetLooseBones();
            Check(Vector3.Distance(first.transform.position,first.home)<.001f,"Loose bone recovers to moved tray");
            tray.SetPositionAndRotation(authoredPosition,authoredRotation);
            game.StartStage(4);
            var active=game.ActivePieces.ToArray();
            Check(active.Length==22 && active.All(b=>b.gameObject.activeInHierarchy),"All full-body pieces are on the trays");
            var boxes=active.Select(b=>BoneSurfacePlacement.MeshBounds(b.gameObject)).ToArray();
            for(int i=0;i<boxes.Length;i++)
                for(int j=i+1;j<boxes.Length;j++)
                    if(boxes[i].Intersects(boxes[j])) throw new Exception("Overlapping tray slots: "+active[i].label+" / "+active[j].label);
            report.AppendLine("Full-body spawn bounds are disjoint");
            var p=game.ActivePieces.First(a=>a.IsHead);
            p.Teleport(p.home+Vector3.up*.5f,p.homeRotation);float initial=p.transform.position.y;
            yield return .25f;
            Check(p.transform.position.y<initial-.1f,"Dropped bone falls under gravity");
            yield return 1f;
            Check(BoneSurfacePlacement.MeshBounds(p.gameObject).min.y>.84f,"Bone settles on side tray without falling through");
            p.SetMotion(BoneMotionState.Attracting,0);Vector3 start=p.transform.position;Vector3 goal=start+Vector3.up*.4f;p.Follow(goal,p.transform.rotation);
            yield return .1f;
            Check(p.transform.position.y>start.y&&p.transform.position.y<goal.y,"Magnet pickup moves gradually rather than teleporting");
            p.Drop();Check(!p.held&&p.Body.useGravity,"Cancelled magnet restores gravity");
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Verification obstacle";obstacle.transform.position=new Vector3(-2,2,-.5f);obstacle.transform.localScale=new Vector3(1,1,.1f);
            p.Teleport(new Vector3(-2,2,-1),Quaternion.identity);p.SetMotion(BoneMotionState.Attracting,0);p.Follow(new Vector3(-2,2,0),Quaternion.identity);
            yield return .6f;
            Check(BoneSurfacePlacement.MeshBounds(p.gameObject).max.z<-.51f,"Magnet respects solid obstacles");
            UnityEngine.Object.DestroyImmediate(obstacle);
            p.SetMotion(BoneMotionState.Regripping,0);Vector3 frozen=p.transform.position;
            yield return .15f;
            Check(Vector3.Distance(frozen,p.transform.position)<.001f,"Regrip keeps the bone suspended");
            Check(!game.TryPlace(p)&&game.LastPlacement==PlacementResult.Unavailable,"Submission disabled during floating regrip");
            p.SetMotion(BoneMotionState.Held,1);p.Follow(frozen,p.transform.rotation);
            Check(p.owner==1&&p.held&&!p.Body.isKinematic,"Transfer restores dynamic handling with one owner");
            p.Teleport(p.target,p.targetRotation*Quaternion.Euler(0,120,0));
            p.Teleport(p.transform.position+BoneSurfacePlacement.MeshBounds(p.ghost).center-BoneSurfacePlacement.MeshBounds(p.gameObject).center,p.transform.rotation);
            p.Follow(p.transform.position,p.transform.rotation);
            Check(!game.TryPlace(p)&&game.LastPlacement==PlacementResult.WrongRotation&&p.held,"Skull rejects wrong rotation and remains held");
            Vector3 failed=p.transform.position;int mistakes=game.mistakes;
            yield return .7f;
            Check(p.held&&Vector3.Distance(p.transform.position,failed)<.02f&&p.surfaces.All(r=>r.sharedMaterial==game.ivory),"Error flash expires without returning the bone to its tray");
            var other=game.ActivePieces.First(a=>a.label=="Pelvic girdle");
            Vector3 centreOffset=BoneSurfacePlacement.MeshBounds(p.gameObject).center-p.transform.position;
            p.Teleport(BoneSurfacePlacement.MeshBounds(other.ghost).center-centreOffset,p.transform.rotation);
            Check(!game.TryPlace(p)&&game.LastPlacement==PlacementResult.WrongBone,"Wrong anatomical position reports wrong bone");
            p.Teleport(p.target+Vector3.up*2,p.targetRotation);
            Check(!game.TryPlace(p)&&game.LastPlacement==PlacementResult.TooFar,"Empty-space submission reports move closer");
            p.Teleport(p.target,p.targetRotation);
            Check(game.TryPlace(p)&&p.placed&&p.Body.isKinematic,"Correct submission locks bone at its target");
            yield return .7f;
            Check(p.surfaces.All(r=>r.sharedMaterial==game.success),"Successful bone remains green after a previous error");
            other.SetMotion(BoneMotionState.Held,0);other.Follow(other.transform.position,other.transform.rotation);
            Vector3 heldPosition=other.transform.position,placedPosition=p.transform.position;float elapsed=game.elapsed;mistakes=game.mistakes;
            game.ResetLooseBones();
            Check(other.held&&other.transform.position==heldPosition&&p.placed&&p.transform.position==placedPosition&&game.elapsed==elapsed&&game.mistakes==mistakes,"Recovery preserves held pieces, completed pieces, timer and score");
            Check(!game.Workspace.Adjust("larger"),"Size change refused while holding a bone");other.Drop();
            other.Teleport(new Vector3(0,-1,0),Quaternion.identity);yield return .1f;
            Check(other.transform.position.y>.8f,"Out-of-bounds bones are automatically rescued");
            float size=game.Workspace.Size;game.Workspace.Adjust("larger");
            Check(game.Workspace.Size>size&&p.placed&&Vector3.Distance(p.transform.position,p.target)<.001f,"Board resizing preserves completed anatomy");
            game.Workspace.Adjust("raise");game.Workspace.Adjust("far");
            Check(Vector3.Distance(p.transform.position,p.target)<.001f,"Placed bones follow board height and distance changes");
            foreach(float eyeHeight in new[]{1.15f,1.75f}){
                var boardPosition=game.Workspace.Board.position;
                var consolePosition=console.transform.position;
                var tracked=new Vector3(.3f,eyeHeight,-.2f);
                game.CalibrateSeatedPose(tracked,Quaternion.Euler(15,35,0));
                Check(Vector3.Distance(game.rig.TransformPoint(tracked),game.Workspace.Station.playerStart.position+Vector3.up*eyeHeight)<.001f,"Player recentered at tracked height "+eyeHeight);
                Check(game.Workspace.Board.position==boardPosition && console.transform.position==consolePosition,"Recenter keeps the workstation fixed");

            }
            other.SetMotion(BoneMotionState.Held,0);other.Follow(other.transform.position,other.transform.rotation);game.ShowBoneInformation(other);
            var card=game.playerCamera.GetComponent<BoneLearningDisplay>();card.UpdateVisibility(Time.time+20);yield return .1f;
            Check(card.IsVisible&&!card.panel.transform.IsChildOf(game.playerCamera.transform),"Information persists while held and is not camera-parented");
            Check(Vector3.Dot(card.panel.transform.forward,(card.panel.transform.position-game.playerCamera.transform.position).normalized)>.99f,"Information panel faces player");
            card.Toggle();Check(!card.IsVisible,"Information card can be hidden");card.Toggle();other.Drop();card.UpdateVisibility(Time.time);Check(!card.IsVisible,"Dropped bone hides its card");
            foreach(int assignment in Enumerable.Range(0,4)){
                game.BeginAssignment(assignment);
                foreach(var bone in game.ActivePieces.ToArray()){bone.Teleport(bone.target,bone.targetRotation);Check(game.TryPlace(bone),"Assignment "+(assignment+1)+": "+bone.label+" fits");}
                Check(game.completed,"Assignment "+(assignment+1)+" completes");
            }
            game.assignmentMode=false;game.practice=false;
            for(int stage=0;stage<5;stage++){
                game.StartStage(stage);Check(game.ActivePieces.All(b=>!b.ghost.activeSelf),"Test stage "+stage+" hides target guides");
                foreach(var bone in game.ActivePieces.ToArray()){bone.Teleport(bone.target,bone.targetRotation);Check(game.TryPlace(bone),"Test pose accepted: "+bone.label);}
            }
            game.StartStage(4);p=game.ActivePieces.First();p.SetMotion(BoneMotionState.Held,0);game.AdvanceSession(600);
            Check(game.lost&&!p.held&&p.Body.useGravity,"Timeout releases held bones and ends interaction");
            game.practice=true;game.StartStage(4);game.ResetDesktopSeat();yield return .3f;
            Check(game.settings.leftHandPrefab&&game.settings.rightHandPrefab,"Both PolyOne hand prefabs configured");
            Capture("output/ComfortWorkspace.png");
            var left=UnityEngine.Object.Instantiate(game.settings.leftHandPrefab);var right=UnityEngine.Object.Instantiate(game.settings.rightHandPrefab);
            left.transform.SetPositionAndRotation(game.playerCamera.transform.position+new Vector3(-.16f,-.12f,.45f),Quaternion.Euler(-40,0,0));
            right.transform.SetPositionAndRotation(game.playerCamera.transform.position+new Vector3(.16f,-.12f,.45f),Quaternion.Euler(-40,0,0));
            left.GetComponent<ControllerHandVisual>().Pose(0,0);right.GetComponent<ControllerHandVisual>().Pose(1,1);
            yield return .1f;
            var leftSkin=left.GetComponentInChildren<SkinnedMeshRenderer>();var rightSkin=right.GetComponentInChildren<SkinnedMeshRenderer>();
            var baked=new Mesh();leftSkin.BakeMesh(baked);Check(baked.vertexCount>100,"Left skinned hand renders");rightSkin.BakeMesh(baked);Check(baked.vertexCount>100,"Right skinned hand renders");UnityEngine.Object.DestroyImmediate(baked);
            Capture("output/ControllerHands.png");UnityEngine.Object.DestroyImmediate(left);UnityEngine.Object.DestroyImmediate(right);
            p=game.ActivePieces.First(b=>b.IsHead);p.SetMotion(BoneMotionState.Held,0);p.Teleport(game.playerCamera.transform.position+new Vector3(0,-.1f,.6f),p.targetRotation);p.Follow(p.transform.position,p.transform.rotation);game.ShowBoneInformation(p);
            yield return .1f;Capture("output/BoneSideInformation.png");p.Drop();
            report.AppendLine("Hardware Quest controller feel and Android performance require headset testing.");
        }
        static void Capture(string path)
        {
            bool canvas=game.desktopUI&&game.desktopUI.canvas.enabled;if(game.desktopUI)game.desktopUI.canvas.enabled=false;
            var camera=game.playerCamera;var rt=new RenderTexture(1600,1000,24);var old=camera.targetTexture;var active=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            Directory.CreateDirectory("output");File.WriteAllBytes(path,image.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=active;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);if(game.desktopUI)game.desktopUI.canvas.enabled=canvas;
        }
    }
}
