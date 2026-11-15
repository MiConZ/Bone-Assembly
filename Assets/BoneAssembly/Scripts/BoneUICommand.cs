using UnityEngine;
using UnityEngine.UI;
namespace BoneAssembly
{
    [RequireComponent(typeof(Button))]
    public class BoneUICommand : MonoBehaviour
    {
        [Tooltip("assignment:0..3, test, mode, hint, reset, next, seated, text, sound")]
        public string command;
        [Tooltip("Use the caption from Game Settings. Untick to edit the child Caption text directly.")]
        public bool captionFromSettings = true;
    }
}
