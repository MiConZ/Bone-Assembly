using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BoneAssembly.Editor
{
    [InitializeOnLoad]
    public static class BoneComfortTools
    {
        const string Folder="Assets/BoneAssembly/Hands";
        static BoneComfortTools(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(SessionState.GetBool("BoneComfort.PendingVerification",false)&&EditorApplication.isPlaying&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating){
                SessionState.SetBool("BoneComfort.PendingVerification",false);EditorApplication.isPaused=false;BoneComfortVerification.Start();
            }
            const string request="tmp/comfort-command.txt";
            if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            string action=File.ReadAllText(request).Trim();File.Delete(request);
            try{
                if(action=="hands")PrepareHands();
                if(action=="refresh")AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                if(action=="android"){
                    if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before building.");
                    File.WriteAllText(request,"androidbuild");
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);
                }
                if(action=="androidbuild"){
                    Directory.CreateDirectory("output/Quest");
                    var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/BoneAssemblyVR.unity"},target=BuildTarget.Android,locationPathName="output/Quest/BoneAssemblyVR.apk",options=BuildOptions.Development});
                    File.WriteAllText("tmp/quest-build.txt",result.summary.result+"; errors="+result.summary.totalErrors+"; warnings="+result.summary.totalWarnings);
                }
                if(action=="verify"){
                    if(!EditorApplication.isPlaying){SessionState.SetBool("BoneComfort.PendingVerification",true);EditorApplication.isPlaying=true;}
                    else {EditorApplication.isPaused=false;BoneComfortVerification.Start();}
                }
                if(action=="status")File.WriteAllText("tmp/comfort-status.txt","Playing="+EditorApplication.isPlaying+"; compiling="+EditorApplication.isCompiling);
            }catch(Exception e){File.WriteAllText("tmp/comfort-error.txt",e.ToString());Debug.LogException(e);}
        }
        [MenuItem("Bone Assembly/Prepare Controller Hands")]
        public static void PrepareHands()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before preparing hand assets.");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PolyOne/Free VR Hands/Prefabs/Free Pack - VR Hands ( Rigged ).prefab");
            if(!source)throw new Exception("Import PolyOne Free Pack - VR Hands first.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var settings=AssetDatabase.LoadAssetAtPath<BoneGameSettings>("Assets/BoneAssembly/Configuration/BoneGameSettings.asset");
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/SurgicalGlove.mat");
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+"/SurgicalGlove.mat");}
            mat.color=new Color(.12f,.53f,.65f);mat.SetFloat("_Smoothness",.25f);
            var report=new System.Text.StringBuilder();
            for(int i=0;i<2;i++){
                string side=i==0?"Left":"Right";
                var root=new GameObject(side+" surgical hand");
                try{
                    var model=UnityEngine.Object.Instantiate(source,root.transform);
                    foreach(var animator in model.GetComponentsInChildren<Animator>())UnityEngine.Object.DestroyImmediate(animator);
                    var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
                    // The vendor prefab is posed. Restore the bind pose before recording an open hand.
                    var bind=skin.sharedMesh.bindposes;
                    var boneMatrices=skin.bones.Select((bone,index)=>new {bone,matrix=skin.transform.localToWorldMatrix*bind[index].inverse}).ToArray();
                    foreach(var entry in boneMatrices.OrderBy(e=>AnimationUtility.CalculateTransformPath(e.bone,model.transform).Count(c=>c=='/')))
                        entry.bone.SetPositionAndRotation(entry.matrix.GetColumn(3),entry.matrix.rotation);
                    var mesh=UnityEngine.Object.Instantiate(skin.sharedMesh);mesh.name=side+" hand mesh";
                    var weights=mesh.boneWeights;var triangles=mesh.triangles;var kept=new List<int>();
                    Func<int,bool> belongs=v=>skin.bones[weights[v].boneIndex0].name.Contains(side);
                    for(int t=0;t<triangles.Length;t+=3)if(belongs(triangles[t])&&belongs(triangles[t+1])&&belongs(triangles[t+2])){kept.Add(triangles[t]);kept.Add(triangles[t+1]);kept.Add(triangles[t+2]);}
                    if(kept.Count==0||kept.Count==triangles.Length)throw new Exception("Could not isolate "+side+" hand geometry.");
                    mesh.subMeshCount=1;mesh.SetTriangles(kept,0);mesh.RecalculateBounds();
                    string meshPath=Folder+"/"+side+"Hand.asset";
                    var prior=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if(prior){EditorUtility.CopySerialized(mesh,prior);UnityEngine.Object.DestroyImmediate(mesh);mesh=prior;}else AssetDatabase.CreateAsset(mesh,meshPath);
                    skin.sharedMesh=mesh;skin.sharedMaterial=mat;skin.updateWhenOffscreen=true;
                    var all=model.GetComponentsInChildren<Transform>();
                    var wrist=all.Single(t=>t.name=="J_"+side+"_Hand");
                    var middle=all.Single(t=>t.name=="J_"+side+"_HandMiddle1");
                    var index=all.Single(t=>t.name=="J_"+side+"_HandIndex1");
                    var pinky=all.Single(t=>t.name=="J_"+side+"_HandPinky1");
                    Vector3 forward=(middle.position-wrist.position).normalized;
                    Vector3 right=(index.position-pinky.position).normalized*(i==0?1:-1);
                    Vector3 up=Vector3.Cross(forward,right).normalized;
                    model.transform.rotation=Quaternion.Inverse(Quaternion.LookRotation(forward,up));
                    model.transform.position=-wrist.position+new Vector3(0,-.015f,-.035f);
                    var visual=root.AddComponent<ControllerHandVisual>();
                    visual.joints=all.Where(t=>t.name.StartsWith("J_"+side+"_Hand")&&char.IsDigit(t.name[t.name.Length-1])&&!t.name.EndsWith("4")).Select(t=>{
                        Vector3 axis=t.InverseTransformDirection(Vector3.right);
                        float curl=t.name.Contains("Thumb")?35:70;
                        return new ControllerHandVisual.FingerJoint{joint=t,openEuler=t.localEulerAngles,closedEuler=(t.localRotation*Quaternion.AngleAxis(curl,axis)).eulerAngles,indexFinger=t.name.Contains("Index")};
                    }).ToArray();
                    var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/"+side+"Hand.prefab");
                    if(i==0)settings.leftHandPrefab=prefab;else settings.rightHandPrefab=prefab;
                    report.AppendLine(side+": "+kept.Count/3+" triangles, "+visual.joints.Length+" animated joints");
                }finally{UnityEngine.Object.DestroyImmediate(root);}
            }
            settings.resetLabel="RESTART ASSIGNMENT";settings.recenterLabel="RECENTER WORKSPACE";
            settings.seatedInstructions="Grip: grab / magnet | Trigger: submit | A / X: regrip";
            EditorUtility.SetDirty(settings);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssets();
            File.WriteAllText("tmp/hand-setup.txt",report.ToString());
        }
    }
}
