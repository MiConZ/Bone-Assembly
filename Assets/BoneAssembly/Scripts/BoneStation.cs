using System.Linq;
using UnityEngine;

namespace BoneAssembly
{
    public class BoneStation : MonoBehaviour
    {
        public Transform playerStart, blueprint, console, leftTray, rightTray;

        Transform Anchor(string label, Vector3 position, float yaw = 0)
        {
            var anchor = new GameObject(label).transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = position;
            anchor.localRotation = Quaternion.Euler(0, yaw, 0);
            return anchor;
        }

        public static BoneStation Prepare(BoneAssemblyGame game)
        {
            var existing = game.GetComponentInChildren<BoneStation>(true);
            if (existing) return existing;
            var root = new GameObject("Workstation anchors").transform;
            root.SetParent(game.FindNamed("03 Workbench") ?? game.transform, false);
            var station = root.gameObject.AddComponent<BoneStation>();
            station.playerStart = station.Anchor("Player Start", new Vector3(0, 0, -.74f));
            station.blueprint = station.Anchor("Blueprint", new Vector3(0, 1.32f, .16f));
            station.console = station.Anchor("Side Console", new Vector3(1.45f, 1.44f, .42f), 51);
            station.leftTray = station.Anchor("Left Tray", new Vector3(-.73f, .925f, .34f));
            station.rightTray = station.Anchor("Right Tray", new Vector3(.73f, .925f, .34f));
            foreach (var tray in new[] { station.leftTray, station.rightTray })
            {
                var original = game.GetComponentsInChildren<Transform>(true)
                    .Where(t => t.name == "Instrument trolley" || t.name == "Tray sterile lining" ||
                                t.name == "Trolley leg" || t.name == "Caster")
                    .Where(t => !t.IsChildOf(root) && (t.position.x < 0) == (tray == station.leftTray)).ToArray();
                var lining = original.First(t => t.name == "Tray sterile lining");
                var oldCenter = lining.position + Vector3.up * lining.GetComponent<Renderer>().bounds.extents.y;
                foreach (var part in original)
                {
                    part.position += tray.position - oldCenter;
                    part.SetParent(tray, true);
                }
                for (int i = 0; i < 12; i++)
                {
                    var slot = new GameObject($"Slot {i + 1:00}").transform;
                    slot.SetParent(tray, false);
                    float inner = tray == station.leftTray ? .25f : -.25f;
                    slot.localPosition = new Vector3(i % 2 == 0 ? inner : -inner, .008f, -.98f + i / 2 * .39f);
                }
            }
            return station;
        }

        void OnDrawGizmosSelected()
        {
            if (!playerStart) return;
            Gizmos.color = new Color(.2f, .85f, .7f);
            Gizmos.DrawWireSphere(playerStart.position + Vector3.up * 1.12f, .65f);
            foreach (var tray in new[] { leftTray, rightTray })
                if (tray) foreach (Transform slot in tray)
                    if (slot.name.StartsWith("Slot ")) Gizmos.DrawWireSphere(slot.position, .035f);
        }
    }
}
