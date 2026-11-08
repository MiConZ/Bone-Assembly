using UnityEngine;

namespace BoneAssembly
{
    public static class BoneSurfacePlacement
    {
        // Include hidden meshes; Renderer.bounds can be stale after an inactive object moves.
        public static Bounds MeshBounds(GameObject model)
        {
            bool initialized=false;var result=new Bounds();
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>(true)){
                if(!filter.sharedMesh)continue;
                var box=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++){
                    var corner=box.center+Vector3.Scale(box.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var world=filter.transform.TransformPoint(corner);
                    if(!initialized){result=new Bounds(world,Vector3.zero);initialized=true;}else result.Encapsulate(world);
                }
            }
            return result;
        }
        public static bool OverlapsTop(Bounds piece,Bounds surface)
        {
            return piece.max.x>surface.min.x&&piece.min.x<surface.max.x&&piece.max.z>surface.min.z&&piece.min.z<surface.max.z;
        }
    }
}
