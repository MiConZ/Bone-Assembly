using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.InputSystem.XR;
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
        [System.Serializable]
        public class HandCalibration
        {
            [Tooltip("Orient the glove fingers along the controller's reported aim. Grabbing always uses the grip pose.")]
            public bool useAimForGlove = true;
            public Vector3 glovePosition, gloveEuler;
            public Vector3 fallbackAimPosition, fallbackAimEuler;
        }
        public HandCalibration leftHand = new HandCalibration(), rightHand = new HandCalibration();
        readonly ControllerHandVisual[] visuals = new ControllerHandVisual[2];
        readonly InputAction[] aim = new InputAction[2];
        static readonly InputFeatureUsage<Vector3> pointerPosition = new InputFeatureUsage<Vector3>("PointerPosition");
        static readonly InputFeatureUsage<Quaternion> pointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");

        Ray Pointer(int index, HandCalibration calibration, UnityEngine.XR.InputDevice device, out Quaternion gloveBasis)
        {
            gloveBasis = hands[index].rotation;
            if (aim[index]?.activeControl is PoseControl control)
            {
                var pose = control.ReadValue();
                const InputTrackingState required = InputTrackingState.Position | InputTrackingState.Rotation;
                if (pose.isTracked && (pose.trackingState & required) == required)
                {
                    gloveBasis = transform.rotation * pose.rotation;
                    return new Ray(transform.TransformPoint(pose.position), gloveBasis * Vector3.forward);
                }
            }
            // LinuxXR publishes aim as separate XR features instead of an Input System PoseControl.
            if (device.TryGetFeatureValue(pointerPosition, out var position) &&
                device.TryGetFeatureValue(pointerRotation, out var rotation) && Quaternion.Dot(rotation, rotation) > .5f)
            {
                gloveBasis = transform.rotation * rotation;
                return new Ray(transform.TransformPoint(position), gloveBasis * Vector3.forward);
            }
            var grip = hands[index];
            return new Ray(grip.TransformPoint(calibration.fallbackAimPosition),
                grip.rotation * Quaternion.Euler(calibration.fallbackAimEuler) * Vector3.forward);
        }
        readonly Vector3[] calibratedForward = { Vector3.forward, Vector3.forward };
        readonly string[] calibrationDevice = new string[2];
        readonly bool[] stickDown = new bool[2];

        Ray AlignPointer(int hand, UnityEngine.XR.InputDevice device, Ray raw, Quaternion basis)
        {
            string name = device.name;
            bool changed = calibrationDevice[hand] != name;
            if (changed)
            {
                calibrationDevice[hand] = name;
                string key = $"BoneAssembly.Pointer.{name}.{hand}.";
                calibratedForward[hand] = new Vector3(PlayerPrefs.GetFloat(key+"x",0),
                    PlayerPrefs.GetFloat(key+"y",0), PlayerPrefs.GetFloat(key+"z",1)).normalized;
            }
            device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool pressed);
            if (!changed && pressed && !stickDown[hand] && !held[hand])
            {
                // User holds this controller parallel to their view and clicks its stick.
                var direction = Quaternion.Inverse(basis) * head.transform.forward;
                calibratedForward[hand] = direction;
                string key = $"BoneAssembly.Pointer.{name}.{hand}.";
                PlayerPrefs.SetFloat(key+"x",direction.x);
                PlayerPrefs.SetFloat(key+"y",direction.y);
                PlayerPrefs.SetFloat(key+"z",direction.z);
                PlayerPrefs.Save();
                device.SendHapticImpulse(0,.35f,.1f);
                game.message = (hand == 0 ? "Left" : "Right") + " pointer calibrated.";
            }
            stickDown[hand] = pressed;
            return new Ray(raw.origin, basis * calibratedForward[hand]);
        }

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
                if (aim[i] == null)
                {
                    var side = i == 0 ? "LeftHand" : "RightHand";
                    aim[i] = new InputAction("Aim " + side, InputActionType.Value, $"<XRController>{{{side}}}/pointer");
                }
                aim[i].Enable();
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
            visuals[i]=hands[i].GetComponentInChildren<ControllerHandVisual>(true);
            if(visuals[i])return;
            var prefab=game.settings?(i==0?game.settings.leftHandPrefab:game.settings.rightHandPrefab):null;
            if(!prefab)return;
            var glove=hands[i].Find("Glove");if(glove)glove.gameObject.SetActive(false);
            var model=Instantiate(prefab,hands[i],false);visuals[i]=model.GetComponent<ControllerHandVisual>() ?? model.AddComponent<ControllerHandVisual>();
        }
        void OnDestroy(){foreach(var action in aim)action?.Dispose();}
        void OnDisable(){foreach(var action in aim)action?.Disable();foreach(var bone in held)if(bone&&bone.held)bone.Drop();if(desktop)desktop.Drop();}
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
                for(int i=0;i<2;i++){game.Workspace.Console.ClearHover(i);hands[i].gameObject.SetActive(false);if(held[i]){held[i].Drop();held[i]=null;}}
                Desktop();
            }
        }
        void CaptureGrip(int i,BonePiece bone,bool magnet, Pose pose)
        {
            held[i]=bone;
            for(int other=0;other<2;other++)if(other!=i&&held[other]==bone)held[other]=null;
            rotations[i]=Quaternion.Inverse(pose.rotation)*bone.transform.rotation;
            offsets[i]=Quaternion.Inverse(pose.rotation)*(bone.transform.position-pose.position);
            if(magnet){
                Vector3 centre=BoneSurfacePlacement.MeshBounds(bone.gameObject).center-bone.transform.position;
                offsets[i]=Vector3.forward*holdDistance-Quaternion.Inverse(pose.rotation)*centre;
            }
            reach[i]=holdDistance;
            bone.Follow(pose.position + pose.rotation * offsets[i], pose.rotation * rotations[i]);
            bone.SetMotion(magnet?BoneMotionState.Attracting:BoneMotionState.Held,i);
            game.ShowBoneInformation(bone);
        }
        void Hand(int i,XRNode node)
        {
            var device=InputDevices.GetDeviceAtXRNode(node);
            bool tracked=device.isValid&&(!device.TryGetFeatureValue(CommonUsages.isTracked,out bool value)||value);
            hands[i].gameObject.SetActive(tracked);
            if(!tracked){game.Workspace.Console.ClearHover(i);if(held[i]&&held[i].owner==i)held[i].Drop();held[i]=null;gripDown[i]=triggerDown[i]=freezeDown[i]=infoDown[i]=false;return;}
            if(device.TryGetFeatureValue(CommonUsages.devicePosition,out Vector3 p))hands[i].localPosition=p;
            if(device.TryGetFeatureValue(CommonUsages.deviceRotation,out Quaternion q))hands[i].localRotation=q;
            device.TryGetFeatureValue(CommonUsages.gripButton,out bool grip);
            device.TryGetFeatureValue(CommonUsages.triggerButton,out bool trigger);
            device.TryGetFeatureValue(CommonUsages.primaryButton,out bool freeze);
            device.TryGetFeatureValue(CommonUsages.secondaryButton,out bool info);
            device.TryGetFeatureValue(CommonUsages.grip,out float gripAmount);
            device.TryGetFeatureValue(CommonUsages.trigger,out float triggerAmount);
            var calibration = i == 0 ? leftHand : rightHand;
            var rawRay = Pointer(i, calibration, device, out var aimRotation);
            var rayBasis = Quaternion.FromToRotation(aimRotation * Vector3.forward, rawRay.direction) * aimRotation;
            var ray = AlignPointer(i, device, rawRay, rayBasis);
            var pose = new Pose(ray.origin, rayBasis * Quaternion.FromToRotation(Vector3.forward, calibratedForward[i]));
            if (visuals[i])
            {
                visuals[i].transform.localPosition = calibration.glovePosition;
                var basis = calibration.useAimForGlove ? pose.rotation : hands[i].rotation;
                visuals[i].transform.rotation = basis * Quaternion.Euler(calibration.gloveEuler);
                visuals[i].Pose(gripAmount, triggerAmount);
            }
            bool onConsole = game.Workspace.Console.Point(i, ray, trigger && !triggerDown[i], out float panelDistance);
            if(held[i]&&(!held[i].held||held[i].owner!=i))held[i]=null;
            if(grip&&!gripDown[i]&&!held[i]&&game.CanManipulate){
                BonePiece found=null;float nearest=.14f;
                foreach(var bone in game.ActivePieces){
                    if(!bone.gameObject.activeSelf||bone.placed)continue;
                    float distance=bone.DistanceTo(hands[i].position);
                    if(distance<nearest){nearest=distance;found=bone;}
                }
                if(found)CaptureGrip(i,found,false,pose);
                else {var bone=game.Grab(ray,i,out float distance);if(bone)CaptureGrip(i,bone,true,pose);}
            }
            if(held[i]){
                var bone=held[i];
                if(!grip){bone.Drop();held[i]=null;}
                else {
                    if(freeze&&!freezeDown[i])bone.SetMotion(BoneMotionState.Regripping,i);
                    if(!freeze&&bone.Motion==BoneMotionState.Regripping)CaptureGrip(i,bone,false,pose);
                    if(bone.Motion!=BoneMotionState.Regripping){
                        if(device.TryGetFeatureValue(CommonUsages.primary2DAxis,out Vector2 stick)&&Mathf.Abs(stick.y)>.2f){
                            float next=Mathf.Clamp(reach[i]+stick.y*1.2f*Time.deltaTime,holdDistance,game.settings?game.settings.maximumReach:3.5f);
                            offsets[i]+=Vector3.forward*(next-reach[i]);reach[i]=next;
                        }
                        bone.Follow(pose.position + pose.rotation * offsets[i], pose.rotation * rotations[i]);
                    }
                }
            }
            if(!onConsole&&trigger&&!triggerDown[i]){
                if(held[i]){
                    if(held[i].Motion==BoneMotionState.Held){bool ok=game.TryPlace(held[i]);device.SendHapticImpulse(0,ok?.45f:.18f,.15f);if(ok)held[i]=null;}
                    else game.message="Finish pickup or release A / X before submitting.";
                }else game.InteractWithRay(ray);
            }
            if(info&&!infoDown[i])head.GetComponent<BoneLearningDisplay>()?.Toggle();
            rays[i].SetPosition(0,ray.origin);
            rays[i].SetPosition(1,ray.GetPoint(onConsole ? panelDistance : 4));
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
