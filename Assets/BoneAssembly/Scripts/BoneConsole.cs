using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BoneAssembly
{
    public class BoneConsole : MonoBehaviour
    {
        BoneAssemblyGame game;
        Canvas canvas;
        GraphicRaycaster graphics;
        RectTransform panel;
        Text heading, detail;
        Font font;
        readonly List<Button> buttons = new List<Button>();
        readonly List<RaycastResult> hits = new List<RaycastResult>();
        readonly Button[] hover = new Button[2];
        PointerEventData pointer;
        bool controls;
        int page = -1;
        float scale;
        static readonly Color ink = new Color(.04f, .10f, .13f);
        static readonly Color key = new Color(.08f, .30f, .33f);
        static readonly Color focus = new Color(.12f, .49f, .51f);

        public void Initialize(BoneAssemblyGame source)
        {
            game = source;
            var legacy = game.FindNamed("World panels - VR");
            if (legacy) legacy.gameObject.SetActive(false);
            else foreach (var t in game.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<LabAction>() || t.GetComponent<TextMesh>() || t.name == "Session console")
                    t.gameObject.SetActive(false);
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("Lesson console", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)).GetComponent<RectTransform>();
            panel.SetParent(transform, false);
            panel.sizeDelta = new Vector2(540, 640);
            panel.localScale = Vector3.one * .0011f;
            canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = game.playerCamera;
            graphics = panel.GetComponent<GraphicRaycaster>();
            var background = panel.gameObject.AddComponent<Image>();
            background.color = ink;
            background.raycastTarget = false;
            heading = Label(panel, "Heading", 24, 22, 492, 52, 36);
            detail = Label(panel, "Details", 24, 84, 492, 126, 23);
            Refresh();
        }

        RectTransform Rect(Transform parent, string label, int x, int y, int w, int h)
        {
            var rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        Text Label(Transform parent, string name, int x, int y, int w, int h, int size)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
            return text;
        }

        void Action(string caption, string command)
        {
            int index = buttons.Count;
            var rect = Rect(panel, caption, 24 + index % 2 * 250, 220 + index / 2 * 57, 240, 50);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = key;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var label = Label(rect, "Caption", 6, 0, 228, 50, Mathf.RoundToInt(21 * scale));
            label.alignment = TextAnchor.MiddleCenter;
            label.text = caption;
            button.onClick.AddListener(() => {
                if (command == "controls") controls = !controls;
                else
                {
                    if (command == "lessons" || command == "reset") controls = false;
                    game.Command(command);
                }
                Refresh();
            });
            buttons.Add(button);
        }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            // In XR the two controller rays dispatch explicitly. Keep mouse dispatch via EventSystem on desktop.
            graphics.enabled = !UnityEngine.XR.XRSettings.isDeviceActive;
            int next = controls ? (game.selectingAssignment ? 4 : 3) : game.selectingAssignment ? 0 : game.completed || game.lost ? 2 : 1;
            if (page != next || scale != game.textScale)
            {
                foreach (var button in buttons) { button.gameObject.SetActive(false); Destroy(button.gameObject); }
                buttons.Clear();
                System.Array.Clear(hover, 0, hover.Length);
                scale = game.textScale;
                page = next;
                heading.fontSize = Mathf.RoundToInt(34 * scale);
                detail.fontSize = Mathf.RoundToInt(21 * scale);
                if (page == 0)
                {
                    string[] assignments = { "1  Torso", "2  Arms & hands", "3  Legs & feet", "4  Full body" };
                    for (int i = 0; i < assignments.Length; i++) Action(assignments[i], "assignment:" + i);
                    for (int i = 0; i < game.stages.Length; i++) Action("Test: " + game.stages[i], "exam:" + i);
                }
                if (page == 2)
                {
                    if (game.completed) { Action("Review", "review"); Action("Next lesson", "next"); }
                    Action("Retry", "reset");
                    Action("Choose lesson", "lessons");
                }
                if (page == 1 && game.practice) Action("Hint", "hint");
                if (controls)
                {
                    Action("Recenter player", "seated"); Action("Text size", "text");
                    Action("Board higher", "board:raise"); Action("Board lower", "board:lower");
                    Action("Board nearer", "board:near"); Action("Board farther", "board:far");
                    Action("Board smaller", "board:smaller"); Action("Board larger", "board:larger");
                    Action("Board defaults", "board:defaults"); Action("Sound on / off", "sound");
                    if (!game.selectingAssignment) { Action("Recover bones", "recover"); Action("Restart lesson", "reset"); Action("Choose lesson", "lessons"); }
                }
                Action(controls ? "Back" : "Controls", "controls");
            }
            heading.text = controls ? "Controls" : page == 0 ? "Choose a lesson" : page == 2 ? (game.completed ? "Lesson complete" : "Time is up") : game.SessionName;
            if (page == 0) detail.text = "Choose an assignment with a placement guide, or test yourself on a body region.";
            else if (controls) detail.text = "Grip to pick up or drop. Move the stick to adjust reach. Trigger submits. Hold A / X to regrip. B / Y toggles bone information.";
            else {
                int seconds = Mathf.CeilToInt(game.RemainingTime);
                detail.text = $"{seconds / 60:00}:{seconds % 60:00}     {game.ActivePieces.Count(p => p.placed)} / {game.ActivePieces.Count()} assembled\n\n{game.message}";
            }
        }

        public void ClearHover(int hand)
        {
            var old = hover[hand];
            hover[hand] = null;
            if (old && old != hover[1 - hand]) old.image.color = key;
        }

        public bool Point(int hand, Ray ray, bool click, out float distance)
        {
            ClearHover(hand);
            var plane = new Plane(panel.forward, panel.position);
            if (!plane.Raycast(ray, out distance) || distance > 4 || Vector3.Dot(panel.forward, ray.direction) <= 0) return false;
            var point = ray.GetPoint(distance);
            if (!panel.rect.Contains(panel.InverseTransformPoint(point))) return false;
            var screen = canvas.worldCamera.WorldToScreenPoint(point);
            if (EventSystem.current && screen.z > 0)
            {
                pointer ??= new PointerEventData(EventSystem.current);
                pointer.position = screen;
                hits.Clear();
                graphics.Raycast(pointer, hits);
                foreach (var hit in hits)
                {
                    var button = hit.gameObject.GetComponent<Button>();
                    if (!button) continue;
                    hover[hand] = button;
                    button.image.color = focus;
                    if (click) button.onClick.Invoke();
                    break;
                }
            }
            // A trigger aimed at blank console space must not submit a held bone.
            return true;
        }
    }
}
