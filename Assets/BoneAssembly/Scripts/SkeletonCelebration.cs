using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BoneAssembly
{
    // Original procedural celebration using the supplied mesh; no stock animation is required.
    public class SkeletonCelebration : MonoBehaviour
    {
        readonly Dictionary<string, Transform> joints = new Dictionary<string, Transform>();
        readonly Dictionary<string, Quaternion> rest = new Dictionary<string, Quaternion>();
        float started;
        Vector3 standingPosition = new Vector3(1.6f, 0, 1.65f);
        Vector3 lyingPosition = new Vector3(0,1.02f,-.6845f);
        static Vector3 Upright(Vector3 lying) => new Vector3(lying.x, lying.z + .9345f, -lying.y);

        public void Initialize(Material material,Vector3? assemblyOrigin=null)
        {
            if(assemblyOrigin.HasValue)lyingPosition=assemblyOrigin.Value-Vector3.forward*.9345f;
            Joint("Hips", null, new Vector3(0, 1.02f, 0));
            Joint("Chest", "Hips", new Vector3(0, 1.3f, 0));
            Joint("Head", "Chest", new Vector3(0, 1.63f, 0));
            foreach (string side in new[] { "L", "R" })
            {
                float s = side == "L" ? 1 : -1;
                Joint(side+"Arm", "Chest", new Vector3(s*.15f, 1.54f, .025f));
                Joint(side+"Forearm", side+"Arm", new Vector3(s*.33f, 1.27f, .025f));
                Joint(side+"Hand", side+"Forearm", new Vector3(s*.44f, 1.12f, 0));
                Joint(side+"Thigh", "Hips", new Vector3(s*.075f, 1, 0));
                Joint(side+"Shin", side+"Thigh", new Vector3(s*.085f, .51f, 0));
                Joint(side+"Foot", side+"Shin", new Vector3(s*.085f, .08f, .025f));
            }
            var bones = new List<Transform>(joints.Values);
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var weights = new List<BoneWeight>();
            var data = JsonUtility.FromJson<SkeletonData>(Resources.Load<TextAsset>("SkeletonData").text);
            foreach (var source in data.bones)
            {
                int bone = bones.IndexOf(joints[JointFor(source)]), offset = vertices.Count;
                foreach (var vertex in source.vertices)
                {
                    vertices.Add(Upright(vertex + source.center));
                    weights.Add(new BoneWeight { boneIndex0 = bone, weight0 = 1 });
                }
                foreach (int index in source.triangles) triangles.Add(offset + index);
            }
            var mesh = new Mesh { name = "Celebration skeleton", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.boneWeights = weights.ToArray();
            var bind = new Matrix4x4[bones.Count];
            for (int i=0;i<bind.Length;i++) bind[i] = bones[i].worldToLocalMatrix * transform.localToWorldMatrix;
            mesh.bindposes = bind; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var skin = gameObject.AddComponent<SkinnedMeshRenderer>(); skin.sharedMesh = mesh; skin.sharedMaterial = material;
            skin.bones = bones.ToArray(); skin.rootBone = joints["Hips"];
            skin.localBounds = new Bounds(Vector3.up, new Vector3(3, 4, 3));
            started = Time.time;
            Sample(0);
        }
        void Joint(string name, string parent, Vector3 point)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(parent == null ? transform : joints[parent], false);
            joint.position = transform.TransformPoint(point);
            joints.Add(name, joint); rest.Add(name, joint.localRotation);
        }
        static string JointFor(BoneMeshData b)
        {
            string side=b.sourceCenter.x<0?"R":"L";
            switch (b.id)
            {
                case 54:case 220: return side+"Thigh";
                case 55:case 221:case 56:case 222:case 57:case 223: return side+"Shin";
                case 61:case 228:return side+"Arm";
                case 59:case 60:case 226:case 227:return side+"Forearm";
                case 58:case 225:case 224:case 245:return "Chest";
                case 62:case 63:case 66:return "Hips";
            }
            float x=Mathf.Abs(b.sourceCenter.x),z=b.sourceCenter.z;
            if(z<1.5f)return side+"Foot";
            if(x>3.3f&&z<12.8f&&z>8)return side+"Hand";
            if(z>15.5f)return "Head";
            return z>11?"Chest":"Hips";
        }
        void LateUpdate() => Sample(Time.time-started);
        public void Sample(float seconds)
        {
            float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(seconds/1.6f));
            transform.localPosition=Vector3.Lerp(lyingPosition,standingPosition,rise)+Vector3.up*(Mathf.Sin(rise*Mathf.PI)*.3f);
            transform.localRotation=Quaternion.Slerp(Quaternion.Euler(90,0,0),Quaternion.identity,rise);
            foreach(var entry in joints)entry.Value.localRotation=rest[entry.Key];
            joints["Hips"].localPosition=new Vector3(0,1.02f,0);
            if(seconds<1.6f)return;
            float beat=(seconds-1.6f)*Mathf.PI*2*2.15f, bounce=Mathf.Abs(Mathf.Sin(beat));
            // Horse-riding rhythm: crossed arms, alternating knee lifts, hip bounce.
            joints["Hips"].localPosition+=new Vector3(Mathf.Sin(beat*.5f)*.055f,bounce*.055f,0);
            Pose("Hips",new Vector3(0,Mathf.Sin(beat*.5f)*9,Mathf.Sin(beat)*5));
            Pose("Chest",new Vector3(-6,Mathf.Sin(beat*.5f)*-10,0));
            Pose("Head",new Vector3(Mathf.Sin(beat)*7,Mathf.Sin(beat*.5f)*8,0));
            Pose("LArm",new Vector3(40,0,-38+bounce*5));
            Pose("RArm",new Vector3(45,0,38-bounce*5));
            Pose("LForearm",new Vector3(55,0,-65));
            Pose("RForearm",new Vector3(55,0,65));
            Pose("LHand",new Vector3(0,0,-15));Pose("RHand",new Vector3(0,0,15));
            float left=Mathf.Max(0,Mathf.Sin(beat)),right=Mathf.Max(0,-Mathf.Sin(beat));
            Pose("LThigh",new Vector3(left*28,0,9));Pose("RThigh",new Vector3(right*28,0,-9));
            Pose("LShin",new Vector3(-left*42,0,0));Pose("RShin",new Vector3(-right*42,0,0));
            Pose("LFoot",new Vector3(left*14,0,-5));Pose("RFoot",new Vector3(right*14,0,5));
        }
        void Pose(string name,Vector3 euler)=>joints[name].localRotation=rest[name]*Quaternion.Euler(euler);
        void OnDestroy()
        {
            var renderer=GetComponent<SkinnedMeshRenderer>();
            if(renderer&&renderer.sharedMesh){if(Application.isPlaying)Destroy(renderer.sharedMesh);else DestroyImmediate(renderer.sharedMesh);}
        }
    }
}
