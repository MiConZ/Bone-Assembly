using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BoneAssembly
{
    // Targets live in board coordinates; tray homes remain in room coordinates.
    public class BoneWorkspace : MonoBehaviour
    {
        public Transform Board { get; private set; }
        public float Size { get; private set; }
        BoneAssemblyGame game;
        Transform controls,session,feedbackPanel,backdrop;
        Vector3 pivot,viewPosition,boardDimensions;
        Quaternion facing;
        float distance,height;
        readonly Dictionary<BonePiece,Vector3> positions=new Dictionary<BonePiece,Vector3>();
        readonly Dictionary<BonePiece,Quaternion> rotations=new Dictionary<BonePiece,Quaternion>();
        readonly Dictionary<BonePiece,Vector3> scales=new Dictionary<BonePiece,Vector3>();
        public void Initialize(BoneAssemblyGame source)
        {
            if(Board)return;game=source;
            var bounds=BoneSurfacePlacement.MeshBounds(game.pieces[0].ghost);
            foreach(var p in game.pieces)bounds.Encapsulate(BoneSurfacePlacement.MeshBounds(p.ghost));pivot=bounds.center;
            boardDimensions=new Vector3(bounds.size.x+.15f,.025f,bounds.size.z+.15f);
            Board=new GameObject("Upright anatomy blueprint").transform;Board.SetParent(transform,false);
            foreach(var p in game.pieces){
                positions[p]=p.target-pivot;rotations[p]=p.targetRotation;scales[p]=p.transform.localScale;
                p.ghost.transform.SetParent(Board,true);
                foreach(var c in p.ghost.GetComponentsInChildren<Collider>())c.enabled=false;
            }
            backdrop=game.Shape("Blueprint board backing",PrimitiveType.Cube,new Vector3(0,-.12f,0),new Vector3(bounds.size.x+.15f,.025f,bounds.size.z+.15f),game.dark,false,Board).transform;
            foreach(var t in game.GetComponentsInChildren<Transform>(true))
                if(new[]{"Table pedestal","Table base","Operating table","Sterile drape","Table rail"}.Contains(t.name))t.gameObject.SetActive(false);
            controls=new GameObject("Comfort controls").transform;controls.SetParent(transform,false);
            var backing=game.Shape("Comfort panel",PrimitiveType.Cube,Vector3.zero,new Vector3(.68f,1.05f,.025f),game.dark,false,controls);
            AddText("Controls","CONTROLS\nGrip: grab / magnet\nRelease Grip: drop\nStick: extend / retract\nTrigger: submit / select\nHold A / X: float & regrip\nB / Y: show / hide info",new Vector3(0,.31f,-.025f),.012f);
            AddButton("RESET LOOSE BONES","recover",new Vector3(0,.04f,-.035f),.60f);
            AddButton("HEIGHT -","board:lower",new Vector3(-.16f,-.08f,-.035f),.29f);
            AddButton("HEIGHT +","board:raise",new Vector3(.16f,-.08f,-.035f),.29f);
            AddButton("NEARER","board:near",new Vector3(-.16f,-.20f,-.035f),.29f);
            AddButton("FARTHER","board:far",new Vector3(.16f,-.20f,-.035f),.29f);
            AddButton("SMALLER","board:smaller",new Vector3(-.16f,-.32f,-.035f),.29f);
            AddButton("LARGER","board:larger",new Vector3(.16f,-.32f,-.035f),.29f);
            AddButton("RECENTER","board:recenter",new Vector3(-.16f,-.44f,-.035f),.29f);
            AddButton("DEFAULTS","board:defaults",new Vector3(.16f,-.44f,-.035f),.29f);
            // Move the existing session controls as a group, preserving their authored relative layout.
            session=new GameObject("Accessible session console").transform;session.SetParent(transform,false);
            session.position=new Vector3(0,1.8f,2.24f);
            foreach(var t in game.GetComponentsInChildren<Transform>(true).ToArray()){
                if(t.GetComponentInParent<Canvas>())continue;
                if(t.IsChildOf(controls)||t.IsChildOf(Board)||t==session||t.IsChildOf(session))continue;
                if(t.name=="Session console"||t.name=="Title"||t.name=="Subtitle"||t.name=="StatusBoard"||t.name=="Feedback"||t.name=="Assignment selection"||t.name.StartsWith("Button ")||t.name.StartsWith("Label "))t.SetParent(session,true);
            }
            session.localScale=Vector3.one*.48f;
            controls.localScale=Vector3.one*.75f;
            // Decorative overlay text must not show through the usable panels.
            var vitals=game.FindNamed("Vitals");if(vitals)vitals.gameObject.SetActive(false);
            var oldFeedback=game.FindNamed("VR feedback background");if(oldFeedback)oldFeedback.gameObject.SetActive(false);
            feedbackPanel=new GameObject("Placement feedback").transform;feedbackPanel.SetParent(transform,false);
            game.Shape("Feedback background",PrimitiveType.Cube,Vector3.zero,new Vector3(.85f,.18f,.02f),game.dark,false,feedbackPanel);
            var feedbackText=game.GetComponentsInChildren<TextMesh>(true).FirstOrDefault(t=>t.name=="Feedback");
            var feedback=feedbackText?feedbackText.transform:null;
            if(feedback){feedback.SetParent(feedbackPanel,false);feedback.localPosition=Vector3.back*.025f;feedback.localRotation=Quaternion.identity;feedback.localScale=Vector3.one;feedback.GetComponent<TextMesh>().characterSize=.008f;}
            Size=game.settings?game.settings.boardScale:.6f;distance=game.settings?game.settings.boardDistance:.9f;height=game.settings?game.settings.boardHeightOffset:-.2f;
            Recenter();
        }
        void AddText(string name,string text,Vector3 position,float size)
        {
            var label=game.Label(name,text,Vector3.zero,size,Color.white);label.transform.SetParent(controls,false);label.transform.localPosition=position;
        }
        void AddButton(string label,string command,Vector3 position,float width)
        {
            var button=game.Shape(label,PrimitiveType.Cube,position,new Vector3(width,.095f,.025f),game.teal,true,controls);
            button.AddComponent<LabAction>().command=command;AddText(label+" caption",label,position+Vector3.back*.02f,.014f);
        }
        public void Recenter()
        {
            if(!Board)return;
            viewPosition=game.playerCamera.transform.position;
            facing=Quaternion.Euler(0,game.playerCamera.transform.eulerAngles.y,0);
            Apply(false);
        }
        public bool Adjust(string command)
        {
            bool resize=command=="smaller"||command=="larger"||command=="defaults";
            if(resize&&game.pieces.Any(p=>p.held)){game.message="Release held bones before changing blueprint size.";return false;}
            switch(command){
                case "raise":height=Mathf.Min(.5f,height+.1f);break;
                case "lower":height=Mathf.Max(-.8f,height-.1f);break;
                case "near":distance=Mathf.Max(.5f,distance-.1f);break;
                case "far":distance=Mathf.Min(1.8f,distance+.1f);break;
                case "smaller":Size=Mathf.Max(.35f,Size-.05f);break;
                case "larger":Size=Mathf.Min(1f,Size+.05f);break;
                case "defaults":Size=game.settings?game.settings.boardScale:.6f;distance=game.settings?game.settings.boardDistance:.9f;height=game.settings?game.settings.boardHeightOffset:-.2f;break;
                case "recenter":Recenter();return true;
                default:return false;
            }
            Apply(resize);return true;
        }
        void Apply(bool resetLoose)
        {
            Board.SetPositionAndRotation(viewPosition+facing*new Vector3(0,height,distance),facing*Quaternion.Euler(-90,0,0));
            // Backing is visual only so it cannot obstruct a manually aligned bone.
            backdrop.localScale=Vector3.Scale(boardDimensions,new Vector3(Size,1,Size));
            foreach(var p in game.pieces){
                p.target=Board.TransformPoint(positions[p]*Size);p.targetRotation=Board.rotation*rotations[p];
                p.ghost.transform.SetPositionAndRotation(p.target,p.targetRotation);p.ghost.transform.localScale=scales[p]*Size;
                p.transform.localScale=scales[p]*Size;
                if(p.placed)p.Teleport(p.target,p.targetRotation);
            }
            controls.SetPositionAndRotation(viewPosition+facing*new Vector3(-.75f,.28f,1.05f),facing*Quaternion.Euler(0,-30,0));
            session.SetPositionAndRotation(viewPosition+facing*new Vector3(.95f,-.05f,1.15f),facing*Quaternion.Euler(0,30,0));
            feedbackPanel.SetPositionAndRotation(viewPosition+facing*new Vector3(0,.62f,1.05f),facing);
            if(resetLoose){game.RefreshTrayHomes();game.ResetLooseBones();}
            Physics.SyncTransforms();
        }
    }
}
