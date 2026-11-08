using UnityEngine;
namespace BoneAssembly
{
    public class BonePiece : MonoBehaviour
    {
        public string label, region;
        public bool IsHead => label=="Skull and mandible";
        public bool IsHand => region=="HAND";
        public bool IsShoulderBlade => label!=null&&label.EndsWith("scapula");
        [HideInInspector] public bool retired;
        public Vector3 target, home;
        public Quaternion targetRotation, homeRotation;
        public bool placed, held;
        public int owner = -1;
        public GameObject ghost;
        public Renderer[] surfaces;
    }
}
