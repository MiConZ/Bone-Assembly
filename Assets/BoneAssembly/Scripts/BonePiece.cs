using UnityEngine;
namespace BoneAssembly
{
    public enum BoneMotionState { Free, Attracting, Held, Regripping, Placed }
    public enum PlacementResult { Success, WrongBone, WrongRotation, TooFar, Unavailable }
    public class BonePiece : MonoBehaviour
    {
        public string label, region;
        public bool IsHead => label=="Skull and mandible";
        public bool IsHand => region=="HAND";
        public bool IsShoulderBlade => label!=null&&label.EndsWith("scapula");
        [HideInInspector] public bool retired;
        public Vector3 target, home;
        public Quaternion targetRotation=Quaternion.identity, homeRotation=Quaternion.identity;
        public bool placed, held;
        public int owner = -1;
        public GameObject ghost;
        public Renderer[] surfaces;
        public BoneMotionState Motion { get; private set; }
        public Rigidbody Body { get; private set; }
        BoneAssemblyGame game;
        Vector3 goalPosition;
        Quaternion goalRotation;
        float flashUntil;
        public void InitializePhysics(BoneAssemblyGame ownerGame)
        {
            if(Body)return;
            game=ownerGame;
            // The broad root box remains a query trigger; individual source bounds supply contact geometry.
            var root=GetComponent<BoxCollider>();if(root)root.isTrigger=true;
            var material=new PhysicsMaterial("Bone friction"){dynamicFriction=.55f,staticFriction=.65f,bounciness=0};
            foreach(var mesh in GetComponentsInChildren<MeshFilter>(true)){
                var c=mesh.GetComponent<BoxCollider>();if(!c)c=mesh.gameObject.AddComponent<BoxCollider>();
                c.center=mesh.sharedMesh.bounds.center;c.size=Vector3.Max(mesh.sharedMesh.bounds.size,Vector3.one*.012f);c.sharedMaterial=material;
            }
            Body=GetComponent<Rigidbody>();if(!Body)Body=gameObject.AddComponent<Rigidbody>();
            Body.mass=.3f;Body.interpolation=RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            Body.linearDamping=.6f;Body.angularDamping=2;Body.maxAngularVelocity=12;
            Body.solverIterations=12;Body.solverVelocityIterations=4;
            SetMotion(placed?BoneMotionState.Placed:BoneMotionState.Free);
        }
        public void SetMotion(BoneMotionState state,int hand=-1)
        {
            Motion=state;placed=state==BoneMotionState.Placed;
            held=state==BoneMotionState.Held||state==BoneMotionState.Attracting||state==BoneMotionState.Regripping;
            owner=held?hand:-1;
            if(!Body)return;
            if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
            Body.isKinematic=state==BoneMotionState.Placed||state==BoneMotionState.Regripping;
            Body.useGravity=state==BoneMotionState.Free;
            // Completed anatomy should not block fitting an adjoining piece.
            foreach(var c in GetComponentsInChildren<Collider>(true))if(c.gameObject!=gameObject)c.isTrigger=placed;
        }
        public void Follow(Vector3 position,Quaternion rotation){goalPosition=position;goalRotation=rotation;}
        public void Drop(){if(!placed)SetMotion(BoneMotionState.Free);}
        public void Teleport(Vector3 position,Quaternion rotation)
        {
            rotation=Quaternion.Normalize(rotation);
            transform.SetPositionAndRotation(position,rotation);
            if(Body){Body.position=position;Body.rotation=rotation;if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}}
        }
        public void ResetHome(){SetMotion(BoneMotionState.Free);Teleport(home,homeRotation);RestoreColor();}
        public float DistanceTo(Vector3 point)
        {
            float nearest=float.MaxValue;
            foreach(var c in GetComponentsInChildren<Collider>())if(c.enabled&&!c.isTrigger)nearest=Mathf.Min(nearest,Vector3.Distance(c.ClosestPoint(point),point));
            return nearest;
        }
        public void FlashError(){flashUntil=Time.time+.6f;foreach(var r in surfaces)r.sharedMaterial=game.error;}
        public void RestoreColor(){flashUntil=0;if(game)foreach(var r in surfaces)r.sharedMaterial=placed?game.success:game.ivory;}
        void FixedUpdate()
        {
            if(!Body||!game)return;
            if(held&&!game.CanManipulate){Drop();return;}
            if(Motion==BoneMotionState.Held||Motion==BoneMotionState.Attracting){
                float speed=Motion==BoneMotionState.Attracting?(game.settings?game.settings.magnetSpeed:1.5f):4f;
                float gain=game.settings?game.settings.attachmentGain:18f;
                Body.linearVelocity=Vector3.ClampMagnitude((goalPosition-Body.position)*gain,speed);
                Quaternion delta=goalRotation*Quaternion.Inverse(Body.rotation);
                delta.ToAngleAxis(out float degrees,out Vector3 axis);if(degrees>180)degrees-=360;
                Body.angularVelocity=axis.sqrMagnitude>.001f&&Mathf.Abs(degrees)>.01f?Vector3.ClampMagnitude(axis.normalized*degrees*Mathf.Deg2Rad*gain,10):Vector3.zero;
                if(Motion==BoneMotionState.Attracting&&Vector3.Distance(goalPosition,Body.position)<.025f)Motion=BoneMotionState.Held;
            }
            if(Motion==BoneMotionState.Free){var p=transform.position;if(p.y<-.5f||Mathf.Abs(p.x)>4.3f||Mathf.Abs(p.z)>4.3f)ResetHome();}
        }
        void Update(){if(flashUntil>0&&Time.time>=flashUntil)RestoreColor();}
        void OnDisable(){if(held)Drop();}
    }
}
