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
        Transform backdrop;
        public BoneStation Station { get; private set; }
        public BoneConsole Console { get; private set; }
        Vector3 pivot,boardDimensions;
        float distance,height;
        readonly Dictionary<BonePiece,Vector3> positions=new Dictionary<BonePiece,Vector3>();
        readonly Dictionary<BonePiece,Quaternion> rotations=new Dictionary<BonePiece,Quaternion>();
        readonly Dictionary<BonePiece,Vector3> scales=new Dictionary<BonePiece,Vector3>();
        public void Initialize(BoneAssemblyGame source)
        {
            if(Board)return;game=source;
            Station=BoneStation.Prepare(game);
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
            Size=game.settings?game.settings.boardScale:.6f;
            Console=Station.console.gameObject.AddComponent<BoneConsole>();
            Console.Initialize(game);
            Apply(false);
        }
        public bool Adjust(string command)
        {
            bool resize=command=="smaller"||command=="larger"||command=="defaults";
            if(resize&&game.pieces.Any(p=>p.held)){game.message="Release held bones before changing blueprint size.";return false;}
            switch(command){
                case "raise":height=Mathf.Min(.7f,height+.1f);break;
                case "lower":height=Mathf.Max(-.8f,height-.1f);break;
                case "near":distance=Mathf.Max(-.4f,distance-.1f);break;
                case "far":distance=Mathf.Min(.9f,distance+.1f);break;
                case "smaller":Size=Mathf.Max(.35f,Size-.05f);break;
                case "larger":Size=Mathf.Min(1f,Size+.05f);break;
                case "defaults":Size=game.settings?game.settings.boardScale:.6f;distance=0;height=0;break;
                case "recenter":game.Command("seated");return true;
                default:return false;
            }
            Apply(resize);return true;
        }
        void Apply(bool resetLoose)
        {
            Board.SetPositionAndRotation(Station.blueprint.TransformPoint(new Vector3(0,height,distance)),Station.blueprint.rotation*Quaternion.Euler(-90,0,0));
            // Backing is visual only so it cannot obstruct a manually aligned bone.
            backdrop.localScale=Vector3.Scale(boardDimensions,new Vector3(Size,1,Size));
            foreach(var p in game.pieces){
                p.target=Board.TransformPoint(positions[p]*Size);p.targetRotation=Board.rotation*rotations[p];
                p.ghost.transform.SetPositionAndRotation(p.target,p.targetRotation);p.ghost.transform.localScale=scales[p]*Size;
                p.transform.localScale=scales[p]*Size;
                if(p.placed)p.Teleport(p.target,p.targetRotation);
            }
            if(resetLoose){game.RefreshTrayHomes();game.ResetLooseBones();}
            Physics.SyncTransforms();
        }
    }
}
