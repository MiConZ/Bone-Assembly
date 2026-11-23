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
        Quaternion desktopRotation;
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
        readonly float[] reach=new float[2];
        readonly bool[] freezeDown=new bool[2],infoDown=new bool[2];
        void OnEnable()
        {
            if(!Application.isPlaying||!head||!game)return;
            yaw=head.transform.eulerAngles.y;pitch=head.transform.eulerAngles.x;
            for(int i=0;i<2;i++){
                string name=i==0?"Left surgical glove":"Right surgical glove";
                var existing=transform.Find(name);
                if(existing){hands[i]=existing;rays[i]=existing.GetComponentInChildren<LineRenderer>(true);InstallHand(i);continue;}
                var h=new GameObject(name);h.transform.SetParent(transform,false);hands[i]=h.transform;
                game.Shape("Glove",PrimitiveType.Sphere,Vector3.zero,new Vector3(.07f,.055f,.1f),game.teal,false,h.transform);
                var line=new GameObject("Pointer").AddComponent<LineRenderer>();line.transform.SetParent(h.transform,false);line.sharedMaterial=game.glow;line.positionCount=2;line.startWidth=.004f;line.endWidth=.001f;rays[i]=line;InstallHand(i);
            }
        }
        void InstallHand(int i)
        {
            if(hands[i].GetComponentInChildren<ControllerHandVisual>(true))return;
            var prefab=game.settings?(i==0?game.settings.leftHandPrefab:game.settings.rightHandPrefab):null;
            if(!prefab)return;
            var glove=hands[i].Find("Glove");if(glove)glove.gameObject.SetActive(false);
            var model=Instantiate(prefab,hands[i],false);if(!model.GetComponent<ControllerHandVisual>())model.AddComponent<ControllerHandVisual>();
        }
        void OnDisable(){foreach(var bone in held)if(bone&&bone.held)bone.Drop();if(desktop)desktop.Drop();}
        bool XRActive()=>XRSettings.isDeviceActive;
        void Update()
        {
            if(!hands[0]||!hands[1])OnEnable();
            bool xr=XRActive();
            if(xr&&!wasXR){seatCalibrated=false;transform.rotation=Quaternion.identity;head.transform.localRotation=Quaternion.identity;var systems=new List<XRInputSubsystem>();SubsystemManager.GetSubsystems(systems);foreach(var s in systems)s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);}
            wasXR=xr;
            if(xr){
                var center=InputDevices.GetDeviceAtXRNode(XRNode.Head);
                bool hasPosition=center.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 pos);
                bool hasRotation=center.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion rot);
                if(hasPosition)head.transform.localPosition=pos;
                if(hasRotation)head.transform.localRotation=rot;
                bool tracked=!center.TryGetFeatureValue(CommonUsages.isTracked,out bool isTracked)||isTracked;
                if(!seatCalibrated&&hasPosition&&hasRotation&&tracked){game.CalibrateSeatedPose(pos,rot);seatCalibrated=true;}
                Hand(0,XRNode.LeftHand);Hand(1,XRNode.RightHand);
            }else{
                for(int i=0;i<2;i++){hands[i].gameObject.SetActive(false);if(held[i]){held[i].Drop();held[i]=null;}}
                Desktop();
            }
        }
        void CaptureGrip(int i,BonePiece bone,bool magnet)
        {
            held[i]=bone;
            for(int other=0;other<2;other++)if(other!=i&&held[other]==bone)held[other]=null;
            rotations[i]=Quaternion.Inverse(hands[i].rotation)*bone.transform.rotation;
            offsets[i]=hands[i].InverseTransformPoint(bone.transform.position);
            if(magnet){
                Vector3 centre=BoneSurfacePlacement.MeshBounds(bone.gameObject).center-bone.transform.position;
                offsets[i]=Vector3.forward*holdDistance-Quaternion.Inverse(hands[i].rotation)*centre;
            }
            reach[i]=holdDistance;
            bone.Follow(hands[i].TransformPoint(offsets[i]),hands[i].rotation*rotations[i]);
            bone.SetMotion(magnet?BoneMotionState.Attracting:BoneMotionState.Held,i);
            game.ShowBoneInformation(bone);
        }
        void Hand(int i,XRNode node)
        {
            var device=InputDevices.GetDeviceAtXRNode(node);
            bool tracked=device.isValid&&(!device.TryGetFeatureValue(CommonUsages.isTracked,out bool value)||value);
            hands[i].gameObject.SetActive(tracked);
            if(!tracked){if(held[i]&&held[i].owner==i)held[i].Drop();held[i]=null;gripDown[i]=triggerDown[i]=freezeDown[i]=infoDown[i]=false;return;}
            if(device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p))hands[i].localPosition=p;
            if(device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q))hands[i].localRotation=q;
            device.TryGetFeatureValue(CommonUsages.gripButton,out bool grip);
            device.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);
            device.TryGetFeatureValue(CommonUsages.primaryButton,out bool freeze);
            device.TryGetFeatureValue(CommonUsages.secondaryButton,out bool info);
            device.TryGetFeatureValue(CommonUsages.grip,out float gripAmount);
            device.TryGetFeatureValue(CommonUsages.trigger,out float triggerAmount);
            hands[i].GetComponentInChildren<ControllerHandVisual>()?.Pose(gripAmount,triggerAmount);
            var ray=new Ray(hands[i].position,hands[i].forward);
            if(held[i]&&(!held[i].held||held[i].owner!=i))held[i]=null;
            if(grip&&!gripDown[i]&&!held[i]&&game.CanManipulate){
                BonePiece found=null;float nearest=.14f;
                foreach(var bone in game.ActivePieces){
                    if(!bone.gameObject.activeSelf||bone.placed)continue;
                    float distance=bone.DistanceTo(hands[i].position);
                    if(distance<nearest){nearest=distance;found=bone;}
                }
                if(found)CaptureGrip(i,found,false);
                else {var bone=game.Grab(ray,i,out float distance);if(bone)CaptureGrip(i,bone,true);}
            }
            if(held[i]){
                var bone=held[i];
                if(!grip){bone.Drop();held[i]=null;}
                else {
                    if(freeze&&!freezeDown[i])bone.SetMotion(BoneMotionState.Regripping,i);
                    if(!freeze&&bone.Motion==BoneMotionState.Regripping)CaptureGrip(i,bone,false);
                    if(bone.Motion!=BoneMotionState.Regripping){
                        if(device.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick)&&Mathf.Abs(stick.y)>.2f){
                            float next=Mathf.Clamp(reach[i]+stick.y*1.2f*Time.deltaTime,holdDistance,game.settings?game.settings.maximumReach:3.5f);
                            offsets[i]+=Vector3.forward*(next-reach[i]);reach[i]=next;
                        }
                        bone.Follow(hands[i].TransformPoint(offsets[i]),hands[i].rotation*rotations[i]);
                    }
                }
            }
            if(trigger&&!triggerDown[i]){
                if(held[i]){
                    if(held[i].Motion==BoneMotionState.Held){bool ok=game.TryPlace(held[i]);device.SendHapticImpulse(0,ok?.45f:.18f,.15f);if(ok)held[i]=null;}
                    else game.message="Finish pickup or release A / X before submitting.";
                }else game.InteractWithRay(ray);
            }
            if(info&&!infoDown[i])head.GetComponent<BoneLearningDisplay>()?.Toggle();
            rays[i].SetPosition(0,ray.origin);
            rays[i].SetPosition(1,held[i]?BoneSurfacePlacement.MeshBounds(held[i].gameObject).center:ray.GetPoint(4));
            gripDown[i]=grip;triggerDown[i]=trigger;freezeDown[i]=freeze;infoDown[i]=info;
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
                float scale=Mathf.Max(.75f,Screen.height/900f);var m=mouse.position.ReadValue();bool overPanel=game.desktopUI ? UnityEngine.EventSystems.EventSystem.current&&UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject() : m.x<310*scale&&m.y>Screen.height-650*scale;
                if(!overPanel){if(desktop){desktop.Drop();desktop=null;}else if(!game.InteractWithRay(ray)){desktop=game.Grab(ray,2,out depth);if(desktop){desktop.SetMotion(BoneMotionState.Held,2);desktopRotation=desktop.transform.rotation;}}}
            }
            if(desktop){
                depth=Mathf.Clamp(depth+mouse.scroll.ReadValue().y*.001f,.25f,3.5f);
                float turn=((keyboard.eKey.isPressed?1:0)-(keyboard.qKey.isPressed?1:0))*100*Time.deltaTime;
                desktopRotation=Quaternion.AngleAxis(turn,keyboard.shiftKey.isPressed?Vector3.right:Vector3.up)*desktopRotation;
                desktop.Follow(ray.GetPoint(depth),desktopRotation);
                if(keyboard.enterKey.wasPressedThisFrame&&game.TryPlace(desktop))desktop=null;
            }
            if(keyboard.bKey.wasPressedThisFrame)head.GetComponent<BoneLearningDisplay>()?.Toggle();
            if(keyboard.backspaceKey.wasPressedThisFrame)game.Command("recover");
            if(keyboard.hKey.wasPressedThisFrame)game.Command("hint");if(keyboard.rKey.wasPressedThisFrame)game.Command("reset");
        }
    }
}
