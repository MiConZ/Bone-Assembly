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
        [Range(1,3)] public float seatedEyeHeight = 2.2f;
        [Range(.05f,.3f)] public float holdDistance = .12f;
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
