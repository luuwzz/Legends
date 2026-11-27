using UnityEngine;

namespace DarkAwakening
{
    /// <summary>A drifting, pulsing firefly (billboard quad tinted through a property block).</summary>
    public class DAFirefly : MonoBehaviour
    {
        public Renderer rend;
        public float size = 0.07f;
        public float radius = 0.9f;
        Vector3 origin;
        float seed;
        MaterialPropertyBlock mpb;
        Camera cam;

        void Awake()
        {
            origin = transform.position;
            seed = Random.value * 50f;
            mpb = new MaterialPropertyBlock();
            if (rend == null) rend = GetComponentInChildren<Renderer>();
        }

        void LateUpdate()
        {
            float t = Time.time * 0.35f + seed;
            Vector3 off = new Vector3(Mathf.PerlinNoise(t, 1.3f) - 0.5f, (Mathf.PerlinNoise(2.1f, t) - 0.5f) * 0.6f, Mathf.PerlinNoise(t * 0.9f, 4.7f) - 0.5f) * 2f * radius;
            transform.position = origin + off;
            if (cam == null) cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            float pulse = Mathf.Clamp01(Mathf.Sin(Time.time * 1.7f + seed * 3f) * 1.6f);
            float s = size * (0.6f + 0.6f * pulse);
            transform.localScale = new Vector3(s, s, s);
            if (rend != null)
            {
                mpb.SetColor("_Color", new Color(0.85f, 1f, 0.45f, 0.15f + 0.85f * pulse));
                rend.SetPropertyBlock(mpb);
            }
        }
    }
}
