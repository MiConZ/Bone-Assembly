# คู่มือแก้เกม Bone Assembly ใน Unity

เปิด `Assets/Scenes/BoneAssemblyVR.unity` และออกจาก Play Mode ก่อนแก้ฉาก แล้วกด Ctrl+S เพื่อบันทึก

## ค่าตั้งเกม (ScriptableObject)

เลือก `Assets/BoneAssembly/Configuration/BoneGameSettings.asset` หรือเลือกรากฉากแล้วกด **Open Game Settings**

- Session: เวลาต่อรอบ วางศีรษะ/มือง่ายแค่ไหน และเริ่มแบบนั่ง
- Seat: ระยะกระดูกจากมือ หน่วยเป็นเมตร
- Learning card: เวลาแสดง (เริ่มต้น 4 วินาที) และตำแหน่ง/ขนาดเริ่มต้นของการ์ดใหม่
- Bones: มีข้อมูล 22 ชิ้น แก้ชื่อไทย ชื่ออังกฤษ และคำอธิบายได้ **อย่าเปลี่ยน Bone Key** เพราะใช้เชื่อมกับโมเดล
- UI: ชื่อปุ่มที่เปลี่ยนตามสถานะ เช่น HINT / REVIEW และชื่อ Assignment

ค่าเกมอ่านเมื่อเริ่ม Play ครั้งถัดไป ส่วนคำอธิบายอ่านเมื่อกดกระดูก การแก้ ScriptableObject ใน Play Mode อาจติดอยู่หลังหยุดเกม จึงแนะนำให้แก้นอก Play Mode

## หมวดใน Hierarchy

| หมวด | สิ่งที่แก้ได้ |
|---|---|
| 01 Environment | ผนัง พื้น ตู้ อุปกรณ์ และรอยต่อ |
| 02 Lighting | ไฟห้องและโคมผ่าตัด |
| 03 Workbench | เตียงและถาดวางกระดูก |
| 04 Bones - pick up | กระดูกแยกตามบริเวณร่างกาย |
| 05 Targets - placement guides | โมเดลแนวตำแหน่งประกอบ |
| 06 UI | หน้าจอ Desktop และป้าย/ปุ่มใน VR |
| 07 Player and VR | กล้องและตัวควบคุมผู้เล่น |

ย้ายวัตถุภายในหมวดได้ โดยคงชื่อและ component ที่ระบบใช้ อย่าเปลี่ยนสเกล/หมุนรากฉากหรือหมวดกระดูก เพราะตำแหน่งเป้าหมายเกมเป็น world coordinates

## แก้ UI ด้วยมือ

**หน้าจอ Desktop:** เปิด `06 UI > Desktop Canvas - edit layout here` เลือก Module menu หรือปุ่ม แล้วปรับ Rect Transform, Image, Text และ Button ได้เหมือน UI ปกติ ไม่ต้องแก้ OnGUI อีก

ปุ่มแต่ละอันมี `Bone UI Command` ระบุคำสั่ง เช่น `assignment:0`, `test`, `hint`, `reset`, `next`, `seated`, `text`, `sound` ถ้าต้องการแก้ Caption ในฉากเอง ให้ปิด **Caption From Settings** ก่อน (ปุ่ม Hint เปลี่ยนเป็น Review เมื่อจบเกมจึงใช้ข้อความจาก Settings)

**VR console and reach:** In `03 Workbench > Workstation anchors`, move or rotate **Player Start**, **Blueprint**, **Side Console**, **Left Tray**, or **Right Tray** in Edit Mode. The blue Z arrow on Player Start sets the recenter direction. Select Workstation anchors with Gizmos enabled to see the reach guide and slot centers. Each curved tray has a named slot for each bone. The tray top is 0.82 m above the floor. The desktop preview uses Seated Eye Height in BoneGameSettings (1.30 m by default). VR follows the tracked headset height. Move slots along the tray surface and keep room between adjacent bones. Save the scene and check Full Body for the densest layout.

**Edit the playable layout:** Open the scene outside Play Mode and expand **03 Workbench → Workstation anchors**. Move an ivory bone under its tray slot to edit its resting pose. Move a slot or tray to move its contents. Under **Blueprint → Upright anatomy blueprint**, move a target to edit its snap pose. The side console and desktop preview camera are also scene objects. Save the scene normally. Starting a lesson and recovering loose bones use these saved poses.

**Hand alignment:** Select `07 Player and VR > Player Rig > Lab Player`. Under Left Hand or Right Hand, adjust Glove Position and Glove Euler while pointing forward, sideways, and down. Use Aim For Glove uses the controller’s measured pointing orientation at the grip position. Turn it off to use grip orientation. Fallback Aim Position and Euler apply when the runtime supplies only grip tracking. Copy tuned values back to the component in Edit Mode to save them.

**การ์ดความรู้:** เปิด `07 Player and VR > Player Rig > Main Camera > Bone learning card` เปิดวัตถุชั่วคราวเพื่อจัดตำแหน่ง/ขนาดและแก้สไตล์ใน Scene View แล้วปิดกลับ ระบบเติมข้อความและเปิดการ์ดเมื่อกดกระดูก ทั้งกระดูกที่ถือและที่วางเสร็จแล้ว

อย่าลบ reference ของ Status, Feedback, Camera หรือปุ่ม และไม่ควรเปลี่ยนชื่อวัตถุระบบ เช่น StatusBoard, Feedback, Label hint

## สำรองและเตรียมฉากอื่น

เมนู **Bone Assembly > Prepare Editable Scene** เตรียมฉากเดิมโดยไม่สร้างห้องใหม่ และเติมเฉพาะข้อมูลที่ยังไม่มี มีสำรองไฟล์ฉากก่อนเตรียมที่ `tmp/BoneAssemblyVR-before-authoring.unity` ส่วนฉากหลักที่เตรียมแล้วบันทึกอยู่ใน Assets/Scenes ตามเดิม

ปรับตำแหน่งอุปกรณ์ด้วย Workstation anchors ใน Scene View แล้วบันทึกฉาก
