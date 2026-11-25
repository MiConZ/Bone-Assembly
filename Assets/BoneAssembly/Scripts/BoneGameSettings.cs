using System;
using System.Collections.Generic;
using UnityEngine;

namespace BoneAssembly
{
    [CreateAssetMenu(menuName="Bone Assembly/Game Settings", fileName="BoneGameSettings")]
    public class BoneGameSettings : ScriptableObject
    {
        [Header("Session / เวลาและการช่วยวาง")]
        [Min(1)] public float assignmentSeconds = 600;
        public bool startSeated = true;
        [Range(.12f,.5f)] public float headSnapDistance = .25f;
        [Range(.12f,.5f)] public float handSnapDistance = .20f;
        [Header("Seat / ตำแหน่งผู้เล่น (เมตร)")]
        public Vector3 seatedAnchor = new Vector3(0,0,-.15f);
        [Tooltip("Desktop preview eye height in metres. The headset supplies its own tracked eye height.")]
        [Range(1,3)] public float seatedEyeHeight = 1.30f;
        [Range(.05f,.3f)] public float holdDistance = .12f;
        [Header("Comfortable interaction")]
        [Range(.3f,4f)] public float magnetSpeed=1.5f;
        [Range(8f,30f)] public float attachmentGain=18f;
        [Range(1f,4f)] public float maximumReach=3.5f;
        [Range(.5f,1.8f)] public float boardDistance=.9f;
        [Range(.35f,1f)] public float boardScale=.6f;
        [Range(-.8f,.5f)] public float boardHeightOffset=-.2f;
        public GameObject leftHandPrefab,rightHandPrefab;
        [Min(.02f)] public float cardGap=.08f;
        [Header("Learning card / การ์ดความรู้")]
        [Min(.5f)] public float informationSeconds = 4;
        public Vector3 cardPosition = new Vector3(.82f,-.03f,1.3f);
        [Range(.5f,2)] public float cardScale = 1.25f;
        public List<BoneInformation> bones = new List<BoneInformation>();
        [Header("UI / ข้อความที่เปลี่ยนตามสถานะ")]
        public string assignmentPrefix = "ASSIGNMENT";
        public string testLabel = "TEST MODE", modeLabel = "ASSIGNMENT / TEST";
        public string hintLabel = "HINT", reviewLabel = "REVIEW", resetLabel = "RESET", nextLabel = "NEXT";
        public string recenterLabel = "RECENTER SEAT", textSizeLabel = "TEXT SIZE", soundLabel = "SOUND";
        [TextArea] public string seatedInstructions = "SEATED / GRIP: PICK UP / TRIGGER: AIM AND PLACE";
        public BoneLearningCard Information(BonePiece piece)
        {
            var entry=bones.Find(b=>b.boneKey==piece.label);
            return entry==null ? BoneEducation.Get(piece) : new BoneLearningCard(entry.thaiName,entry.englishName,entry.explanation);
        }
    }
    [Serializable]
    public class BoneInformation
    {
        [Tooltip("Stable key matching BonePiece.label; change the display names below instead.")]
        public string boneKey;
        public string thaiName, englishName;
        [TextArea(3,8)] public string explanation;
    }
}
