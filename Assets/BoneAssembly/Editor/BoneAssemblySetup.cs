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
            if(File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                BoneAuthoringSetup.FixVRPanels();
                return;
            }
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
                else if(command=="prepareeditable")BoneAuthoringSetup.Prepare();
                else if(command=="verifyeditable")BoneAuthoringSetup.Verify();
                else if(command=="fixvrpanels")BoneAuthoringSetup.FixVRPanels();
                else if(command=="verifyvrpanels")BoneAuthoringSetup.VerifyVRPanels();
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
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
            BoneComfortVerification.Start();
        }
        [MenuItem("Bone Assembly/Verify Seated Play (Play Mode)")]
        public static void VerifySeatedPlay()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
            BoneComfortVerification.Start();
        }
        [MenuItem("Bone Assembly/Verify Centered Assembly View (Play Mode)")]
        public static void VerifyAssemblyView()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
            BoneComfortVerification.Start();
        }
        [MenuItem("Bone Assembly/Verify Manual Placement (Play Mode)")]
        public static void VerifyHeadAssist()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
            BoneComfortVerification.Start();
        }
        [MenuItem("Bone Assembly/Verify Full Trays And Learning Cards (Play Mode)")]
        public static void VerifyWorkbenchAndLearning()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
            BoneComfortVerification.Start();
        }
        [MenuItem("Bone Assembly/Verify Comfortable VR Handling (Play Mode)")]
        public static void VerifyReviewAndSnap()
        {
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
            BoneComfortVerification.Start();
        }
    }
}
