# คู่มือแก้เกม Bone Assembly ใน Unity

เปิด `Assets/Scenes/BoneAssemblyVR.unity` และออกจาก Play Mode ก่อนแก้ฉาก แล้วกด Ctrl+S เพื่อบันทึก

## ค่าตั้งเกม (ScriptableObject)

เลือก `Assets/BoneAssembly/Configuration/BoneGameSettings.asset` หรือเลือกรากฉากแล้วกด **Open Game Settings**

- Session: เวลาต่อรอบ วางศีรษะ/มือง่ายแค่ไหน และเริ่มแบบนั่ง
- Seat: ตำแหน่งที่นั่ง ความสูงสายตา และระยะกระดูกจากมือ หน่วยเป็นเมตร
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

**ป้าย VR:** เปิด `06 UI > World panels - VR` เลือก Text Mesh เพื่อเปลี่ยนฟอนต์ สี หรือขนาด ป้ายคงที่เช่น Title แก้ข้อความได้โดยตรง ป้ายสถานะ/Feedback/ชื่อ Assignment ใช้ค่าจากระบบและ Settings ป้ายปุ่มเป็นลูกของตัวปุ่มแล้ว จึงย้ายพร้อมกันได้ การกด VR ใช้ Collider + LabAction เดิม

**การ์ดความรู้:** เปิด `07 Player and VR > Player Rig > Main Camera > Bone learning card` เปิดวัตถุชั่วคราวเพื่อจัดตำแหน่ง/ขนาดและแก้สไตล์ใน Scene View แล้วปิดกลับ ระบบเติมข้อความและเปิดการ์ดเมื่อกดกระดูก ทั้งกระดูกที่ถือและที่วางเสร็จแล้ว

อย่าลบ reference ของ Status, Feedback, Camera หรือปุ่ม และไม่ควรเปลี่ยนชื่อวัตถุระบบ เช่น StatusBoard, Feedback, Label hint

## สำรองและเตรียมฉากอื่น

เมนู **Bone Assembly > Prepare Editable Scene** เตรียมฉากเดิมโดยไม่สร้างห้องใหม่ และเติมเฉพาะข้อมูลที่ยังไม่มี มีสำรองไฟล์ฉากก่อนเตรียมที่ `tmp/BoneAssemblyVR-before-authoring.unity` ส่วนฉากหลักที่เตรียมแล้วบันทึกอยู่ใน Assets/Scenes ตามเดิม

ไม่ต้องกด Create Operating Theatre สำหรับแก้ UI เพราะเมนูนั้นสร้างห้องใหม่
