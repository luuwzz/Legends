using System;
using UnityEngine;

namespace DarkAwakening
{
    /// <summary>Something the player can point at and activate (trigger or 2 s gaze dwell).</summary>
    public class DAInteractable : MonoBehaviour
    {
        public string label = "";
        public bool interactable = true;
        public bool useDwell = true;

        [Header("Optional visuals")]
        public Transform marker;               // glow orb (billboard)
        public Renderer markerRenderer;
        public TMPro.TextMeshPro labelText;    // floating label, shown while hovered
        public float markerSize = 0.16f;
        public GameObject visualToHide;        // model hidden once the item is taken

        public event Action<DAInteractable> Activated;

        bool hovered;
        Material markerMat;
        float seed;
        Camera cam;

        void Awake()
        {
            seed = UnityEngine.Random.value * 10f;
            if (markerRenderer != null) markerMat = markerRenderer.material;
            if (labelText != null) { labelText.fontSize *= 0.3f; labelText.gameObject.SetActive(false); }
        }

        public void SetHovered(bool h)
        {
            hovered = h;
            if (labelText != null) labelText.gameObject.SetActive(h && interactable && !string.IsNullOrEmpty(label));
        }

        public void Activate()
        {
            if (!interactable) return;
            if (Activated != null) Activated(this);
        }

        bool consumed;

        /// <summary>Item taken for good: never interactable again and its model disappears.</summary>
        public void Consume()
        {
            consumed = true;
            SetInteractable(false);
            if (visualToHide != null) visualToHide.SetActive(false);
        }

        public void SetInteractable(bool v)
        {
            if (consumed) v = false;
            interactable = v;
            if (marker != null) marker.gameObject.SetActive(v);
            if (!v && labelText != null) labelText.gameObject.SetActive(false);
            // disable colliders so overlapping interactables never block each other
            foreach (Collider c in GetComponents<Collider>()) c.enabled = v;
            if (!v) hovered = false;
        }

        void LateUpdate()
        {
            if (marker == null || !interactable) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 3f + seed);
            float s = markerSize * pulse * (hovered ? 1.35f : 1f);
            marker.localScale = new Vector3(s, s, s);
            Vector3 d = marker.position - cam.transform.position;
            if (d.sqrMagnitude > 0.0001f) marker.rotation = Quaternion.LookRotation(d);
            if (markerMat != null)
            {
                Color c = hovered ? new Color(1f, 0.95f, 0.75f, 1f) : new Color(1f, 0.82f, 0.45f, 0.75f + 0.25f * Mathf.Sin(Time.time * 2.2f + seed));
                markerMat.color = c;
            }
            if (labelText != null && labelText.gameObject.activeSelf)
            {
                Vector3 dl = labelText.transform.position - cam.transform.position;
                if (dl.sqrMagnitude > 0.0001f) labelText.transform.rotation = Quaternion.LookRotation(dl);
            }
        }
    }
}
