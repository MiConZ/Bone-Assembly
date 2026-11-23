using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BoneAssembly
{
    // Layout and static text are authored in the scene. Only session values are refreshed.
    public class BoneDesktopUI : MonoBehaviour
    {
        public BoneAssemblyGame game;
        public Canvas canvas;
        public Text status, feedback;
        public Button[] assignments;
        public Text hintCaption;
        void Awake()
        {
            foreach(var action in GetComponentsInChildren<BoneUICommand>(true))
            {
                var captured=action;
                action.GetComponent<Button>().onClick.AddListener(()=>game.Command(captured.command));
                if(action.captionFromSettings&&game.settings)
                {
                    var caption=action.GetComponentInChildren<Text>();
                    if(caption)caption.text=Caption(action.command,caption.text);
                }
            }
        }
        string Caption(string command,string fallback)
        {
            var s=game.settings;
            if(command.StartsWith("assignment:"))return s.assignmentPrefix+" "+(int.Parse(command.Substring(11))+1);
            switch(command){case "test":return s.testLabel;case "hint":return s.hintLabel;case "reset":return "RESTART ASSIGNMENT";case "next":return s.nextLabel;case "seated":return "RECENTER WORKSPACE";case "text":return s.textSizeLabel;case "sound":return s.soundLabel;default:return fallback;}
        }
        void Update()
        {
            canvas.enabled=!UnityEngine.XR.XRSettings.isDeviceActive;
            if(!canvas.enabled)return;
            status.text=$"{game.SessionName}\n{Mathf.FloorToInt(game.RemainingTime/60):00}:{Mathf.FloorToInt(game.RemainingTime%60):00}\n{game.ActivePieces.Count(p=>p.placed)} / {game.ActivePieces.Count()} ASSEMBLED";
            feedback.text=game.message;
            if(hintCaption)hintCaption.text=game.settings?(game.completed?game.settings.reviewLabel:game.settings.hintLabel):(game.completed?"REVIEW":"HINT");
        }
    }
}
