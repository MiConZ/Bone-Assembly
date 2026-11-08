using System.Globalization;
using System.Text;

namespace BoneAssembly
{
    public struct BoneLearningCard
    {
        public string ThaiName,EnglishName,Explanation;
        public BoneLearningCard(string thai,string english,string explanation){ThaiName=thai;EnglishName=english;Explanation=explanation;}
    }
    public static class BoneEducation
    {
        public static BoneLearningCard Get(BonePiece piece)
        {
            string name=piece.label,side=name.StartsWith("Left ")?"ข้างซ้าย":name.StartsWith("Right ")?"ข้างขวา":"";
            string thai,description;
            if(piece.IsHead){thai="กะโหลกและขากรรไกรล่าง";description="กะโหลกช่วยปกป้องสมอง\nขากรรไกรล่างเคลื่อนไหวได้\nและช่วยในการบดเคี้ยวอาหาร";}
            else if(piece.IsHand){thai="ชุดกระดูกมือ";description="รวมกระดูกข้อมือ ฝ่ามือ และนิ้วมือ\nช่วยให้กำและหยิบจับสิ่งของ\nเชื่อมต่อกับกระดูกปลายแขนที่ข้อมือ";}
            else if(name.Contains("lower leg")){thai="ชุดหน้าแข้งและเข่า";description="Tibia คือกระดูกหน้าแข้ง รับน้ำหนัก\nFibula คือกระดูกน่อง อยู่ด้านข้าง\nPatella คือสะบ้า อยู่หน้าเข่า\nช่วยการทำงานของกล้ามเนื้อเหยียดเข่า";}
            else if(name.Contains("femur")){thai="กระดูกต้นขา";description="อยู่ระหว่างสะโพกกับหัวเข่า\nเป็นกระดูกยาวที่รับน้ำหนักร่างกาย\nมีส่วนร่วมในการยืนและเดิน";}
            else if(name.Contains("humerus")){thai="กระดูกต้นแขน";description="อยู่ระหว่างหัวไหล่กับข้อศอก\nต่อกับสะบักที่หัวไหล่\nและต่อกับกระดูกปลายแขนที่ข้อศอก";}
            else if(name.Contains("forearm")){thai="กระดูกปลายแขน";description="ประกอบด้วย Radius และ Ulna\nRadius อยู่ด้านนิ้วหัวแม่มือ\nUlna อยู่ด้านนิ้วก้อย\nช่วยให้ปลายแขนหมุนคว่ำและหงาย";}
            else if(name.Contains("scapula")){thai="กระดูกสะบัก";description="เป็นกระดูกแบนด้านหลังหัวไหล่\nมีเบ้าสำหรับต่อกับกระดูกต้นแขน\nช่วยรองรับการเคลื่อนไหวของไหล่";}
            else if(name.Contains("clavicle")){thai="กระดูกไหปลาร้า";description="อยู่ด้านหน้าบริเวณหัวไหล่\nเชื่อมกระดูกอกกับกระดูกสะบัก\nช่วยพยุงตำแหน่งของหัวไหล่";}
            else if(name.Contains("forefoot")){thai="กระดูกส่วนหน้าเท้า";description="เป็นกลุ่มกระดูกฝ่าเท้าและนิ้วเท้า\nช่วยรับและถ่ายน้ำหนักขณะยืน\nรวมถึงการส่งแรงขณะเดิน";}
            else if(name.Contains("hindfoot")){thai="กระดูกหลังเท้าและกลางเท้า";description="เป็นกลุ่มกระดูกบริเวณข้อเท้าและเท้า\nช่วยรองรับน้ำหนักร่างกาย\nและประกอบเป็นโครงสร้างของเท้า";}
            else if(name.Contains("Pelvic")){thai="กระดูกเชิงกราน";description="เชื่อมโครงร่างลำตัวกับขาทั้งสองข้าง\nช่วยถ่ายน้ำหนักจากลำตัวลงสู่ขา\nและรองรับอวัยวะในอุ้งเชิงกราน";}
            else if(name.Contains("Thoracic")){thai="กระดูกทรวงอก";description="ประกอบด้วยกลุ่มซี่โครงและกระดูกอก\nช่วยปกป้องหัวใจและปอด\nทรวงอกเคลื่อนไหวขณะหายใจ";}
            else if(name.Contains("Vertebral")){thai="กระดูกสันหลัง";description="เรียงเป็นแนวกลางด้านหลังลำตัว\nช่วยพยุงศีรษะและลำตัว\nและล้อมเป็นช่องปกป้องไขสันหลัง";}
            else {thai="กระดูกในระบบโครงร่าง";description="สังเกตรูปร่างและตำแหน่งของชิ้นนี้\nแล้วเทียบกับตำแหน่งบนโครงกระดูก\nก่อนนำไปประกอบเข้าที่";}
            string english=name.Contains("lower leg")?(name.StartsWith("Left ")?"Left ":"Right ")+"knee and lower leg":name;
            return new BoneLearningCard(thai+(side.Length>0?" "+side:""),english,description);
        }
        public static string Wrap(string text,int columns)
        {
            var result=new StringBuilder();var elements=StringInfo.GetTextElementEnumerator(text);int count=0;
            while(elements.MoveNext()){
                string element=elements.GetTextElement();
                if(element=="\n"){result.Append(element);count=0;continue;}
                if(count>=columns){result.Append('\n');count=0;}
                result.Append(element);count++;
            }
            return result.ToString();
        }
    }
}
