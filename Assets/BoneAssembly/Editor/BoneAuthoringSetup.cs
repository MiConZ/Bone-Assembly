using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace BoneAssembly.Editor
{
    public static class BoneAuthoringSetup
    {
        [MenuItem("Bone Assembly/Fix VR Panel Layout")]
        public static void FixVRPanels()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            Undo.RegisterFullObjectHierarchyUndo(game.gameObject,"Separate VR feedback and controls");
            var feedback=game.FindNamed("Feedback");
            feedback.position=new Vector3(1.65f,1.88f,2.17f);
            var text=feedback.GetComponent<TextMesh>();text.characterSize=.012f;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            game.feedbackColumns=34;
            text.text=BoneEducation.Wrap(game.message,game.feedbackColumns);
            var panel=game.FindNamed("VR feedback background");
            if(!panel){panel=game.Box("VR feedback background",new Vector3(1.65f,1.88f,2.22f),new Vector3(1.1f,.64f,.035f),game.dark,false).transform;panel.SetParent(feedback.parent,true);}
            foreach(string command in new[]{"seated","text","sound"})
            {
                var label=game.FindNamed("Label "+command);if(label)label.GetComponent<TextMesh>().characterSize=.0085f;
            }
            for(int i=0;i<5;i++){var label=game.FindNamed("Label stage:"+i);if(label)label.GetComponent<TextMesh>().characterSize=.0075f;}
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);EditorSceneManager.SaveScene(game.gameObject.scene);
            File.WriteAllText("tmp/vr-layout-result.txt","PASS: feedback moved to separate right panel; compact control captions; scene saved.");
        }

        public static void VerifyVRPanels()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            if(!EditorApplication.isPlaying)throw new Exception("Play Mode required.");
            game.BeginAssignment(3);game.Command("seated");Physics.SyncTransforms();
            var feedback=game.FindNamed("Feedback").GetComponent<Renderer>();
            foreach(string command in new[]{"seated","text","sound","hint","reset","next"})
            {
                var button=game.FindNamed("Button "+command);var collider=button.GetComponent<Collider>();
                if(feedback.bounds.Intersects(collider.bounds))throw new Exception("Feedback overlaps "+command);
                var ray=new Ray(game.playerCamera.transform.position,(collider.bounds.center-game.playerCamera.transform.position).normalized);
                if(!Physics.Raycast(ray,out var hit,4)||hit.collider!=collider)throw new Exception("VR ray blocked for "+command);
            }
            bool sound=game.soundEnabled;var soundButton=game.FindNamed("Button sound");
            if(!game.InteractWithRay(new Ray(soundButton.position-Vector3.forward*.3f,Vector3.forward))||game.soundEnabled==sound)throw new Exception("Trigger command failed.");
            game.soundEnabled=sound;
            File.WriteAllText("tmp/vr-panel-verification.txt","PASS: feedback clear of all 6 controls; physics rays reach all 6 colliders; Trigger interaction toggles sound. Physical Quest not tested.");
        }
        [MenuItem("Bone Assembly/Prepare Editable Scene")]
        public static void Prepare()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode before preparing the scene.");
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            if(!game)throw new Exception("Open BoneAssemblyVR scene first.");
            // Keep the current scene and all object references; never rebuild the room.
            Directory.CreateDirectory("tmp");
            if(File.Exists(game.gameObject.scene.path))File.Copy(game.gameObject.scene.path,"tmp/BoneAssemblyVR-before-authoring.unity",true);
            Undo.RegisterFullObjectHierarchyUndo(game.gameObject,"Prepare editable Bone Assembly scene");
            Directory.CreateDirectory("Assets/BoneAssembly/Configuration");
            const string path="Assets/BoneAssembly/Configuration/BoneGameSettings.asset";
            var settings=game.settings?game.settings:AssetDatabase.LoadAssetAtPath<BoneGameSettings>(path);
            if(!settings){settings=ScriptableObject.CreateInstance<BoneGameSettings>();settings.assignmentSeconds=game.assignmentDuration;settings.startSeated=game.startSeated;settings.headSnapDistance=game.headSnapDistance;settings.handSnapDistance=game.handSnapDistance;AssetDatabase.CreateAsset(settings,path);}
            game.settings=settings;
            game.Bind();
            foreach(var piece in game.pieces)
            {
                if(settings.bones.Any(b=>b.boneKey==piece.label))continue;
                var card=BoneEducation.Get(piece);
                settings.bones.Add(new BoneInformation{boneKey=piece.label,thaiName=card.ThaiName,englishName=card.EnglishName,explanation=card.Explanation});
            }
            game.RefreshAssignmentControls();
            Organize(game);
            if(!game.desktopUI)CreateDesktopUI(game);
            var learning=game.playerCamera.GetComponent<BoneLearningDisplay>();
            if(!learning)learning=game.playerCamera.gameObject.AddComponent<BoneLearningDisplay>();
            learning.settings=settings;
            if(!learning.panel)learning.CreatePanel();
            learning.panel.SetActive(false);
            // Persist the generated card's materials so the edit-time panel survives reopening.
            foreach(var renderer in game.GetComponentsInChildren<Renderer>(true))
            {
                var mat=renderer.sharedMaterial;
                if(mat&&!AssetDatabase.Contains(mat))AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath("Assets/BoneAssembly/Configuration/SceneMaterial.mat"));
            }
            game.authoringPrepared=true;
            EditorUtility.SetDirty(settings);EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            EditorSceneManager.SaveScene(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            Selection.activeObject=game.gameObject;
            File.WriteAllText("tmp/authoring-result.txt","PASS: existing scene preserved; grouped hierarchy; editable Canvas and learning card; settings and "+settings.bones.Count+" education entries saved. No player build.");
        }

        public static void Verify()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<BoneAssemblyGame>();
            if(!EditorApplication.isPlaying||!game||!game.authoringPrepared)throw new Exception("Prepared scene in Play Mode required.");
            if(!game.settings||game.settings.bones.Count!=22||game.pieces.Count!=22)throw new Exception("Settings or bones missing.");
            if(game.transform.childCount!=7)throw new Exception("Unexpected ungrouped objects: "+game.transform.childCount);
            var ui=game.desktopUI;
            if(!ui||!ui.status||!ui.feedback)throw new Exception("Canvas references missing.");
            game.verificationMode=true;
            for(int assignment=0;assignment<4;assignment++)
            {
                ui.assignments[assignment].onClick.Invoke();
                if(game.assignmentIndex!=assignment||game.selectingAssignment||!game.ActivePieces.Any())throw new Exception("UI assignment failed.");
                foreach(var piece in game.ActivePieces.ToArray())
                {
                    piece.transform.SetPositionAndRotation(piece.target,piece.targetRotation);
                    if(!game.TryPlace(piece))throw new Exception("Cannot assemble "+piece.label);
                }
                if(!game.completed)throw new Exception("Completion failed.");
                game.ReviewPlacedBones();var bone=game.ActivePieces.First();game.ShowBoneInformation(bone);
                var card=game.playerCamera.GetComponent<BoneLearningDisplay>();
                if(!card.IsVisible||card.CurrentExplanation!=game.settings.Information(bone).Explanation)throw new Exception("Editable education failed.");
                card.UpdateVisibility(Time.time+game.settings.informationSeconds+.1f);
                if(card.IsVisible)throw new Exception("Information duration failed.");
            }
            game.verificationMode=false;
            File.WriteAllText("tmp/authoring-verification.txt","PASS: 7 hierarchy groups; 22 bones and editable explanations; Canvas buttons start all 4 assignments; all assignments assemble and complete; review card reads ScriptableObject and expires at configured duration. Hardware VR not tested.");
        }

        static Transform Group(Transform parent,string name)
        {
            var existing=parent.Find(name);if(existing)return existing;
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;
        }
        static void Organize(BoneAssemblyGame game)
        {
            var root=game.transform;
            var room=Group(root,"01 Environment");var architecture=Group(room,"Architecture");var trim=Group(room,"Joints and seams");
            var equipment=Group(room,"Cabinets and equipment");var lights=Group(root,"02 Lighting");
            var workbench=Group(root,"03 Workbench");var bed=Group(workbench,"Operating table");var trays=Group(workbench,"Instrument trays");
            var bones=Group(root,"04 Bones - pick up");var targets=Group(root,"05 Targets - placement guides");
            var ui=Group(root,"06 UI");var world=Group(ui,"World panels - VR");var player=Group(root,"07 Player and VR");
            foreach(var child in root.Cast<Transform>().ToArray())
            {
                if(child==room||child==lights||child==workbench||child==bones||child==targets||child==ui||child==player)continue;
                string n=child.name;Transform parent;
                if(child.GetComponent<BonePiece>())parent=Group(bones,child.GetComponent<BonePiece>().region);
                else if(n.StartsWith("Target |"))parent=targets;
                else if(child.GetComponent<LabPlayer>()||n=="Player Rig")parent=player;
                else if(n=="Assignment selection"||child.GetComponent<TextMesh>()||child.GetComponent<LabAction>()||n=="Session console")parent=world;
                else if(child.GetComponent<Light>()||n.Contains("Lamp")||n.Contains("lamp")||n=="Ceiling light")parent=lights;
                else if(n=="Floor joint"||n=="Wall seam")parent=trim;
                else if(n=="Floor"||n=="Ceiling"||n.EndsWith("wall"))parent=architecture;
                else if(n.Contains("Trolley")||n.Contains("trolley")||n=="Caster"||n=="Tray sterile lining")parent=trays;
                else if(n.StartsWith("Table ")||n=="Operating table"||n=="Sterile drape")parent=bed;
                else parent=equipment;
                child.SetParent(parent,true);
            }
            // Keep each button's caption with its collider so moving it moves both.
            foreach(var action in world.GetComponentsInChildren<LabAction>(true))
            {
                if(!action.name.StartsWith("Button "))continue;
                var label=game.FindNamed("Label "+action.name.Substring(7));
                if(label)label.SetParent(action.transform,true);
            }
        }

        static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=position;rt.sizeDelta=size;return rt;
        }
        static Text Text(string name,Transform parent,string value,Vector2 pos,Vector2 size,int fontSize=22)
        {
            var rt=Rect(name,parent,pos,size);var text=rt.gameObject.AddComponent<Text>();
            text.font=Resources.Load<Font>("Fonts/Sarabun-Regular");text.text=value;text.fontSize=fontSize;text.color=Color.white;text.raycastTarget=false;
            text.verticalOverflow=VerticalWrapMode.Overflow;return text;
        }
        static Button Button(Transform parent,string caption,string command,float y,float x=20,float width=260)
        {
            var rt=Rect("Button - "+command,parent,new Vector2(x,-y),new Vector2(width,42));var image=rt.gameObject.AddComponent<Image>();image.color=new Color(.04f,.24f,.27f,.97f);
            var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=image;rt.gameObject.AddComponent<BoneUICommand>().command=command;
            var text=Text("Caption",rt,caption,new Vector2(4,-4),new Vector2(width-8,34),19);text.alignment=TextAnchor.MiddleCenter;return button;
        }
        static void CreateDesktopUI(BoneAssemblyGame game)
        {
            var root=Group(game.transform,"06 UI");var go=new GameObject("Desktop Canvas - edit layout here",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(root,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=1;
            var ui=go.AddComponent<BoneDesktopUI>();ui.game=game;ui.canvas=canvas;game.desktopUI=ui;
            var panel=Rect("Module menu",go.transform,new Vector2(20,-20),new Vector2(300,710));panel.gameObject.AddComponent<Image>().color=new Color(.015f,.04f,.055f,.94f);
            Text("Title - edit text",panel,"BONE ASSEMBLY",new Vector2(20,-15),new Vector2(270,45),25);
            Text("Section title - edit text",panel,"SELECT ANATOMY MODULE",new Vector2(20,-65),new Vector2(270,35),18);
            ui.assignments=new Button[4];for(int i=0;i<4;i++)ui.assignments[i]=Button(panel,game.settings.assignmentPrefix+" "+(i+1),"assignment:"+i,110+i*50);
            Button(panel,game.settings.testLabel,"test",320);
            var hint=Button(panel,game.settings.hintLabel,"hint",380,20,125);ui.hintCaption=hint.GetComponentInChildren<Text>();
            Button(panel,game.settings.resetLabel,"reset",380,155,125);Button(panel,game.settings.nextLabel,"next",430);
            Button(panel,game.settings.recenterLabel,"seated",480);Button(panel,game.settings.textSizeLabel,"text",530);Button(panel,game.settings.soundLabel,"sound",580);
            Text("Controls - edit text",panel,"Grip: pick up / Trigger: place\nQ / E: rotate / F: align\nClick placed bone: information",new Vector2(20,-630),new Vector2(265,75),16);
            var status=Rect("Session status",go.transform,new Vector2(-280,-20),new Vector2(260,120));status.anchorMin=status.anchorMax=new Vector2(1,1);status.gameObject.AddComponent<Image>().color=new Color(.015f,.04f,.055f,.94f);
            ui.status=Text("Live session values",status,"SESSION",new Vector2(10,-10),new Vector2(240,100));ui.status.alignment=TextAnchor.MiddleCenter;
            var feedback=Rect("Feedback",go.transform,new Vector2(340,20),new Vector2(1200,80));feedback.anchorMin=new Vector2(0,0);feedback.anchorMax=new Vector2(1,0);feedback.pivot=new Vector2(0,0);feedback.sizeDelta=new Vector2(-360,80);
            feedback.gameObject.AddComponent<Image>().color=new Color(.015f,.04f,.055f,.94f);ui.feedback=Text("Live feedback",feedback,"Select an assignment",new Vector2(15,-10),new Vector2(1100,65),22);ui.feedback.alignment=TextAnchor.MiddleCenter;
            if(!UnityEngine.Object.FindAnyObjectByType<EventSystem>()){var events=new GameObject("UI Event System",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(root,false);}
        }
    }
}
