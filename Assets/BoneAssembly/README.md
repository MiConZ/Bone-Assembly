# Bone Assembly VR — Operating Theatre

โปรเจกต์ Unity 6000.5.9f1 / Universal Render Pipeline สำหรับฝึกประกอบกระดูกในห้องผ่าตัด

การแก้ค่าตั้งเกม ข้อมูลกระดูก UI และหมวด Hierarchy: อ่าน [คู่มือแก้เกม](EDITING-GUIDE.md) เริ่มที่ `Configuration/BoneGameSettings.asset` หรือเลือกรากฉากแล้วกด **Open Game Settings**

## เปิดทดลอง

1. เปิด Scene `Assets/Scenes/BoneAssemblyVR.unity`
2. กด Play ใน Unity เลือกส่วนร่างกายทางซ้ายหรือบนจอภายในห้อง
3. หยิบกระดูกสีงาช้างจากถาด แล้ววางให้ตรงเงาบนกระดานแนวตั้ง ทั้งตำแหน่งและมุมหมุน

## Comfortable VR controls

Play seated or standing from one spot. The blueprint is upright; side trays remain horizontal. Use RECENTER WORKSPACE after changing posture. The left panel adjusts height, distance, and size. Size changes require releasing held bones and return loose pieces to the trays without resetting completed work.

- Grip near a bone: grab at the contact point. Aim and hold Grip: magnetically draw a distant bone toward your hand.
- Release Grip: drop with gravity and collisions, without submitting or counting a mistake.
- Thumbstick up/down while holding: extend/retract the bone.
- Trigger while holding: submit the actual position and rotation. All bones require manual alignment.
- Wrong submissions flash red and stay held. Feedback distinguishes wrong bone, wrong rotation, and move closer.
- Hold A (right) / X (left) while keeping Grip held: float the bone in place. Reposition your wrist and release A / X to capture the new grip without snapping.
- Grip near the bone with the other hand: transfer at that contact point.
- B / Y: hide/show the bone-side information card. It faces the player and stays visible while held.
- Trigger with an empty hand: select controls or inspect a completed bone.
- RESET LOOSE BONES: recover only uncompleted, unheld pieces. Score, timer, completed anatomy, and held pieces are preserved. Out-of-room/below-floor pieces are automatically rescued.
- RESTART: restart the assignment, including timer and completed pieces.

PolyOne rigged hands are configured as separate blue surgical gloves with controller-driven finger animation. Original assets are under `Assets/PolyOne`; derived prefabs and license/source notes are in `Assets/BoneAssembly/Hands`.

## Desktop testing

Click: grab/drop; Enter: submit. Mouse wheel: reach. Q/E: rotate; Shift changes axis. Right mouse: look. B: info. Backspace: recover loose bones. R: restart. Click world controls with an empty hand.

Stop and restart Unity Play Mode after code changes. Quest Link needs the headset OpenXR runtime. Installed APKs require rebuilding. Controller-free hand tracking is not implemented.

## รูปแบบการเล่น

ปรับ flow ตาม mockup PDF ฉบับใหม่ สไลด์ 15–21:

- หน้าเลือก Assignment 1–4 เป็นปุ่มสีแดงเรียงแนวตั้ง ใช้ได้ทั้งเมาส์และเส้นเล็ง VR
- ค่าเริ่มต้น: Assignment 1 ลำตัว, 2 แขนและมือ, 3 ขาและเท้า, 4 เต็มตัว (สไลด์ไม่ได้กำหนดเนื้อหาของแต่ละ Assignment)
- เวลาแต่ละรอบ 10:00 นาที นับถอยหลังและหยุดเมื่อจบเกม
- ใน Assignment เงาเป้าหมายกระพริบ 3 วินาทีแรก แล้วจางลงจนเหลือเงาบางที่วินาที 5
- ประกอบครบก่อนหมดเวลา: END GAME / WIN และเก็บกระดูกสีเขียวบนกระดานไว้ดูข้อมูล
- เวลาเหลือ 0:00 ก่อนประกอบครบ: END GAME / LOSE หยุดการหยิบ/วาง แสดงตำแหน่งที่ขาดเป็นสีแดง กด RESET เพื่อลองใหม่
- Test Mode เลือกส่วนร่างกายได้ ซ่อนเงาตำแหน่งตั้งแต่เริ่มและนับถอยหลัง 10 นาที

5 ด่าน: Hand, Upper Limb, Foot, Lower Limb, Full Body

- Practice: มีเงาตำแหน่งและคำใบ้; กระดูกทั่วไประยะยอมรับ 24 ซม. / มุม 55 องศา
- Test: ซ่อนเป้าหมายของชิ้นที่ต้องวางและปิดคำใบ้; กระดูกทั่วไประยะ 18 ซม. / มุม 40 องศา
- วางถูก: ล็อกตำแหน่ง สีเขียว เสียงยืนยัน และสั่นคอนโทรลเลอร์
- วางผิด: กระพริบสีแดงพร้อมข้อความอธิบาย และยังถือไว้เพื่อแก้ไข
- เก็บจำนวนชิ้น เวลา ความผิดพลาด คำใบ้ ความแม่นยำ และเวลาที่ดีที่สุดต่อด่าน/โหมดใน PlayerPrefs
- ถาดขยายและจัดวางตามขนาดจริงของแต่ละชุด แสดงทุกชิ้นตั้งแต่เริ่มด่าน ไม่มีการซ่อนชิ้นที่เกิน 8 ชิ้น
- มือแต่ละข้างหยิบเป็นชุดเดียว พร้อมพื้นที่หยิบขั้นต่ำ 14 ซม. และระยะช่วย snap 20 ซม. / มุม 60 องศา
- กระดูกหน้าแข้ง น่อง และสะบ้าของแต่ละข้างรวมเป็นหนึ่งชุด เพื่อลดชิ้นเล็กที่หยิบยาก

## โมเดลและขอบเขตต้นแบบ

ใช้ mesh จริง 245 ชิ้น (34,460 vertices) จาก `Skeleton.blend` ที่ผู้ใช้ให้มา แปลงเป็นข้อมูล mesh และบันทึกเป็น Unity assets แล้ว จึงไม่ต้องติดตั้ง Blender เพื่อเปิดเล่นโปรเจกต์

จัดรวมเป็น 22 หน่วยฝึกตามส่วนร่างกาย เช่น forearm รวม radius/ulna, มือทั้งข้าง และชุดหน้าแข้ง/น่อง/สะบ้า โดยยังเก็บ mesh เดิมครบ 245 ชิ้น ชื่อและการแบ่งกลุ่มเป็นการจัดหมวดเบื้องต้นจากรูปร่าง/ตำแหน่ง ควรให้ผู้สอนกายวิภาคตรวจความถูกต้องก่อนใช้ประเมินผู้เรียนจริง

คำอธิบายย่ออ้างอิง OpenStax Anatomy & Physiology: [กระดูกแขนและมือ](https://openstax.org/books/anatomy-and-physiology/pages/8-2-bones-of-the-upper-limb), [กระดูกขาและเท้า](https://openstax.org/books/anatomy-and-physiology/pages/8-4-bones-of-the-lower-limb), [ระบบโครงกระดูก](https://openstax.org/books/biology-2e/pages/38-1-types-of-skeletal-systems) ใช้ฟอนต์ Sarabun ภายใต้ SIL Open Font License ซึ่งแนบไว้ใน Resources/Fonts/OFL.txt

อ้างอิงแนวเกมจาก PDF ของผู้ใช้: การประกอบ 3D, การหยิบ/หมุน/snap, ด่านตามส่วนร่างกาย, feedback และการวัดเวลา โดยปรับฉากเป็นห้องผ่าตัดตามคำขอในแชต

## ตรวจงาน

เมนู `Bone Assembly > Verify Gameplay (Play Mode)` ตรวจทุกด่าน การปฏิเสธตำแหน่ง/มุมที่ผิด การรับชิ้นที่ถูก การจบด่าน และการปิดคำใบ้ในโหมดสอบ ผลอยู่ `tmp/verification-result.txt`

เมนู `Bone Assembly > Capture Theatre Preview` บันทึกภาพฉากลง `output/OperatingTheatre.png`

ยังไม่ได้ทดสอบประสิทธิภาพ ความสบาย การติดตามมือ และคอนโทรลเลอร์บน Quest จริง ไม่มี APK หรือ executable ที่บิลด์แล้ว

Current verification: `Bone Assembly > Verify Comfortable VR Handling (Play Mode)`. Results: `tmp/comfort-verification.txt`; preview: `output/ComfortWorkspace.png`. This covers physics, magnet cancellation, floating regrip, feedback, recovery, board adjustments, cards, assignments, and test stages. Physical Quest comfort and performance still require headset testing.
