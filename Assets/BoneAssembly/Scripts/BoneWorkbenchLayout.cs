using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace BoneAssembly
{
    public static class BoneWorkbenchLayout
    {
        public static void Simplify(BoneAssemblyGame game)
        {
            foreach(string side in new[]{"Left ","Right "}){
                Merge(game,side+"tibia",new[]{side+"fibula",side+"patella"},side+"lower leg (tibia + fibula + patella)");
                Merge(game,side+"wrist and palm",new[]{side+"fingers"},side+"hand");
            }
        }
        static void Merge(BoneAssemblyGame game,string primaryName,string[] otherNames,string name)
        {
            var primary=game.pieces.FirstOrDefault(p=>p.label==primaryName);
            if(!primary)return;
            var members=game.pieces.Where(p=>p==primary||otherNames.Contains(p.label)).ToArray();
            bool allPlaced=members.All(p=>p.placed);
            primary.transform.SetPositionAndRotation(primary.target,primary.targetRotation);
            primary.ghost.transform.SetPositionAndRotation(primary.target,primary.targetRotation);
            foreach(var source in members.Where(p=>p!=primary)){
                source.transform.SetPositionAndRotation(source.target,source.targetRotation);
                source.ghost.transform.SetPositionAndRotation(source.target,source.targetRotation);
                foreach(var child in source.transform.Cast<Transform>().ToArray())child.SetParent(primary.transform,true);
                foreach(var child in source.ghost.transform.Cast<Transform>().ToArray())child.SetParent(primary.ghost.transform,true);
                source.retired=true;source.held=false;source.gameObject.SetActive(false);source.ghost.SetActive(false);
                game.pieces.Remove(source);
                if(Application.isPlaying){Object.Destroy(source.ghost);Object.Destroy(source.gameObject);}else{Object.DestroyImmediate(source.ghost);Object.DestroyImmediate(source.gameObject);}
            }
            primary.label=name;primary.name=name;primary.ghost.name="Target | "+name;
            primary.placed=allPlaced;primary.held=false;primary.owner=-1;
            primary.surfaces=primary.GetComponentsInChildren<Renderer>(true);
            foreach(var renderer in primary.surfaces)renderer.sharedMaterial=allPlaced?game.success:game.ivory;
            FitCollider(primary);
        }
        public static void FitCollider(BonePiece piece)
        {
            var bounds=BoneSurfacePlacement.MeshBounds(piece.gameObject);
            var collider=piece.GetComponent<BoxCollider>();
            collider.center=piece.transform.InverseTransformPoint(bounds.center);
            collider.size=Vector3.Max(bounds.size,Vector3.one*(piece.IsHand?.14f:.035f));
        }
        public static void ResizeTrays(BoneAssemblyGame game)
        {
            foreach(Transform item in game.transform){
                if(item.name!="Instrument trolley"&&item.name!="Tray sterile lining"&&item.name!="Trolley leg"&&item.name!="Caster")continue;
                float side=item.localPosition.x<0?-1:1;var position=item.localPosition;position.x=side*1.25f;
                if(item.name=="Instrument trolley"){position.z=.25f;item.localScale=new Vector3(1.08f,.08f,2.5f);}
                else if(item.name=="Tray sterile lining"){position.z=.25f;item.localScale=new Vector3(1.02f,.015f,2.44f);}
                else position.z=.25f+(position.z<.25f?-1:1)*1.05f;
                item.localPosition=position;
            }
        }
        public static void Arrange(List<BonePiece> pieces)
        {
            var items=pieces.Select(p=>{
                p.transform.SetPositionAndRotation(p.target,p.targetRotation);
                var b=BoneSurfacePlacement.MeshBounds(p.gameObject);
                return (piece:p,bounds:b,offset:b.center-p.transform.position);
            }).OrderByDescending(item=>item.bounds.size.z).ToArray();
            for(int sideIndex=0;sideIndex<2;sideIndex++){
                float side=sideIndex==0?-1:1,cursor=1.40f;
                var shelf=items.Where((item,index)=>index%2==sideIndex).ToArray();
                for(int row=0;row<shelf.Length;row+=2){
                    float depth=shelf.Skip(row).Take(2).Max(item=>item.bounds.size.z);
                    for(int column=0;column<2&&row+column<shelf.Length;column++){
                        var item=shelf[row+column];
                        var center=new Vector3(side*1.25f+(column==0?-.25f:.25f),1.04f,cursor-depth*.5f);
                        item.piece.home=center-new Vector3(item.offset.x,0,item.offset.z);
                        item.piece.homeRotation=item.piece.targetRotation;
                    }
                    cursor-=depth+.08f;
                }
            }
        }
    }
}
