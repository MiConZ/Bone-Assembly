using UnityEditor;
using UnityEngine;
namespace BoneAssembly.Editor
{
    [CustomEditor(typeof(BoneAssemblyGame))]
    public class BoneAssemblyGameInspector : UnityEditor.Editor
    {
        bool advanced;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("ปรับเวลา / ที่นั่ง / การช่วยวาง / คำอธิบาย ที่ Settings\nแก้หน้าตา UI ใน 06 UI และแก้ฉากในหมวด 01–07\nแก้ฉากนอก Play Mode เพื่อบันทึกการเปลี่ยนแปลง",MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"));
            var game=(BoneAssemblyGame)target;
            if(game.settings&&GUILayout.Button("Open Game Settings / เปิดค่าตั้งเกม"))Selection.activeObject=game.settings;
            if(game.desktopUI&&GUILayout.Button("Select Desktop Canvas / แก้หน้าจอ"))Selection.activeGameObject=game.desktopUI.gameObject;
            if(!game.authoringPrepared&&!Application.isPlaying&&GUILayout.Button("Prepare Editable Scene"))BoneAuthoringSetup.Prepare();
            advanced=EditorGUILayout.Foldout(advanced,"Advanced / Runtime and references",true);
            if(advanced)DrawPropertiesExcluding(serializedObject,"m_Script","settings");
            serializedObject.ApplyModifiedProperties();
        }
    }
}
