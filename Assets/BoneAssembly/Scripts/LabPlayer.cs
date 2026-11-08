using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using System.Collections.Generic;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace BoneAssembly
{
    // Uses XR's device API directly: either controller can hold a separate teaching unit.
    public class LabPlayer : MonoBehaviour
    {
        public BoneAssemblyGame game; public Camera head;
        [Tooltip("Distance from the controller to the centre of a held bone, in metres.")]
        [Range(.05f, .3f)] public float holdDistance = .12f;
        BonePiece desktop; float depth=1.2f,yaw,pitch; bool wasXR;
        bool seatCalibrated;
        public void RecenterSeat()
        {
            seatCalibrated=false;
            if(!XRActive()){game.ResetDesktopSeat();yaw=head.transform.eulerAngles.y;pitch=head.transform.eulerAngles.x;}
        }
        readonly BonePiece[] held=new BonePiece[2]; readonly bool[] gripDown=new bool[2],triggerDown=new bool[2];
        readonly Transform[] hands=new Transform[2]; readonly LineRenderer[] rays=new LineRenderer[2];
        readonly Vector3[] offsets=new Vector3[2]; readonly Quaternion[] rotations=new Quaternion[2];
        void OnEnable()
        {
            if(!Application.isPlaying||!head||!game)return;
            yaw=head.transform.eulerAngles.y;pitch=head.transform.eulerAngles.x;
            for(int i=0;i<2;i++){
                string name=i==0?"Left surgical glove":"Right surgical glove";
                var existing=transform.Find(name);
                if(existing){hands[i]=existing;rays[i]=existing.GetComponentInChildren<LineRenderer>();continue;}
                var h=new GameObject(name);h.transform.SetParent(transform,false);hands[i]=h.transform;
                game.Shape("Glove",PrimitiveType.Sphere,Vector3.zero,new Vector3(.07f,.055f,.1f),game.teal,false,h.transform);
                var line=new GameObject("Pointer").AddComponent<LineRenderer>();line.transform.SetParent(h.transform,false);line.sharedMaterial=game.glow;line.positionCount=2;line.startWidth=.004f;line.endWidth=.001f;rays[i]=line;
            }
        }
        bool XRActive()=>XRSettings.isDeviceActive;
        void Update()
        {
            if(!hands[0]||!hands[1])OnEnable();
            bool xr=XRActive();
            if(xr&&!wasXR){transform.rotation=Quaternion.identity;head.transform.localRotation=Quaternion.identity;var systems=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(systems);foreach(var s in systems)s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);}
            wasXR=xr;
            if(xr){
                var center=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                bool hasPosition=center.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 pos);
                bool hasRotation=center.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion rot);
                if(hasPosition)head.transform.localPosition=pos;
                if(hasRotation)head.transform.localRotation=rot;
                bool tracked=!center.TryGetFeatureValue(CommonUsages.isTracked,out bool isTracked)||isTracked;
                if(game.seated&&!seatCalibrated&&hasPosition&&hasRotation&&tracked){game.CalibrateSeatedPose(pos,rot);seatCalibrated=true;}
                Hand(0,XRNode.LeftHand);Hand(1,XRNode.RightHand);
            }else{
                foreach(var h in hands)h.gameObject.SetActive(false);
                Desktop();
            }
        }
        void Hand(int i,XRNode node)
        {
            var device=InputDevices.GetDeviceAtXRNode(node);hands[i].gameObject.SetActive(device.isValid);if(!device.isValid){if(held[i]){game.TryPlace(held[i]);held[i]=null;}gripDown[i]=triggerDown[i]=false;return;}
            if(device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p))hands[i].localPosition=p;
            if(device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q))hands[i].localRotation=q;
            device.TryGetFeatureValue(CommonUsages.gripButton,out bool grip);device.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);
            var ray=new Ray(hands[i].position,hands[i].forward);rays[i].SetPosition(0,ray.origin);rays[i].SetPosition(1,ray.GetPoint(4));
            if(grip&&!gripDown[i]){
                // Direct grab in reach first; ray grab provides comfortable access from seated mode.
                BonePiece found=null;float nearest=.2f;
                foreach(var bone in game.ActivePieces){if(!game.CanManipulate||!bone.gameObject.activeSelf||bone.placed||bone.held)continue;float d=Vector3.Distance(bone.GetComponent<Collider>().ClosestPoint(hands[i].position),hands[i].position);if(d<nearest){nearest=d;found=bone;}}
                float dist=0;held[i]=found??game.Grab(ray,i,out dist);
                if(held[i]){
                    held[i].held=true;held[i].owner=i;
                    game.ShowBoneInformation(held[i]);
                    if(held[i].IsHead)game.message="HEAD / Aim near the skull outline. Rotation aligns automatically.";
                    if(held[i].IsShoulderBlade)game.message="SHOULDER BLADE / Aim near the outline. Rotation aligns automatically.";
                    rotations[i]=Quaternion.Inverse(hands[i].rotation)*held[i].transform.rotation;
                    // Bring the visible centre to the hand, including pieces whose pivot is off-centre.
                    var collider=held[i].GetComponent<BoxCollider>();
                    Vector3 centreOffset=held[i].transform.TransformVector(collider.center);
                    offsets[i]=Vector3.forward*holdDistance-Quaternion.Inverse(hands[i].rotation)*centreOffset;
                }
            }
            if(held[i]){
                if(!held[i].held){held[i]=null;}
                else {held[i].transform.SetPositionAndRotation(hands[i].TransformPoint(offsets[i]),hands[i].rotation*rotations[i]);game.ConstrainHeldPiece(held[i]);if(!grip&&gripDown[i]){bool ok=game.TryPlace(held[i]);device.SendHapticImpulse(0,ok?.45f:.18f,.15f);held[i]=null;}}
            }
            if(trigger&&!triggerDown[i]){
                if(held[i]&&game.seated){
                    bool ok=game.TryPlaceFromRay(held[i],ray);device.SendHapticImpulse(0,ok?.45f:.18f,.15f);
                    if(!held[i].held)held[i]=null;
                }else game.InteractWithRay(ray);
            }
            gripDown[i]=grip;triggerDown[i]=trigger;
        }
        void Desktop()
        {
            var mouse=Mouse.current;var keyboard=Keyboard.current;if(mouse==null||keyboard==null)return;
            if(mouse.rightButton.isPressed){var delta=mouse.delta.ReadValue();yaw+=delta.x*.14f;pitch=Mathf.Clamp(pitch-delta.y*.14f,-75,80);head.transform.rotation=Quaternion.Euler(pitch,yaw,0);}
            Vector3 forward=Vector3.ProjectOnPlane(head.transform.forward,Vector3.up).normalized,right=Vector3.Cross(Vector3.up,forward);
            Vector3 move=(keyboard.wKey.isPressed?forward:Vector3.zero)-(keyboard.sKey.isPressed?forward:Vector3.zero)+(keyboard.dKey.isPressed?right:Vector3.zero)-(keyboard.aKey.isPressed?right:Vector3.zero);
            if(!game.seated)transform.position+=move*Time.deltaTime;var position=transform.position;position.x=Mathf.Clamp(position.x,-3.2f,3.2f);position.z=Mathf.Clamp(position.z,-3.2f,3.2f);transform.position=position;
            var ray=head.ScreenPointToRay(mouse.position.ReadValue());
            if(desktop&&!desktop.held)desktop=null;
            if(mouse.leftButton.wasPressedThisFrame){
                // Screen-space controls consume clicks in their own rectangles.
                float scale=Mathf.Max(.75f,Screen.height/900f);var m=mouse.position.ReadValue();bool overPanel=m.x<310*scale&&m.y>Screen.height-650*scale;
                if(!overPanel){if(desktop){game.TryPlace(desktop);desktop=null;}else desktop=game.Grab(ray,2,out depth);}
            }
            if(desktop){depth=Mathf.Clamp(depth+mouse.scroll.ReadValue().y*.001f,.25f,3.5f);desktop.transform.position=ray.GetPoint(depth);
                float turn=((keyboard.eKey.isPressed?1:0)-(keyboard.qKey.isPressed?1:0))*100*Time.deltaTime;
                desktop.transform.Rotate(keyboard.shiftKey.isPressed?Vector3.right:Vector3.up,turn,Space.World);
                if(keyboard.fKey.wasPressedThisFrame)desktop.transform.rotation=desktop.targetRotation;
                game.ConstrainHeldPiece(desktop);
            }
            if(keyboard.hKey.wasPressedThisFrame)game.Command("hint");if(keyboard.rKey.wasPressedThisFrame)game.Command("reset");
        }
    }
}
