using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BoneAssembly
{
    [Serializable] public class BoneMeshData { public int id; public Vector3 center, sourceCenter; public Vector3[] vertices; public int[] triangles; }
    [Serializable] public class SkeletonData { public BoneMeshData[] bones; }

    public class BoneAssemblyGame : MonoBehaviour
    {
        public const string Version = "1.0";
        public List<BonePiece> pieces = new List<BonePiece>();
        public string[] stages = { "HAND", "UPPER LIMB", "FOOT", "LOWER LIMB", "FULL BODY" };
        public int stage = 1, mistakes, hints; public bool practice = true, seated, completed, soundEnabled = true;
        public bool startSeated = true;
        public static readonly Vector3 SeatedAnchor = new Vector3(0,0,-.15f);
        public const float SeatedEyeHeight = 2.20f;
        public Vector3 AssemblyViewCenter => bodyOrigin+Vector3.up*(assemblyLift+.03f)-Vector3.forward*.20f;
        [Range(.12f,.4f)] public float headSnapDistance=.25f;
        [Range(.12f,.35f)] public float handSnapDistance=.20f;
        public float elapsed, textScale = 1; public string message = "Select a bone from the instrument trays.";
        public Camera playerCamera; public Transform rig;
        public Material ivory, ghostMaterial, success, error, steel, teal, dark, white, glow;
        TextMesh board, feedback; AudioSource audioSource; float nextPulse, flashUntil;
        readonly Vector3 bodyOrigin = new Vector3(0, 1.02f, .25f);
        BonePiece flashed; System.Random random = new System.Random(35);
        [NonSerialized] public bool verificationMode;
        SkeletonCelebration celebration;
        public bool assignmentMode = true, selectingAssignment = true, lost;
        public int assignmentIndex;
        public float assignmentDuration = 600f;
        Material assignmentGuide;
        GameObject assignmentMenu;
        [HideInInspector] public float assemblyLift;
        const float SurfaceClearance=.008f;
        Renderer[] supportSurfaces;
        public float RemainingTime => Mathf.Max(0, assignmentDuration-elapsed);
        public bool CanManipulate => !selectingAssignment&&!completed&&!lost;
        public string SessionName => assignmentMode ? "ASSIGNMENT "+(assignmentIndex+1) : stages[stage];

        void Awake()
        {
            seated=startSeated;
            if (transform.childCount == 0) BuildRoom();
            else Bind();
            if(assignmentMode)ShowAssignmentMenu();else StartStage(stage);
            RefreshAssignmentControls();
            if(seated)rig.GetComponent<LabPlayer>()?.RecenterSeat();
        }
        public void CalibrateSeatedPose(Vector3 trackedPosition,Quaternion trackedRotation)
        {
            rig.rotation=Quaternion.Euler(0,-trackedRotation.eulerAngles.y,0);
            rig.position=SeatedAnchor+Vector3.up*SeatedEyeHeight-rig.rotation*trackedPosition;
        }
        public void ResetDesktopSeat()
        {
            rig.SetPositionAndRotation(SeatedAnchor,Quaternion.identity);
            playerCamera.transform.localPosition=Vector3.up*SeatedEyeHeight;
            playerCamera.fieldOfView=88;
            playerCamera.transform.LookAt(AssemblyViewCenter);
        }
        void OnEnable(){if(Application.isPlaying&&transform.childCount>0)Bind();}

        public void Bind()
        {
            pieces = GetComponentsInChildren<BonePiece>(true).Where(p=>!p.retired).ToList();
            BoneWorkbenchLayout.Simplify(this);
            BoneWorkbenchLayout.ResizeTrays(this);
            board = transform.Find("StatusBoard")?.GetComponent<TextMesh>();
            feedback = transform.Find("Feedback")?.GetComponent<TextMesh>();
            playerCamera = GetComponentInChildren<Camera>(); rig = playerCamera.transform.parent;
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            RepairSurfacePlacement();
        }
        public void RepairSurfacePlacement()
        {
            supportSurfaces=GetComponentsInChildren<Renderer>(true).Where(r=>r.name=="Sterile drape"||r.name=="Tray sterile lining").ToArray();
            var bed=supportSurfaces.FirstOrDefault(r=>r.name=="Sterile drape");
            if(bed&&pieces.Count>0){
                float bottom=pieces.Where(p=>p.ghost).Min(p=>BoneSurfacePlacement.MeshBounds(p.ghost).min.y);
                float lift=Mathf.Max(0,bed.bounds.max.y+SurfaceClearance-bottom);
                if(lift>.00001f){
                    assemblyLift+=lift;
                    foreach(var p in pieces){p.target+=Vector3.up*lift;if(p.ghost)p.ghost.transform.position+=Vector3.up*lift;if(p.placed)p.transform.position+=Vector3.up*lift;}
                }
            }
            foreach(var p in pieces){
                if(p.home.sqrMagnitude<.01f)continue;
                FitHomeToTray(p);
                if(!p.held&&!p.placed)p.transform.SetPositionAndRotation(p.home,p.homeRotation);
            }
        }
        void FitHomeToTray(BonePiece p)
        {
            var tray=supportSurfaces?.Where(r=>r.name=="Tray sterile lining").OrderBy(r=>Mathf.Abs(r.bounds.center.x-p.home.x)).FirstOrDefault();
            if(!tray)return;
            var position=p.transform.position;var rotation=p.transform.rotation;
            p.transform.SetPositionAndRotation(p.home,p.homeRotation);
            var bounds=BoneSurfacePlacement.MeshBounds(p.gameObject);
            p.transform.position+=Vector3.up*(tray.bounds.max.y+SurfaceClearance-bounds.min.y);
            ConstrainHeldPiece(p);p.home=p.transform.position;
            p.transform.SetPositionAndRotation(position,rotation);
        }
        public void ConstrainHeldPiece(BonePiece p)
        {
            if(supportSurfaces==null)return;
            var bounds=BoneSurfacePlacement.MeshBounds(p.gameObject);float lift=0;
            foreach(var surface in supportSurfaces)if(surface&&BoneSurfacePlacement.OverlapsTop(bounds,surface.bounds))lift=Mathf.Max(lift,surface.bounds.max.y+SurfaceClearance-bounds.min.y);
            if(lift>0)p.transform.position+=Vector3.up*lift;
        }

        public Material Mat(string name, Color color, float metallic = 0, bool emission = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader) { name = name, color = color };
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", metallic > 0 ? .68f : .32f);
            if (emission) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.5f); }
            return m;
        }
        public GameObject Shape(string name, PrimitiveType type, Vector3 p, Vector3 size, Material mat, bool collide = true, Transform parent = null)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name; g.transform.SetParent(parent ?? transform, false);
            g.transform.localPosition = p; g.transform.localScale = size; g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collide) { var c = g.GetComponent<Collider>(); if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
            return g;
        }
        public GameObject Box(string n, Vector3 p, Vector3 s, Material m, bool collision = true) => Shape(n, PrimitiveType.Cube, p, s, m, collision);
        public TextMesh Label(string n, string text, Vector3 pos, float size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var g = new GameObject(n); g.transform.SetParent(transform, false); g.transform.localPosition = pos;
            var t = g.AddComponent<TextMesh>(); t.text = text; t.fontSize = 60; t.characterSize = size*.55f; t.anchor = anchor; t.alignment = TextAlignment.Center; t.color = color;
            return t;
        }
        void Button(string label, string action, Vector3 p, float width = .38f)
        {
            var g = Box("Button " + action, p, new Vector3(width, .105f, .035f), teal);
            g.AddComponent<LabAction>().command = action;
            Label("Label " + action, label, p + Vector3.back * .025f, .015f, Color.white);
        }
        public void BuildRoom()
        {
            ivory = Mat("Bone | warm ivory", new Color(.88f,.82f,.66f));
            ghostMaterial = Mat("Target | blueprint", new Color(.07f,.29f,.34f));
            success = Mat("Correct | mint", new Color(.2f,.92f,.63f), 0, true);
            error = Mat("Incorrect | coral", new Color(1,.18f,.15f),0,true);
            steel = Mat("Brushed stainless steel", new Color(.43f,.53f,.57f), .82f);
            teal = Mat("Surgical teal", new Color(.045f,.27f,.29f)); dark = Mat("Graphite", new Color(.025f,.05f,.07f));
            white = Mat("Ceramic", new Color(.69f,.8f,.8f)); glow = Mat("Surgical light", new Color(.68f,.94f,1),0,true);
            var wall = Mat("Wall panels",new Color(.24f,.39f,.41f)); var floor = Mat("Resin floor",new Color(.10f,.19f,.21f),.15f);
            Box("Floor",new Vector3(0,-.08f,0),new Vector3(8,.16f,8),floor);
            Box("Back wall",new Vector3(0,1.7f,3.8f),new Vector3(8,3.4f,.15f),wall);
            Box("Left wall",new Vector3(-3.9f,1.7f,0),new Vector3(.15f,3.4f,8),wall);
            Box("Right wall",new Vector3(3.9f,1.7f,0),new Vector3(.15f,3.4f,8),wall);
            Box("Front wall",new Vector3(0,1.7f,-3.9f),new Vector3(8,3.4f,.15f),wall);
            Box("Ceiling",new Vector3(0,3.45f,0),new Vector3(8,.1f,8),white);
            for(int i=-3;i<=3;i++) {
                Box("Floor joint",new Vector3(i,-.001f,0),new Vector3(.009f,.004f,8),steel,false);
                Box("Floor joint",new Vector3(0,-.001f,i),new Vector3(8,.004f,.009f),steel,false);
                Box("Wall seam",new Vector3(i,1.7f,3.71f),new Vector3(.015f,3.3f,.015f),steel,false);
            }
            for(int x=-1;x<=1;x+=2) {
                Box("Ceiling light",new Vector3(x*1.8f,3.36f,0),new Vector3(.7f,.03f,2.5f),glow,false);
                var l = new GameObject("Ceiling illumination").AddComponent<Light>(); l.transform.SetParent(transform); l.transform.position=new Vector3(x*1.8f,2.9f,0); l.type=LightType.Point; l.range=7; l.intensity=2.2f; l.color=new Color(.78f,.91f,1);
            }
            var sun = new GameObject("Soft room illumination").AddComponent<Light>(); sun.transform.SetParent(transform); sun.type=LightType.Directional; sun.intensity=.75f; sun.transform.rotation=Quaternion.Euler(55,-25,0); sun.shadows=LightShadows.Soft;
            RenderSettings.ambientLight = new Color(.25f,.34f,.37f); RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            Box("Table pedestal",new Vector3(0,.4f,.25f),new Vector3(.5f,.75f,.8f),steel);
            Box("Table base",new Vector3(0,.12f,.25f),new Vector3(.85f,.12f,1.3f),dark);
            Box("Operating table",new Vector3(0,.9f,.25f),new Vector3(1.32f,.16f,2.25f),steel);
            Box("Sterile drape",new Vector3(0,.992f,.25f),new Vector3(1.27f,.026f,2.18f),teal);
            for(int s=-1;s<=1;s+=2) {
                Box("Table rail",new Vector3(s*.7f,.88f,.25f),new Vector3(.035f,.06f,1.8f),steel);
                Box("Instrument trolley",new Vector3(s*1.12f,.87f,-.02f),new Vector3(.62f,.08f,1.55f),steel);
                Box("Tray sterile lining",new Vector3(s*1.12f,.917f,-.02f),new Vector3(.56f,.015f,1.48f),teal);
                for(int z=-1;z<=1;z+=2) {
                    Box("Trolley leg",new Vector3(s*1.12f,.44f,z*.57f),new Vector3(.045f,.8f,.045f),steel);
                    Shape("Caster",PrimitiveType.Sphere,new Vector3(s*1.12f,.06f,z*.57f),Vector3.one*.1f,dark);
                }
            }
            // Suspended surgical lamp, articulated support and visible lamp lenses.
            Box("Lamp ceiling mount",new Vector3(.55f,3.3f,.7f),new Vector3(.18f,.2f,.18f),steel);
            Box("Lamp arm",new Vector3(.28f,2.95f,.7f),new Vector3(.65f,.08f,.08f),white);
            Box("Lamp stem",new Vector3(0,2.72f,.7f),new Vector3(.07f,.48f,.07f),steel);
            Shape("Operating lamp housing",PrimitiveType.Cylinder,new Vector3(0,2.47f,.7f),new Vector3(.72f,.055f,.72f),white);
            for(int i=0;i<7;i++) { float a=i*Mathf.PI*2/7; Shape("Lamp lens",PrimitiveType.Cylinder,new Vector3(Mathf.Cos(a)*.23f,2.405f,.7f+Mathf.Sin(a)*.23f),new Vector3(.13f,.01f,.13f),glow,false); }
            var spot = new GameObject("Surgical spotlight").AddComponent<Light>(); spot.transform.SetParent(transform); spot.type=LightType.Spot; spot.transform.position=new Vector3(0,2.38f,.7f); spot.transform.rotation=Quaternion.Euler(90,0,0); spot.range=4; spot.spotAngle=85; spot.intensity=4; spot.shadows=LightShadows.Soft;
            // Cabinets, supply drawers, doorway, anaesthesia cart and monitor.
            for(int i=0;i<4;i++) { float x=-2.7f+i*.65f; Box("Supply cabinet",new Vector3(x,.58f,3.2f),new Vector3(.61f,1.12f,.58f),white); for(int j=0;j<3;j++){Box("Drawer",new Vector3(x,.3f+j*.3f,2.897f),new Vector3(.56f,.26f,.025f),wall); Box("Drawer handle",new Vector3(x,.34f+j*.3f,2.87f),new Vector3(.2f,.025f,.025f),steel);} }
            Box("Operating room door",new Vector3(2.45f,1.25f,3.68f),new Vector3(1.25f,2.5f,.05f),teal);
            Box("Door observation window",new Vector3(2.45f,1.7f,3.635f),new Vector3(.75f,.5f,.03f),dark);
            Label("Theatre identifier","THEATRE  04",new Vector3(2.45f,2.7f,3.6f),.03f,Color.white);
            Box("Monitor pedestal",new Vector3(-1.95f,.7f,1.7f),new Vector3(.1f,1.4f,.1f),steel);
            Box("Monitor",new Vector3(-1.95f,1.55f,1.7f),new Vector3(.8f,.55f,.1f),dark);
            Label("Vitals","SIMULATION\n\n72  bpm      98 %\nSTABLE  /  TRAINING",new Vector3(-1.95f,1.55f,1.635f),.022f,new Color(.3f,1,.68f));
            var ecg=new GameObject("ECG waveform").AddComponent<LineRenderer>(); ecg.transform.SetParent(transform); ecg.sharedMaterial=glow; ecg.startWidth=ecg.endWidth=.008f; ecg.positionCount=35;
            for(int i=0;i<35;i++){float y=(i%12==5?.07f:i%12==6?-.1f:i%12==7?.15f:0);ecg.SetPosition(i,new Vector3(-2.28f+i*.019f,1.53f+y,1.625f));}
            Box("Session console",new Vector3(0,1.88f,2.3f),new Vector3(2.05f,1.23f,.09f),dark);
            Label("Title","BONE ASSEMBLY",new Vector3(0,2.36f,2.24f),.052f,Color.white);
            Label("Subtitle","VIRTUAL ANATOMY  /  OPERATING THEATRE",new Vector3(0,2.18f,2.24f),.016f,new Color(.36f,.85f,.81f));
            board=Label("StatusBoard","",new Vector3(0,1.96f,2.24f),.023f,Color.white);
            for(int i=0;i<5;i++) Button(stages[i],"stage:"+i,new Vector3(-.8f+i*.4f,1.66f,2.22f),.37f);
            Button("PRACTICE / EXAM","mode",new Vector3(-.6f,1.48f,2.22f),.55f);
            Button("HINT","hint",new Vector3(-.12f,1.48f,2.22f),.3f);
            Button("RESET","reset",new Vector3(.23f,1.48f,2.22f),.3f);
            Button("NEXT","next",new Vector3(.61f,1.48f,2.22f),.35f);
            Button("SEATED / STAND","seated",new Vector3(-.6f,1.12f,2.22f),.55f);
            Button("TEXT SIZE","text",new Vector3(0,1.12f,2.22f),.5f);
            Button("SOUND","sound",new Vector3(.6f,1.12f,2.22f),.5f);
            feedback=Label("Feedback",message,new Vector3(0,1.28f,1.9f),.021f,new Color(.42f,.95f,.81f));
            BuildSkeleton();
            var origin=new GameObject("Player Rig"); origin.transform.SetParent(transform); origin.transform.position=new Vector3(.95f,0,-1.0f);
            var cameraGO=new GameObject("Main Camera"); cameraGO.tag="MainCamera"; cameraGO.transform.SetParent(origin.transform,false);
            playerCamera=cameraGO.AddComponent<Camera>(); playerCamera.nearClipPlane=.03f; playerCamera.farClipPlane=30; playerCamera.fieldOfView=68;
            cameraGO.transform.localPosition=new Vector3(0,1.65f,0); cameraGO.transform.rotation=Quaternion.LookRotation(new Vector3(0,1.25f,.4f)-cameraGO.transform.position);
            cameraGO.AddComponent<AudioListener>(); rig=origin.transform;
            var input=origin.AddComponent<LabPlayer>(); input.game=this; input.head=playerCamera;
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend=0;
        }

        void BuildSkeleton()
        {
            var data=JsonUtility.FromJson<SkeletonData>(Resources.Load<TextAsset>("SkeletonData").text);
            // Group source meshes into anatomical teaching units; individual mesh identities remain retained.
            var groups=new Dictionary<string,List<BoneMeshData>>();
            foreach(var b in data.bones) { string key=Classify(b); if(!groups.ContainsKey(key))groups[key]=new List<BoneMeshData>();groups[key].Add(b); }
            foreach(var kv in groups) {
                var parts=kv.Key.Split('|'); var g=new GameObject(parts[1]);g.transform.SetParent(transform,false);
                var p=g.AddComponent<BonePiece>();p.region=parts[0];p.label=parts[1];
                Vector3 center=Vector3.zero;foreach(var b in kv.Value)center+=b.center;center/=kv.Value.Count;
                p.target=bodyOrigin+center;p.targetRotation=Quaternion.identity;g.transform.position=p.target;
                foreach(var b in kv.Value) {
                    var child=new GameObject("Source bone "+b.id);child.transform.SetParent(g.transform,false);child.transform.localPosition=b.center-center;
                    var mesh=new Mesh{name="Skeleton source "+b.id};mesh.vertices=b.vertices;mesh.triangles=b.triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
                    child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=ivory;
                }
                p.surfaces=g.GetComponentsInChildren<Renderer>(); var bounds=p.surfaces[0].bounds;foreach(var r in p.surfaces)bounds.Encapsulate(r.bounds);
                var collider=g.AddComponent<BoxCollider>();collider.center=g.transform.InverseTransformPoint(bounds.center);collider.size=Vector3.Max(bounds.size,Vector3.one*(p.IsHand?.14f:.035f));
                p.ghost=Instantiate(g,g.transform.parent);p.ghost.name="Target | "+p.label;
                if(Application.isPlaying) {p.ghost.GetComponent<BonePiece>().retired=true;Destroy(p.ghost.GetComponent<BonePiece>());Destroy(p.ghost.GetComponent<Collider>());} else {DestroyImmediate(p.ghost.GetComponent<BonePiece>());DestroyImmediate(p.ghost.GetComponent<Collider>());}
                foreach(var r in p.ghost.GetComponentsInChildren<Renderer>())r.sharedMaterial=ghostMaterial;
                pieces.Add(p);
            }
        }
        static string Classify(BoneMeshData b)
        {
            string side=b.sourceCenter.x<0?"Right ":"Left "; float x=Mathf.Abs(b.sourceCenter.x),z=b.sourceCenter.z;
            switch(b.id) {
                case 54:case 220:return "LOWER LIMB|"+side+"femur";
                case 55:case 56:case 57:case 221:case 222:case 223:return "LOWER LIMB|"+side+"lower leg (tibia + fibula + patella)";
                case 58:case 225:return "UPPER LIMB|"+side+"scapula";
                case 61:case 228:return "UPPER LIMB|"+side+"humerus";
                case 224:case 245:return "UPPER LIMB|"+side+"clavicle";
                case 59:case 60:case 226:case 227:return "UPPER LIMB|"+side+"forearm (radius + ulna)";
                case 124:case 229:return "AXIAL|Skull and mandible";
                case 62:case 63:case 66:return "AXIAL|Pelvic girdle";
            }
            if(z<1.5f)return "FOOT|"+side+(z<.5f?"forefoot":"hindfoot and midfoot");
            if(x>3.3f&&z<12.8f&&z>8)return "HAND|"+side+"hand";
            if(z>15.5f)return "AXIAL|Skull and mandible";
            if(x>.4f&&z>11.2f)return "AXIAL|Thoracic cage";
            return "AXIAL|Vertebral column";
        }
        public IEnumerable<BonePiece> ActivePieces => pieces.Where(p=>assignmentMode ? AssignmentContains(p.region,assignmentIndex) : stages[stage]=="FULL BODY"||p.region==stages[stage]);
        public static bool AssignmentContains(string region,int index)
        {
            return index==3 || (index==0&&region=="AXIAL") || (index==1&&(region=="UPPER LIMB"||region=="HAND")) || (index==2&&(region=="LOWER LIMB"||region=="FOOT"));
        }
        void RefreshAssignmentControls()
        {
            if(!assignmentMenu){var existing=transform.Find("Assignment selection");if(existing)assignmentMenu=existing.gameObject;}
            if(assignmentMenu==null){
                assignmentMenu=new GameObject("Assignment selection");assignmentMenu.transform.SetParent(transform,false);
                var red=Mat("Assignment menu coral",new Color(1,.24f,.27f));
                for(int i=0;i<4;i++){
                    var p=new Vector3(0,2.14f-i*.22f,2.12f);
                    var button=Box("Choose assignment "+i,p,new Vector3(1.45f,.18f,.035f),red);button.transform.SetParent(assignmentMenu.transform,true);button.AddComponent<LabAction>().command="assignment:"+i;
                    var text=Label("Assignment choice "+i,"ASSIGNMENT "+(i+1),p+Vector3.back*.025f,.03f,Color.white);text.transform.SetParent(assignmentMenu.transform,true);
                }
            }
            if(!assignmentMenu.transform.Find("Assignment test mode")){
                var p=new Vector3(0,1.26f,2.12f);var test=Box("Assignment test mode",p,new Vector3(.7f,.11f,.035f),teal);test.transform.SetParent(assignmentMenu.transform,true);test.AddComponent<LabAction>().command="test";
                var label=Label("Assignment test label","TEST MODE",p+Vector3.back*.025f,.018f,Color.white);label.transform.SetParent(assignmentMenu.transform,true);
            }
            assignmentMenu.SetActive(selectingAssignment);
            if(board)board.gameObject.SetActive(!selectingAssignment);
            if(feedback)feedback.gameObject.SetActive(!selectingAssignment);
            var subtitle=transform.Find("Subtitle");if(subtitle)subtitle.gameObject.SetActive(!selectingAssignment);
            if(subtitle&&seated)subtitle.GetComponent<TextMesh>().text="SEATED / GRIP: PICK UP / TRIGGER: AIM AND PLACE";
            foreach(string command in new[]{"mode","hint","reset","next"}){
                var button=transform.Find("Button "+command);var label=transform.Find("Label "+command);
                if(button)button.gameObject.SetActive(!selectingAssignment);if(label)label.gameObject.SetActive(!selectingAssignment);
            }
            for(int i=0;i<5;i++){
                var button=transform.Find("Button stage:"+i);var label=transform.Find("Label stage:"+i);
                if(button)button.gameObject.SetActive(!selectingAssignment);if(label)label.gameObject.SetActive(!selectingAssignment);
                if(button)button.GetComponent<LabAction>().command=assignmentMode?(i<4?"assignment:"+i:"test"):"stage:"+i;
                if(label)label.GetComponent<TextMesh>().text=assignmentMode?(i<4?"ASSIGNMENT "+(i+1):"TEST MODE"):stages[i];
            }
            var mode=transform.Find("Label mode");if(mode)mode.GetComponent<TextMesh>().text="ASSIGNMENT / TEST";
            var seat=transform.Find("Label seated");if(seat)seat.GetComponent<TextMesh>().text="RECENTER SEAT";
            var hint=transform.Find("Label hint");if(hint){hint.GetComponent<TextMesh>().text=completed?"REVIEW":"HINT";}
        }
        public void BeginAssignment(int index)
        {
            assignmentMode=true;assignmentIndex=Mathf.Clamp(index,0,3);practice=false;
            StartStage(4);RefreshAssignmentControls();
        }
        void ShowAssignmentMenu()
        {
            StartStage(4);selectingAssignment=true;
            foreach(var p in pieces){p.gameObject.SetActive(false);p.ghost.SetActive(false);}
            message="SELECT ASSIGNMENT 1 - 4";
            RefreshAssignmentControls();
        }
        public void StartStage(int index)
        {
            playerCamera?.GetComponent<BoneLearningDisplay>()?.Clear();
            RepairSurfacePlacement();
            if(celebration){celebration.gameObject.SetActive(false);Destroy(celebration.gameObject);celebration=null;}
            foreach(var p in pieces) {p.held=false;p.owner=-1;p.placed=false;}
            stage=Mathf.Clamp(index,0,4);mistakes=hints=0;elapsed=0;completed=false;lost=false;selectingAssignment=false;
            flashed=null;
            if(assignmentGuide==null){
                assignmentGuide=new Material(ghostMaterial){name="Assignment fading guide"};
                assignmentGuide.SetFloat("_Surface",1);assignmentGuide.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);assignmentGuide.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);assignmentGuide.SetFloat("_ZWrite",0);assignmentGuide.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");assignmentGuide.SetOverrideTag("RenderType","Transparent");assignmentGuide.renderQueue=3000;
            }
            var active=ActivePieces.OrderBy(_=>random.Next()).ToList();
            foreach(var p in pieces) {
                bool enabled=active.Contains(p);p.gameObject.SetActive(enabled);p.ghost.SetActive(assignmentMode?enabled:!enabled||practice);
                foreach(var r in p.ghost.GetComponentsInChildren<Renderer>())r.sharedMaterial=assignmentMode?assignmentGuide:ghostMaterial;
                foreach(var r in p.surfaces)r.sharedMaterial=ivory;
            }
            BoneWorkbenchLayout.ResizeTrays(this);
            BoneWorkbenchLayout.Arrange(active);
            foreach(var p in active) {
                p.transform.SetPositionAndRotation(p.home,p.homeRotation);
                FitHomeToTray(p);p.transform.SetPositionAndRotation(p.home,p.homeRotation);
                p.gameObject.SetActive(true);
            }
            message=assignmentMode?"10 MINUTES / Remember the blinking guide before it fades.":practice?"PRACTICE: match each bone to its blueprint.":"TEST: no blueprints. Match position AND orientation.";
            AdvanceSession(0);
            UpdateBoard();
            if(Application.isPlaying)RefreshAssignmentControls();
        }
        public BonePiece Grab(Ray ray,int owner,out float distance)
        {
            distance=0;var hits=Physics.RaycastAll(ray,4).OrderBy(h=>h.distance);
            foreach(var hit in hits) {
                var action=hit.collider.GetComponent<LabAction>();if(action){Command(action.command);return null;}
                var p=hit.collider.GetComponentInParent<BonePiece>();
                if(p&&p.placed){ShowBoneInformation(p);return null;}
                if(!CanManipulate)return null;
                if(p!=null&&!p.held&&!p.placed){p.held=true;p.owner=owner;distance=hit.distance;message=p.label+(p.IsHead||p.IsShoulderBlade?" / Aim near the matching outline; rotation snaps automatically.":" | Move and rotate, then release.");ShowBoneInformation(p);return p;}
                if(!hit.collider.isTrigger)break;
            }
            return null;
        }
        public bool TryPlace(BonePiece p)
        {
            if(p==null||p.placed||!CanManipulate)return false;
            if(RemainingTime<=0){FinishLoss();return false;}
            float snapDistance=p.IsHead?headSnapDistance:p.IsShoulderBlade?.28f:p.IsHand?handSnapDistance:practice?.24f:.18f;
            bool close=Vector3.Distance(BoneSurfacePlacement.MeshBounds(p.gameObject).center,BoneSurfacePlacement.MeshBounds(p.ghost).center)<=snapDistance;
            bool aligned=p.IsHead||p.IsShoulderBlade||Quaternion.Angle(p.transform.rotation,p.targetRotation)<(p.IsHand?60:practice?55:40);
            p.held=false;p.owner=-1;
            if(close&&aligned){
                p.placed=true;p.transform.SetPositionAndRotation(p.target,p.targetRotation);p.ghost.SetActive(false);
                foreach(var r in p.surfaces)r.sharedMaterial=success;
                message="CORRECT  /  "+p.label;Tone(880,.12f);RevealTray();
                if(ActivePieces.All(a=>a.placed)){completed=true;message="END GAME / WIN! Select REVIEW to inspect, or NEXT to continue.";SaveResult();if(!verificationMode)Celebrate();RefreshAssignmentControls();}
                UpdateBoard();
                return true;
            }
            mistakes++;flashed=p;flashUntil=Time.time+.6f;
            foreach(var r in p.surfaces)r.sharedMaterial=error;
            message=close?"ROTATE: orientation does not match. Returned to tray.":"TRY AGAIN: position does not match. Returned to tray.";
            p.transform.SetPositionAndRotation(p.home,p.homeRotation);Tone(190,.16f);return false;
        }
        void RevealTray()
        {
            foreach(var p in ActivePieces.Where(p=>!p.placed))p.gameObject.SetActive(true);
        }
        public void ShowBoneInformation(BonePiece piece)
        {
            var display=playerCamera.GetComponent<BoneLearningDisplay>()??playerCamera.gameObject.AddComponent<BoneLearningDisplay>();
            display.Show(piece);
        }
        public bool InteractWithRay(Ray pointer)
        {
            if(!Physics.Raycast(pointer,out var hit,4))return false;
            var action=hit.collider.GetComponent<LabAction>();if(action){Command(action.command);return true;}
            var piece=hit.collider.GetComponentInParent<BonePiece>();
            if(piece&&piece.placed){ShowBoneInformation(piece);return true;}
            return false;
        }
        public bool TryPlaceFromRay(BonePiece piece,Ray pointer)
        {
            if(!piece||!piece.held||!CanManipulate)return false;
            float height=BoneSurfacePlacement.MeshBounds(piece.ghost).center.y;
            var plane=new Plane(Vector3.up,new Vector3(0,height,0));
            if(!plane.Raycast(pointer,out float distance)||distance<.05f||distance>4){message="Point at the operating table, then press Trigger to place.";return false;}
            Vector3 offset=BoneSurfacePlacement.MeshBounds(piece.gameObject).center-piece.transform.position;
            piece.transform.position=pointer.GetPoint(distance)-offset;
            return TryPlace(piece);
        }
        void Celebrate()
        {
            var dancer=new GameObject("Skeleton | Celebration dance");dancer.transform.SetParent(transform,false);
            celebration=dancer.AddComponent<SkeletonCelebration>();celebration.Initialize(success,bodyOrigin+Vector3.up*assemblyLift);
            foreach(var piece in pieces){piece.held=false;piece.owner=-1;piece.gameObject.SetActive(false);piece.ghost.SetActive(false);}
        }
        public void ReviewPlacedBones()
        {
            if(!completed)return;
            if(celebration){celebration.gameObject.SetActive(false);Destroy(celebration.gameObject);celebration=null;}
            foreach(var p in pieces){bool active=ActivePieces.Contains(p);p.gameObject.SetActive(active&&p.placed);p.ghost.SetActive(!active);p.held=false;p.owner=-1;}
            message="REVIEW / Click a placed bone, or aim and press Trigger, to read its information.";
            UpdateBoard();
        }
        void SaveResult()
        {
            if(verificationMode)return;
            string key="BoneAssembly."+(assignmentMode?"assignment."+assignmentIndex:stage+(practice?".practice":".exam"));float previous=PlayerPrefs.GetFloat(key,float.MaxValue);
            if(elapsed<previous)PlayerPrefs.SetFloat(key,elapsed);PlayerPrefs.Save();
        }
        public void Command(string command)
        {
            if(command.StartsWith("assignment:")){BeginAssignment(int.Parse(command.Substring(11)));return;}
            if(command=="test"){assignmentMode=false;practice=false;StartStage(1);RefreshAssignmentControls();return;}
            if(command=="review"){ReviewPlacedBones();return;}
            if(command.StartsWith("stage:")){StartStage(int.Parse(command.Substring(6)));return;}
            switch(command){
                case "reset":StartStage(stage);break;
                case "next":if(completed){if(assignmentMode)BeginAssignment((assignmentIndex+1)%4);else StartStage((stage+1)%5);}else message="Complete this stage before continuing.";break;
                case "mode":assignmentMode=!assignmentMode;practice=false;if(assignmentMode)ShowAssignmentMenu();else StartStage(1);RefreshAssignmentControls();break;
                case "hint":if(completed){ReviewPlacedBones();break;}if(!practice){message="Hints are disabled in exam mode.";break;}var p=ActivePieces.FirstOrDefault(a=>!a.placed&&a.gameObject.activeSelf);if(p!=null){hints++;foreach(var r in p.ghost.GetComponentsInChildren<Renderer>())r.sharedMaterial=success;message="HINT  /  "+p.label+" : match the lit blueprint.";}break;
                case "seated":seated=true;rig.GetComponent<LabPlayer>()?.RecenterSeat();message="SEATED / View recentered. Grip: pick up. Trigger: aim and place.";break;
                case "text":float oldScale=textScale;textScale=textScale<1.2f?1.35f:1;foreach(var label in GetComponentsInChildren<TextMesh>())label.characterSize*=textScale/oldScale;break;
                case "sound":soundEnabled=!soundEnabled;break;
            }
            UpdateBoard();
        }
        void Update()
        {
            AdvanceSession(Time.deltaTime);
            if(flashed&&Time.time>flashUntil){foreach(var r in flashed.surfaces)r.sharedMaterial=ivory;flashed=null;}
            if(Time.time>nextPulse){nextPulse=Time.time+2.3f;Tone(650,.035f,.035f);}
            UpdateBoard();
        }
        public void AdvanceSession(float delta)
        {
            if(!CanManipulate)return;
            elapsed=Mathf.Min(assignmentDuration,elapsed+Mathf.Max(0,delta));
            if(assignmentMode&&assignmentGuide){
                float alpha=elapsed<3f?(.25f+.65f*(.5f+.5f*Mathf.Sin(elapsed*Mathf.PI*4))):Mathf.Lerp(.55f,.035f,Mathf.Clamp01((elapsed-3)/2));
                assignmentGuide.color=new Color(.35f,.8f,.85f,alpha);
            }
            if(RemainingTime<=0)FinishLoss();
        }
        void FinishLoss()
        {
            lost=true;message="END GAME / LOSE / Time is up. Select RESET to try again.";
            foreach(var p in ActivePieces){p.held=false;p.owner=-1;if(p.placed)continue;p.ghost.SetActive(true);foreach(var r in p.ghost.GetComponentsInChildren<Renderer>())r.sharedMaterial=error;}
            UpdateBoard();
        }
        void UpdateBoard()
        {
            int count=ActivePieces.Count(),correct=ActivePieces.Count(p=>p.placed);float acc=correct+mistakes==0?100:100f*correct/(correct+mistakes);
            if(board)board.text=selectingAssignment?"SELECT ASSIGNMENT 1 - 4\n10 MINUTES / ASSEMBLE BEFORE TIME RUNS OUT":$"{SessionName} / {(assignmentMode?"ASSIGNMENT MODE":"TEST MODE")}\n{correct:00} / {count:00} ASSEMBLED     {Mathf.FloorToInt(RemainingTime/60):00}:{Mathf.FloorToInt(RemainingTime%60):00}     {acc:0}% ACCURACY";
            if(board){if(completed||lost)board.text="END GAME / "+(completed?"WIN":"LOSE")+"\n"+board.text;board.color=lost?new Color(1,.35f,.3f):completed?new Color(.3f,1,.65f):Color.white;}
            if(feedback){feedback.text=message;feedback.characterSize=.01155f*textScale;}
        }
        void Tone(float frequency,float duration,float volume=.15f)
        {
            if(!soundEnabled||!Application.isPlaying||!audioSource)return;int length=(int)(22050*duration);var samples=new float[length];
            for(int i=0;i<length;i++)samples[i]=Mathf.Sin(2*Mathf.PI*frequency*i/22050)*Mathf.Sin(Mathf.PI*i/length)*volume;
            var clip=AudioClip.Create("Monitor tone",length,1,22050,false);clip.SetData(samples,0);audioSource.PlayOneShot(clip);Destroy(clip,duration+1);
        }
        void OnGUI()
        {
            if(UnityEngine.XR.XRSettings.isDeviceActive)return;
            float scale=Mathf.Max(.75f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
            var style=new GUIStyle(GUI.skin.box){fontSize=15,alignment=TextAnchor.UpperLeft,padding=new RectOffset(20,20,16,16)};
            GUI.Box(new Rect(20,20,280,400),"B O N E  A S S E M B L Y\nOPERATING THEATRE  /  04",style);
            GUI.Label(new Rect(40,85,240,30),"SELECT ANATOMY MODULE");
            for(int i=0;i<(assignmentMode?4:5);i++)if(GUI.Button(new Rect(40,120+i*38,240,32),assignmentMode?"ASSIGNMENT "+(i+1):stages[i]))Command(assignmentMode?"assignment:"+i:"stage:"+i);
            if(GUI.Button(new Rect(40,320,115,32),assignmentMode?"TEST MODE":"ASSIGNMENTS"))Command("mode");
            if(GUI.Button(new Rect(165,320,115,32),completed?"REVIEW":"HINT [H]"))Command("hint");
            if(GUI.Button(new Rect(40,362,115,32),"RESET [R]"))Command("reset");
            if(GUI.Button(new Rect(165,362,115,32),"NEXT"))Command("next");
            GUI.Box(new Rect(20,430,280,170),"",style);GUI.Label(new Rect(40,445,245,140),"SEATED / NO WALKING REQUIRED\nDesktop: click pick up / release\nWheel: depth   Q / E: rotate\nRight mouse: look   F: orient\nVR: Grip picks up near your hand\nAim + Trigger places on the table\nRecenter seat: adjust view and height");
            if(GUI.Button(new Rect(20,610,135,32),"Recenter seat"))Command("seated");
            if(GUI.Button(new Rect(165,610,135,32),"Text size"))Command("text");
            var status=new GUIStyle(GUI.skin.box){fontSize=(int)(18*textScale),alignment=TextAnchor.MiddleCenter,wordWrap=true,padding=new RectOffset(18,18,10,10)};
            float w=Screen.width/scale,h=Screen.height/scale;
            GUI.Box(new Rect(320,h-100,w-340,75),message,status);
            GUI.Box(new Rect(w-280,20,260,100),$"{SessionName}\n{Mathf.FloorToInt(RemainingTime/60):00}:{Mathf.FloorToInt(RemainingTime%60):00} MIN\n{ActivePieces.Count(p=>p.placed)} / {ActivePieces.Count()} ASSEMBLED",status);
            if(selectingAssignment){
                GUI.Box(new Rect(w/2-200,h/2-225,400,420),"",status);
                GUI.Label(new Rect(w/2-190,h/2-215,380,40),"ASSIGNMENT MODE / 10 MINUTES",status);
                Color old=GUI.backgroundColor;GUI.backgroundColor=new Color(1,.24f,.27f);
                for(int i=0;i<4;i++)if(GUI.Button(new Rect(w/2-175,h/2-155+i*82,350,65),"ASSIGNMENT "+(i+1)))BeginAssignment(i);
                GUI.backgroundColor=old;
            }
            if(completed||lost)GUI.Box(new Rect(w/2-200,h-260,400,145),$"END GAME / {(completed?"WIN":"LOSE")}\nTime remaining {Mathf.FloorToInt(RemainingTime/60):00}:{Mathf.FloorToInt(RemainingTime%60):00}\n{(completed?"REVIEW: inspect bones / NEXT: continue":"Select RESET to retry")}",status);
        }
    }
}
