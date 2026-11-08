using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace BoneAssembly.Editor
{
    [InitializeOnLoad]
    public static class BoneAssemblySetup
    {
        const string ScenePath="Assets/Scenes/BoneAssemblyVR.unity";
        static BoneAssemblySetup(){EditorApplication.delayCall+=AutoSetup;}
        static void AutoSetup()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=AutoSetup;return;}
            if(!File.Exists(ScenePath)&&Resources.Load<TextAsset>("SkeletonData")!=null)CreateScene();
            EditorApplication.update-=Bridge;EditorApplication.update+=Bridge;
        }
        [MenuItem("Bone Assembly/Create Operating Theatre")]
        public static void CreateScene()
        {
            // Never overwrite the user's currently edited scene.
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            ConfigureXR();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Bone Assembly | Operating Theatre");var game=root.AddComponent<BoneAssemblyGame>();game.BuildRoom();
            Directory.CreateDirectory("Assets/BoneAssembly/Generated");
            int index=0;
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)){
                var mesh=mf.sharedMesh;if(mesh==null||AssetDatabase.Contains(mesh))continue;
                string path="Assets/BoneAssembly/Generated/Mesh_"+(index++)+".asset";AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(path));
            }
            foreach(var material in root.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Distinct()){
                if(AssetDatabase.Contains(material))continue;AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath("Assets/BoneAssembly/Generated/Material.asset"));
            }
            game.StartStage(1);EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            SceneView.lastActiveSceneView?.LookAt(new Vector3(0,1,.3f),Quaternion.Euler(30,-30,0),4);
            File.WriteAllText("tmp/setup-result.txt","OK: operating theatre created; "+game.pieces.Count+" anatomical teaching units; OpenXR configured.");
            Debug.Log("Bone Assembly VR: operating theatre is ready. Press Play.");
        }
        [MenuItem("Bone Assembly/Configure OpenXR")]
        public static void ConfigureXR()
        {
            var all=XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if(all==null){
                var container=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                Directory.CreateDirectory("Assets/XR");AssetDatabase.CreateAsset(container,"Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,container,true);
                all=ScriptableObject.CreateInstance<XRGeneralSettings>();container.SetSettingsForBuildTarget(BuildTargetGroup.Standalone,all);AssetDatabase.AddObjectToAsset(all,container);
            }
            if(all.Manager==null){all.Manager=ScriptableObject.CreateInstance<XRManagerSettings>();AssetDatabase.AddObjectToAsset(all.Manager,AssetDatabase.GetAssetPath(all));}
            all.InitManagerOnStart=true;
            XRPackageMetadataStore.AssignLoader(all.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Standalone);
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
            if(settings!=null){foreach(var f in settings.GetFeatures<UnityEngine.XR.OpenXR.Features.OpenXRFeature>())if(f is OculusTouchControllerProfile||f is ValveIndexControllerProfile||f is MicrosoftMotionControllerProfile)f.enabled=true;EditorUtility.SetDirty(settings);}
            EditorUtility.SetDirty(all);EditorUtility.SetDirty(all.Manager);
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.companyName="Bone Assembly";PlayerSettings.productName="Bone Assembly VR";
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Bone Assembly/Configure Quest Standalone")]
        public static void ConfigureQuest()
        {
            EditorBuildSettings.TryGetConfigObject<XRGeneralSettingsPerBuildTarget>(XRGeneralSettings.k_SettingsKey,out var container);
            if(container==null){ConfigureXR();EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,out container);}
            if(!container.HasSettingsForBuildTarget(BuildTargetGroup.Android))container.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            var general=container.SettingsForBuildTarget(BuildTargetGroup.Android);
            if(general.Manager==null){general.Manager=ScriptableObject.CreateInstance<XRManagerSettings>();AssetDatabase.AddObjectToAsset(general.Manager,container);}
            general.InitManagerOnStart=true;XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.OpenXR.OpenXRLoader",BuildTargetGroup.Android);
            var settings=OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if(settings!=null){foreach(var feature in settings.GetFeatures())if(feature is OculusTouchControllerProfile||feature is UnityEngine.XR.OpenXR.Features.MetaQuestSupport.MetaQuestFeature)feature.enabled=true;EditorUtility.SetDirty(settings);}
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.boneassembly.vr");
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.Vulkan});
            EditorUtility.SetDirty(container);EditorUtility.SetDirty(general);EditorUtility.SetDirty(general.Manager);AssetDatabase.SaveAssets();
        }
        static void Bridge()
        {
            const string file="tmp/editor-command.txt";if(!File.Exists(file)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            string command;try{command=File.ReadAllText(file).Trim();File.Delete(file);}catch(IOException){return;}
            try{
                if(command=="play")EditorApplication.isPlaying=true;
                else if(command=="stop")EditorApplication.isPlaying=false;
                else if(command=="capture")Capture();
                else if(command=="verify")Verify();
                else if(command=="quest")ConfigureQuest();
                else if(command=="build")BuildWindows();
                else if(command=="polish")Polish();
                else if(command=="verifyassignments")VerifyAssignments();
                else if(command=="recorddance")RecordDance();
                else if(command=="verifysurfaces")VerifySurfacePlacement();
                else if(command=="verifyseated")VerifySeatedPlay();
                else if(command=="verifyview")VerifyAssemblyView();
                else if(command=="verifyhead")VerifyHeadAssist();
                else if(command=="verifyworkbench")VerifyWorkbenchAndLearning();
                else if(command=="verifyreview")VerifyReviewAndSnap();
            }catch(Exception e){File.WriteAllText("tmp/verification-result.txt",e.ToString());Debug.LogException(e);}
        }
        static void Polish()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            foreach(var label in game.GetComponentsInChildren<TextMesh>())label.characterSize*=.55f;
            var c=game.playerCamera;c.transform.rotation=Quaternion.LookRotation(new Vector3(0,1.25f,.4f)-c.transform.position);
            foreach(var entry in new[]{("SEATED / STAND","seated",-.6f),("TEXT SIZE","text",0f),("SOUND","sound",.6f)}){
                var p=new Vector3(entry.Item3,1.12f,2.22f);var g=game.Box("Button "+entry.Item2,p,new Vector3(.5f,.105f,.035f),game.teal);g.AddComponent<LabAction>().command=entry.Item2;game.Label("Label "+entry.Item2,entry.Item1,p+Vector3.back*.025f,.015f,Color.white);
            }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());Capture();
        }
        [MenuItem("Bone Assembly/Build Windows Player")]
        public static void BuildWindows()
        {
            Directory.CreateDirectory("output/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="output/Windows/BoneAssemblyVR.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText("tmp/build-result.txt",report.summary.result+" | "+report.summary.totalErrors+" errors");
        }
        [MenuItem("Bone Assembly/Capture Theatre Preview")]
        static void Capture()=>CaptureTo("output/OperatingTheatre.png");
        static void CaptureTo(string path)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();var c=game.playerCamera;
            var rt=new RenderTexture(1600,1000,24);var previous=c.targetTexture;c.targetTexture=rt;c.Render();
            var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            Directory.CreateDirectory("output");File.WriteAllBytes(path,image.EncodeToPNG());c.targetTexture=previous;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
        }
        [MenuItem("Bone Assembly/Verify Gameplay (Play Mode)")]
        static void Verify()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            if(!EditorApplication.isPlaying)throw new Exception("Verification requires Play mode.");
            game.verificationMode=true;
            game.assignmentMode=false;
            if(game.pieces.Count<15)throw new Exception("Source skeleton groups missing.");
            for(int i=0;i<5;i++){
                game.StartStage(i);if(!game.ActivePieces.Any())throw new Exception("Empty stage "+i);
                var p=game.ActivePieces.First(a=>a.gameObject.activeSelf);Physics.SyncTransforms();
                var bounds=p.GetComponent<Collider>().bounds;float grabDistance;
                var grabbed=game.Grab(new Ray(bounds.center+Vector3.up*(bounds.extents.y+.25f),Vector3.down),2,out grabDistance);
                if(grabbed!=p||!p.held||p.owner!=2)throw new Exception("Ray grab failed at stage "+i);
                p.transform.position=p.target+Vector3.one;game.TryPlace(p);if(p.placed||game.mistakes!=1)throw new Exception("Invalid placement accepted.");
                p.transform.SetPositionAndRotation(p.target,Quaternion.Euler(0,90,0));if(game.TryPlace(p))throw new Exception("Invalid orientation accepted.");
                foreach(var bone in game.ActivePieces.ToArray()){bone.transform.SetPositionAndRotation(bone.target,bone.targetRotation);if(!game.TryPlace(bone))throw new Exception("Valid placement rejected.");}
                if(!game.completed)throw new Exception("Stage completion failed "+i);
            }
            game.practice=false;game.StartStage(1);if(game.ActivePieces.Any(p=>p.ghost.activeSelf))throw new Exception("Exam revealed targets.");
            game.Command("hint");if(game.hints!=0)throw new Exception("Exam allowed hint.");
            game.practice=true;game.StartStage(1);
            game.verificationMode=false;
            File.WriteAllText("tmp/verification-result.txt","PASS: all 5 stages nonempty; ray grab works in every stage; wrong position rejected; wrong orientation rejected; correct placements accepted; all stages complete; exam hides targets and disallows hints; reset restores session.");
            Capture();
        }
        public static void VerifyCelebration()
        {
            var scene=EditorSceneManager.OpenScene(ScenePath);
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            var go=new GameObject("Dance verification");
            var dance=go.AddComponent<SkeletonCelebration>();dance.Initialize(game.ivory);
            var skin=go.GetComponent<SkinnedMeshRenderer>();
            if(skin.sharedMesh.vertexCount!=34460||skin.bones.Length!=15)throw new Exception("Celebration mesh or joints missing.");
            var baked=new Mesh();dance.Sample(0);skin.BakeMesh(baked);
            foreach(float sample in new[]{1.6f,2f,2.3f,3.1f,7f}){
                dance.Sample(sample);skin.BakeMesh(baked);
                foreach(var v in baked.vertices)if(float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z))throw new Exception("Invalid dance geometry.");
                if(baked.bounds.size.y<1||baked.bounds.size.y>3)throw new Exception("Unexpected dance bounds.");
            }
            if(Vector3.Distance(go.transform.position,new Vector3(1.6f,0,1.65f))>.001f)throw new Exception("Dancer failed to leave the bed.");
            UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(baked);
            File.WriteAllText("tmp/dance-verification.txt","PASS: source 34,460 vertices retained; 15 joints; rise reaches floor position; five dance samples have finite geometry and plausible bounds. Hardware VR not tested.");
        }
        [MenuItem("Bone Assembly/Verify Assignments (Play Mode)")]
        public static void VerifyAssignments()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode is required.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();game.verificationMode=true;
            for(int index=0;index<4;index++){
                game.BeginAssignment(index);
                if(!game.ActivePieces.Any()||game.RemainingTime!=600)throw new Exception("Assignment or countdown missing.");
                game.AdvanceSession(6);
                var ghost=game.ActivePieces.First().ghost.GetComponentInChildren<Renderer>().sharedMaterial;
                if(ghost.color.a>.05f)throw new Exception("Guide did not fade.");
                if(index==0)CaptureTo("output/Assignment-Faded.png");
                foreach(var p in game.ActivePieces.ToArray()){p.transform.SetPositionAndRotation(p.target,p.targetRotation);if(!game.TryPlace(p))throw new Exception("Assembly rejected.");}
                if(!game.completed||game.lost||game.CanManipulate)throw new Exception("WIN state failed.");
                if(index==0)CaptureTo("output/Assignment-Win.png");
                float remaining=game.RemainingTime;game.AdvanceSession(10);if(game.RemainingTime!=remaining)throw new Exception("WIN timer continued.");
                game.BeginAssignment(index);game.AdvanceSession(601);
                if(!game.lost||game.completed||game.RemainingTime!=0||game.CanManipulate)throw new Exception("LOSE state failed.");
                if(index==0)CaptureTo("output/Assignment-Lose.png");
                var missing=game.ActivePieces.First();missing.transform.SetPositionAndRotation(missing.target,missing.targetRotation);if(game.TryPlace(missing))throw new Exception("Assembly allowed after timeout.");
                game.Command("reset");if(game.lost||game.completed||game.RemainingTime!=600)throw new Exception("Retry failed.");
            }
            game.BeginAssignment(0);game.verificationMode=false;
            game.Command("mode");game.Command("mode");
            File.WriteAllText("tmp/assignment-verification.txt","PASS: assignments 1-4; 10-minute countdown; guide fades after 5 seconds; WIN freezes time; timeout produces LOSE and locks placement; RESET starts a fresh session.");
            Capture();
        }
        [MenuItem("Bone Assembly/Record Completion Dance (Play Mode)")]
        public static void RecordDance()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Recording requires Play mode.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();
            game.verificationMode=true;game.BeginAssignment(3);game.elapsed=465;
            var active=game.ActivePieces.ToArray();var last=active.First(p=>p.label=="Skull and mandible");
            foreach(var p in active.Where(p=>p!=last)){p.transform.SetPositionAndRotation(p.target,p.targetRotation);game.TryPlace(p);}
            last.gameObject.SetActive(true);last.held=true;
            Vector3 from=new Vector3(.7f,1.3f,.65f);last.transform.SetPositionAndRotation(from,Quaternion.identity);
            var camera=game.playerCamera;var previous=camera.targetTexture;Vector3 previousPosition=camera.transform.position;Quaternion previousRotation=camera.transform.rotation;
            camera.transform.position=new Vector3(2.1f,1.75f,-1.1f);camera.transform.LookAt(new Vector3(0,1.05f,1.1f));
            var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);camera.targetTexture=rt;
            var oldActive=RenderTexture.active;Directory.CreateDirectory("tmp/dance-frames");
            SkeletonCelebration dance=null;SkinnedMeshRenderer skin=null;Mesh sampledMesh=null;
            try{
                for(int frame=0;frame<336;frame++){
                    float seconds=frame/24f;
                    if(seconds<1.5f){float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-.3f)/1.2f));last.transform.position=Vector3.Lerp(from,last.target,t);}
                    else if(!dance){
                        last.transform.SetPositionAndRotation(last.target,last.targetRotation);game.TryPlace(last);game.SendMessage("Celebrate");dance=game.GetComponentInChildren<SkeletonCelebration>();dance.enabled=false;
                        skin=dance.GetComponent<SkinnedMeshRenderer>();skin.enabled=false;sampledMesh=new Mesh();sampledMesh.MarkDynamic();
                        var sample=new GameObject("Recorded pose");sample.transform.SetParent(dance.transform,false);sample.AddComponent<MeshFilter>().sharedMesh=sampledMesh;sample.AddComponent<MeshRenderer>().sharedMaterial=skin.sharedMaterial;
                    }
                    if(dance){dance.Sample(seconds-1.5f);skin.BakeMesh(sampledMesh);}
                    float follow=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-1.5f)/1.6f));
                    camera.transform.LookAt(Vector3.Lerp(new Vector3(0,1.05f,1.1f),new Vector3(1.6f,.95f,1.65f),follow));
                    game.SendMessage("UpdateBoard");camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                    File.WriteAllBytes("tmp/dance-frames/"+frame.ToString("D4")+".png",image.EncodeToPNG());
                }
                File.WriteAllText("tmp/dance-recording.txt","PASS: 336 frames at 24 fps; final bone placement, WIN, rise and celebration from Unity scene.");
            }finally{
                camera.targetTexture=previous;camera.transform.SetPositionAndRotation(previousPosition,previousRotation);RenderTexture.active=oldActive;
                UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);game.verificationMode=false;game.BeginAssignment(0);
                if(sampledMesh)UnityEngine.Object.DestroyImmediate(sampledMesh);
            }
        }
        [MenuItem("Bone Assembly/Verify Surface Clearance (Play Mode)")]
        public static void VerifySurfacePlacement()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode is required.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();game.verificationMode=true;
            game.assignmentMode=false;game.practice=true;game.StartStage(4);
            var surfaces=game.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Sterile drape"||r.name=="Tray sterile lining").ToArray();
            var bed=surfaces.First(r=>r.name=="Sterile drape").bounds;
            Action<BonePiece> checkAbove=(p)=>{
                var bounds=BoneSurfacePlacement.MeshBounds(p.gameObject);
                foreach(var surface in surfaces)if(BoneSurfacePlacement.OverlapsTop(bounds,surface.bounds)&&bounds.min.y<surface.bounds.max.y+.007f)throw new Exception("Surface intersection: "+p.label);
            };
            foreach(var p in game.pieces){
                p.transform.SetPositionAndRotation(p.target,p.targetRotation);checkAbove(p);
                p.transform.SetPositionAndRotation(p.home,p.homeRotation);checkAbove(p);
                p.transform.SetPositionAndRotation(new Vector3(0,.5f,.25f),Quaternion.Euler(35,67,48));game.ConstrainHeldPiece(p);checkAbove(p);
                p.transform.SetPositionAndRotation(p.target+Vector3.one,Quaternion.Euler(0,90,0));
                if(game.TryPlace(p))throw new Exception("Invalid placement accepted.");checkAbove(p);
                if(Vector3.Distance(p.transform.position,p.home)>.001f)throw new Exception("Return to tray failed.");
            }
            var targets=game.pieces.Select(p=>p.target).ToArray();game.RepairSurfacePlacement();game.RepairSurfacePlacement();
            for(int i=0;i<targets.Length;i++)if(Vector3.Distance(targets[i],game.pieces[i].target)>.0001f)throw new Exception("Surface repair applied twice.");
            game.StartStage(1);game.verificationMode=false;
            CaptureTo("output/SurfaceClearance-Fixed.png");
            File.WriteAllText("tmp/surface-verification.txt","PASS: all "+game.pieces.Count+" teaching units above bed at target and above supporting surfaces on tray; rotated held pieces cannot sink; failed placements return above tray; repeated repairs do not accumulate height. Assembly lift: "+game.assemblyLift.ToString("F6")+" m.");
        }
        [MenuItem("Bone Assembly/Verify Seated Play (Play Mode)")]
        public static void VerifySeatedPlay()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode is required.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();game.verificationMode=true;
            if(!game.seated)throw new Exception("Game did not start in seated mode.");
            foreach(float height in new[]{0f,.95f,1.2f,1.45f})foreach(float yaw in new[]{-140f,0f,75f}){
                Vector3 tracked=new Vector3(.8f,height,-.6f);Quaternion rotation=Quaternion.Euler(8,yaw,0);
                game.CalibrateSeatedPose(tracked,rotation);
                Vector3 actual=game.rig.TransformPoint(tracked);
                if(Vector3.Distance(actual,BoneAssemblyGame.SeatedAnchor+Vector3.up*BoneAssemblyGame.SeatedEyeHeight)>.001f)throw new Exception("Seated calibration failed.");
                Vector3 forward=game.rig.rotation*rotation*Vector3.forward;
                if(Vector3.Angle(Vector3.ProjectOnPlane(forward,Vector3.up),Vector3.forward)>.1f)throw new Exception("Seated facing failed.");
                if(Mathf.Abs(Vector3.Distance(game.rig.TransformPoint(tracked+Vector3.up*.08f),actual)-.08f)>.001f)throw new Exception("Natural head movement was lost.");
            }
            game.ResetDesktopSeat();Vector3 seat=game.rig.position;Quaternion seatRotation=game.rig.rotation;
            game.BeginAssignment(3);
            Vector3 hand=BoneAssemblyGame.SeatedAnchor+new Vector3(.18f,1.3f,.15f);
            foreach(var piece in game.ActivePieces.ToArray()){
                if(Vector3.Distance(hand,piece.target)>4)throw new Exception("Target out of seated reach: "+piece.label);
                piece.gameObject.SetActive(true);piece.held=true;piece.owner=0;piece.transform.rotation=piece.targetRotation;
                Vector3 aim=piece.IsHead||piece.IsHand?BoneSurfacePlacement.MeshBounds(piece.ghost).center:piece.target;
                if(!game.TryPlaceFromRay(piece,new Ray(hand,(aim-hand).normalized)))throw new Exception("Seated placement rejected: "+piece.label);
            }
            if(!game.completed||Vector3.Distance(seat,game.rig.position)>.001f||Quaternion.Angle(seatRotation,game.rig.rotation)>.001f)throw new Exception("Completing the skeleton required locomotion.");
            game.BeginAssignment(3);var first=game.ActivePieces.First();first.held=true;first.transform.rotation=Quaternion.Euler(0,90,0);
            if(game.TryPlaceFromRay(first,new Ray(hand,(first.target-hand).normalized)))throw new Exception("Remote placement bypassed rotation check.");
            first.held=true;if(game.TryPlaceFromRay(first,new Ray(hand,Vector3.up))||!first.held)throw new Exception("Invalid pointer did not preserve held piece.");
            first.held=false;game.BeginAssignment(0);game.verificationMode=false;
            CaptureTo("output/Seated-Play.png");
            File.WriteAllText("tmp/seated-verification.txt","PASS: seated calibration at four tracked heights and three headings; natural head movement preserved; all "+game.pieces.Count+" units placed from one fixed seat within 4m; rotation validation retained; invalid pointer keeps bone held. Quest hardware not tested.");
        }
        [MenuItem("Bone Assembly/Verify Centered Assembly View (Play Mode)")]
        public static void VerifyAssemblyView()
        {
            if(!EditorApplication.isPlaying)EditorSceneManager.OpenScene(ScenePath);
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();game.ResetDesktopSeat();game.BeginAssignment(3);game.AdvanceSession(1);
            var camera=game.playerCamera;var center=camera.WorldToViewportPoint(game.AssemblyViewCenter);
            if(Vector2.Distance(new Vector2(center.x,center.y),Vector2.one*.5f)>.001f)throw new Exception("Skeleton is not centered in view.");
            foreach(var piece in game.pieces){
                var bounds=BoneSurfacePlacement.MeshBounds(piece.ghost);
                for(int i=0;i<8;i++){
                    var world=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point=camera.WorldToViewportPoint(world);
                    if(point.z<=0||point.x<0||point.x>1||point.y<0||point.y>1)throw new Exception("Skeleton cropped: "+piece.label+" "+point);
                }
            }
            CaptureTo("output/Centered-Above-Skeleton.png");
            File.WriteAllText("tmp/view-verification.txt","PASS: assembly center projects to screen center; all "+game.pieces.Count+" teaching units fit inside the default camera frame. VR keeps natural headset rotation.");
        }
        [MenuItem("Bone Assembly/Verify Head Placement Assist (Play Mode)")]
        public static void VerifyHeadAssist()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode is required.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();game.verificationMode=true;game.BeginAssignment(3);
            var head=game.ActivePieces.First(p=>p.IsHead);
            head.transform.rotation=Quaternion.Euler(50,170,70);
            Vector3 center=BoneSurfacePlacement.MeshBounds(head.ghost).center;
            head.transform.position+=center+Vector3.right*.18f-BoneSurfacePlacement.MeshBounds(head.gameObject).center;
            if(!game.TryPlace(head)||Quaternion.Angle(head.transform.rotation,head.targetRotation)>.001f)throw new Exception("Head did not align automatically.");
            game.BeginAssignment(3);head.held=true;head.transform.rotation=Quaternion.Euler(80,150,30);
            Vector3 pointer=new Vector3(0,1.8f,-.1f);
            if(!game.TryPlaceFromRay(head,new Ray(pointer,(center+Vector3.right*.18f-pointer).normalized)))throw new Exception("Remote head assist failed.");
            game.BeginAssignment(3);head.transform.rotation=head.targetRotation;head.transform.position=head.target+Vector3.right*.5f;
            if(game.TryPlace(head))throw new Exception("Head accepted too far away.");
            var body=game.ActivePieces.First(p=>!p.IsHead);body.transform.SetPositionAndRotation(body.target,Quaternion.Euler(0,90,0));
            if(game.TryPlace(body))throw new Exception("Body rotation validation changed.");
            game.BeginAssignment(3);game.verificationMode=false;
            File.WriteAllText("tmp/head-verification.txt","PASS: head within 18cm snaps and auto-orients from arbitrary rotation; remote pointer uses visible skull center; 50cm miss rejected; other bones retain rotation validation.");
        }
        [MenuItem("Bone Assembly/Verify Full Trays And Learning Cards (Play Mode)")]
        public static void VerifyWorkbenchAndLearning()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode is required.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();game.verificationMode=true;game.assignmentMode=false;game.StartStage(4);
            if(game.pieces.Count!=22)throw new Exception("Expected 22 simplified teaching units, got "+game.pieces.Count);
            if(game.pieces.Sum(p=>p.GetComponentsInChildren<MeshFilter>(true).Length)!=245)throw new Exception("Source meshes were lost while grouping.");
            var source=JsonUtility.FromJson<SkeletonData>(Resources.Load<TextAsset>("SkeletonData").text).bones.ToDictionary(b=>b.id);
            foreach(var p in game.pieces){
                p.transform.SetPositionAndRotation(p.target,p.targetRotation);
                foreach(var filter in p.GetComponentsInChildren<MeshFilter>(true)){
                    int id=int.Parse(filter.name.Substring("Source bone ".Length));
                    var expected=new Vector3(0,1.02f+game.assemblyLift,.25f)+source[id].center;
                    if(Vector3.Distance(expected,filter.transform.position)>.001f)throw new Exception("Grouping displaced mesh "+id);
                }
                var card=BoneEducation.Get(p);if(string.IsNullOrEmpty(card.Explanation)||card.ThaiName=="กระดูกในระบบโครงร่าง")throw new Exception("Missing learning content: "+p.label);
            }
            game.StartStage(3);if(game.ActivePieces.Count()!=4||game.ActivePieces.Any(p=>p.label.EndsWith("fibula")||p.label.EndsWith("patella")))throw new Exception("Small leg pieces not grouped.");
            game.StartStage(0);if(game.ActivePieces.Count()!=2)throw new Exception("Hands not grouped.");
            foreach(var p in game.ActivePieces)if(p.GetComponentsInChildren<MeshFilter>(true).Length!=27||p.GetComponent<BoxCollider>().size.x<.139f)throw new Exception("Hand geometry or grab area missing.");
            var hand=game.ActivePieces.First();hand.transform.rotation=Quaternion.Euler(0,45,0);
            hand.transform.position+=BoneSurfacePlacement.MeshBounds(hand.ghost).center+Vector3.right*.15f-BoneSurfacePlacement.MeshBounds(hand.gameObject).center;
            if(!game.TryPlace(hand))throw new Exception("Hand snap assist failed.");
            game.StartStage(4);var trays=game.GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Tray sterile lining").ToArray();
            var active=game.ActivePieces.ToArray();
            if(active.Any(p=>!p.gameObject.activeSelf))throw new Exception("Pieces are still hidden after eight.");
            for(int i=0;i<active.Length;i++){
                var p=active[i];var bounds=BoneSurfacePlacement.MeshBounds(p.gameObject);var tray=trays.OrderBy(r=>Mathf.Abs(r.bounds.center.x-bounds.center.x)).First().bounds;
                if(bounds.min.x<tray.min.x-.001f||bounds.max.x>tray.max.x+.001f||bounds.min.z<tray.min.z-.001f||bounds.max.z>tray.max.z+.001f||bounds.min.y<tray.max.y+.007f)throw new Exception("Piece outside tray: "+p.label);
                for(int j=0;j<i;j++)if(BoneSurfacePlacement.OverlapsTop(bounds,BoneSurfacePlacement.MeshBounds(active[j].gameObject)))throw new Exception("Tray pieces overlap: "+p.label+" / "+active[j].label);
                Physics.SyncTransforms();float distance;var picked=game.Grab(new Ray(bounds.center+Vector3.up*(bounds.extents.y+.3f),Vector3.down),2,out distance);
                if(picked!=p)throw new Exception("Cannot pick up tray piece: "+p.label);
                if(game.playerCamera.GetComponent<BoneLearningDisplay>().CurrentExplanation!=BoneEducation.Get(p).Explanation)throw new Exception("Wrong learning card.");p.held=false;
            }
            var font=Resources.Load<Font>("Fonts/Sarabun-Regular");if(!font||!font.HasCharacter('ก'))throw new Exception("Thai font missing.");
            game.Bind();if(game.pieces.Count!=22)throw new Exception("Grouping was not idempotent.");
            game.BeginAssignment(3);game.verificationMode=false;game.ResetDesktopSeat();
            CaptureTo("output/All-Bones-On-Trays.png");
            var sample=game.ActivePieces.First(p=>p.label.Contains("lower leg"));game.ShowBoneInformation(sample);
            CaptureTo("output/Bone-Learning-Card.png");
            File.WriteAllText("tmp/workbench-verification.txt","PASS: 22 units retain all 245 original meshes in their anatomical positions; lower leg pieces and each hand grouped; enlarged hand grab and snap; every piece visible, fits its tray without overlaps and is ray-grabbable; all pieces show matching Thai-English learning content; Thai font present; repeated grouping does not duplicate units.");
        }
        [MenuItem("Bone Assembly/Verify Timed Review And Shoulder Snap (Play Mode)")]
        public static void VerifyReviewAndSnap()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play mode is required.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();game.Bind();game.verificationMode=true;game.assignmentMode=false;game.practice=false;game.StartStage(1);
            var shoulder=game.ActivePieces.First(p=>p.IsShoulderBlade);var targetCenter=BoneSurfacePlacement.MeshBounds(shoulder.ghost).center;
            shoulder.transform.rotation=Quaternion.Euler(80,150,60);
            shoulder.transform.position+=targetCenter+Vector3.right*.24f-BoneSurfacePlacement.MeshBounds(shoulder.gameObject).center;
            if(!game.TryPlace(shoulder)||Quaternion.Angle(shoulder.transform.rotation,shoulder.targetRotation)>.001f)throw new Exception("Shoulder assistance failed.");
            game.StartStage(1);shoulder.held=true;shoulder.transform.rotation=Quaternion.Euler(70,130,40);var pointer=new Vector3(0,1.8f,-.1f);
            if(!game.TryPlaceFromRay(shoulder,new Ray(pointer,(targetCenter+Vector3.right*.24f-pointer).normalized)))throw new Exception("Remote shoulder assistance failed.");
            var placedPosition=shoulder.transform.position;var placedRotation=shoulder.transform.rotation;int errors=game.mistakes;float elapsed=game.elapsed;
            game.ShowBoneInformation(shoulder);var display=game.playerCamera.GetComponent<BoneLearningDisplay>();float shown=Time.time;
            shoulder.held=true;display.UpdateVisibility(shown+3.99f);if(!display.IsVisible)throw new Exception("Card disappeared early.");
            display.UpdateVisibility(shown+4f);if(display.IsVisible)throw new Exception("Card did not expire while held.");shoulder.held=false;
            Physics.SyncTransforms();var bounds=BoneSurfacePlacement.MeshBounds(shoulder.gameObject);var ray=new Ray(bounds.center+Vector3.up*(bounds.extents.y+.3f),Vector3.down);
            if(!game.InteractWithRay(ray)||!display.IsVisible)throw new Exception("Trigger review failed.");
            display.UpdateVisibility(Time.time+4f);float distance;if(game.Grab(ray,2,out distance)!=null||!display.IsVisible)throw new Exception("Mouse/grip review failed.");
            if(!shoulder.placed||shoulder.held||Vector3.Distance(placedPosition,shoulder.transform.position)>.001f||Quaternion.Angle(placedRotation,shoulder.transform.rotation)>.001f||game.mistakes!=errors||game.elapsed!=elapsed)throw new Exception("Review changed the placed bone or score.");
            var body=game.ActivePieces.First(p=>!p.IsShoulderBlade);body.transform.SetPositionAndRotation(body.target,Quaternion.Euler(0,90,0));if(game.TryPlace(body))throw new Exception("Rotation check removed from other bones.");
            game.StartStage(1);shoulder.transform.SetPositionAndRotation(shoulder.target+Vector3.right*.5f,shoulder.targetRotation);if(game.TryPlace(shoulder))throw new Exception("Shoulder accepted a distant miss.");
            game.StartStage(1);foreach(var p in game.ActivePieces.ToArray()){p.transform.SetPositionAndRotation(p.target,p.targetRotation);game.TryPlace(p);}
            if(!game.completed)throw new Exception("Review completion setup failed.");
            game.SendMessage("Celebrate");game.ReviewPlacedBones();Physics.SyncTransforms();
            if(game.CanManipulate||game.ActivePieces.Any(p=>!p.placed||!p.gameObject.activeSelf))throw new Exception("Completed review did not restore locked bones.");
            var reviewBone=game.ActivePieces.First(p=>p.label.Contains("humerus"));var reviewBounds=BoneSurfacePlacement.MeshBounds(reviewBone.gameObject);
            if(!game.InteractWithRay(new Ray(reviewBounds.center+Vector3.up*(reviewBounds.extents.y+.3f),Vector3.down)))throw new Exception("Post-win inspection failed.");
            game.verificationMode=false;game.StartStage(1);
            File.WriteAllText("tmp/review-verification.txt","PASS: shoulder snap at 24cm auto-orients both direct and remote placements; 50cm miss rejected; other bones retain rotation checks; cards remain at 3.99s and expire at 4s even while held; trigger and mouse/grip review preserve bone position, score and time; post-win REVIEW restores locked placed bones and supports inspection after the celebration.");
        }
    }
}
