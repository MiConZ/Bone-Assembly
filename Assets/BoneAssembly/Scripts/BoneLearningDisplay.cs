using UnityEngine;

namespace BoneAssembly
{
    public class BoneLearningDisplay : MonoBehaviour
    {
        [Header("Editable scene references")] public GameObject panel; public TextMesh title,english,body;
        public BoneGameSettings settings;
        BonePiece selected;float shown;
        Font learningFont;
        public const float DisplayDuration=4f;
        public bool IsVisible => panel&&panel.activeSelf;
        public string CurrentExplanation { get;private set; }
        public void Show(BonePiece piece)
        {
            if(!panel)CreatePanel();
            if(!learningFont&&title)learningFont=title.font;
            selected=piece;shown=Time.time;var card=settings?settings.Information(piece):BoneEducation.Get(piece);
            title.text=BoneEducation.Wrap(card.ThaiName,30);
            string englishName=card.EnglishName;int details=englishName.IndexOf(" (");if(details>=0)englishName=englishName.Substring(0,details);
            english.text=BoneEducation.Wrap(englishName,36);
            float englishY=.27f-.065f*(title.text.Split('\n').Length);
            english.transform.localPosition=new Vector3(-.38f,englishY,-.012f);
            body.transform.localPosition=new Vector3(-.38f,englishY-.07f,-.012f);
            CurrentExplanation=card.Explanation;
            body.text=BoneEducation.Wrap(card.Explanation,34)+"\n\nOpenStax Anatomy & Physiology";
            if(learningFont){learningFont.RequestCharactersInTexture(title.text,72);learningFont.RequestCharactersInTexture(english.text+body.text,64);SyncFontTexture();}
            panel.SetActive(true);
        }
        public void CreatePanel()
        {
            var existing=transform.Find("Bone learning card");if(existing)Destroy(existing.gameObject);
            panel=new GameObject("Bone learning card");panel.transform.SetParent(transform,false);
            panel.transform.localPosition=settings?settings.cardPosition:new Vector3(.82f,-.03f,1.3f);panel.transform.localScale=Vector3.one*(settings?settings.cardScale:1.25f);
            var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name="Information background";backing.transform.SetParent(panel.transform,false);backing.transform.localScale=new Vector3(.84f,.62f,.008f);
            var collider=backing.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            var material=new Material(Shader.Find("BoneAssembly/LearningPanelOverlay")){name="Learning card background",color=new Color(.018f,.045f,.06f,.98f),renderQueue=3990};backing.GetComponent<Renderer>().sharedMaterial=material;
            var font=Resources.Load<Font>("Fonts/Sarabun-Regular");
            learningFont=font;
            title=Text("Thai bone name",new Vector3(-.38f,.27f,-.012f),font,.008f,72,new Color(.35f,1,.75f));
            english=Text("English bone name",new Vector3(-.38f,.20f,-.012f),font,.006f,64,Color.white);
            body=Text("Bone explanation",new Vector3(-.38f,.13f,-.012f),font,.0075f,64,Color.white);
        }
        TextMesh Text(string name,Vector3 position,Font font,float size,int resolution,Color color)
        {
            var go=new GameObject(name);go.transform.SetParent(panel.transform,false);go.transform.localPosition=position;
            var text=go.AddComponent<TextMesh>();text.font=font;text.fontSize=resolution;text.characterSize=size;text.anchor=TextAnchor.UpperLeft;text.lineSpacing=1.15f;text.color=color;
            if(font)go.GetComponent<MeshRenderer>().sharedMaterial=new Material(font.material){renderQueue=4000};
            return text;
        }
        void SyncFontTexture(){if(!learningFont)return;foreach(var text in new[]{title,english,body})if(text)text.GetComponent<Renderer>().sharedMaterial.mainTexture=learningFont.material.mainTexture;}
        void Update(){if(panel&&panel.activeSelf)SyncFontTexture();UpdateVisibility(Time.time);}
        public void UpdateVisibility(float now){if(panel&&now-shown>=(settings?settings.informationSeconds:DisplayDuration))panel.SetActive(false);}
        public void Clear(){selected=null;if(panel)panel.SetActive(false);}
    }
}
