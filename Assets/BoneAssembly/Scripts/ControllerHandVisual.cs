using System;
using UnityEngine;

namespace BoneAssembly
{
    // Bind the imported rig's local open/closed poses in the hand prefab.
    public class ControllerHandVisual : MonoBehaviour
    {
        [Serializable] public struct FingerJoint
        {
            public Transform joint;
            public Vector3 openEuler,closedEuler;
            public bool indexFinger;
        }
        public FingerJoint[] joints=Array.Empty<FingerJoint>();
        float grip,trigger;
        public void Pose(float gripValue,float triggerValue){grip=gripValue;trigger=triggerValue;}
        void LateUpdate()
        {
            foreach(var finger in joints)if(finger.joint)
                finger.joint.localRotation=Quaternion.Slerp(Quaternion.Euler(finger.openEuler),Quaternion.Euler(finger.closedEuler),finger.indexFinger?trigger:grip);
        }
    }
}
