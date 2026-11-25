using UnityEngine;

namespace BoneAssembly
{
    public class BoneStation : MonoBehaviour
    {
        public Transform playerStart, blueprint, console, leftTray, rightTray;

        public static BoneStation Prepare(BoneAssemblyGame game)
        {
            var existing = game.GetComponentInChildren<BoneStation>(true);
            if (existing) return existing;

            var parent = game.FindNamed("03 Workbench") ?? game.transform;
            foreach (var part in game.GetComponentsInChildren<Transform>(true))
                if (part.name == "Instrument trolley" || part.name == "Tray sterile lining" ||
                    part.name == "Trolley leg" || part.name == "Caster")
                    part.gameObject.SetActive(false);

            var prefab = Resources.Load<GameObject>("Curved Workstation");
            var station = Instantiate(prefab, parent, false).GetComponent<BoneStation>();
            station.name = "Workstation anchors";
            return station;
        }

        public Transform SlotFor(BonePiece piece)
        {
            string name = "Slot - " + piece.label;
            return leftTray.Find(name) ?? rightTray.Find(name);
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
