using UnityEngine;

namespace DarkAwakening
{
    /// <summary>Candle / flame style flicker for a light, with a smooth on/off.</summary>
    public class DALightFlicker : MonoBehaviour
    {
        public Light target;
        public float baseIntensity = 1.2f;
        public float amount = 0.35f;
        public float speed = 7f;
        public bool on = true;
        public float fadeSpeed = 3f;
        public GameObject flameVisual;       // optional billboard flame
        float k;
        float seed;

        void Awake()
        {
            seed = Random.value * 30f;
            if (target == null) target = GetComponent<Light>();
            k = on ? 1f : 0f;
        }

        void Update()
        {
            k = Mathf.MoveTowards(k, on ? 1f : 0f, fadeSpeed * Time.deltaTime);
            if (target != null)
            {
                float f = 1f + (Mathf.PerlinNoise(Time.time * speed, seed) - 0.5f) * 2f * amount;
                target.intensity = baseIntensity * f * k;
                target.enabled = k > 0.01f;
            }
            if (flameVisual != null)
            {
                bool vis = k > 0.05f;
                if (flameVisual.activeSelf != vis) flameVisual.SetActive(vis);
                float s = Mathf.Lerp(0.3f, 1f, k) * (0.9f + 0.2f * Mathf.PerlinNoise(Time.time * 9f, seed + 5f));
                flameVisual.transform.localScale = new Vector3(s * flameBase.x, s * flameBase.y, 1f);
            }
        }

        Vector3 flameBase = new Vector3(0.05f, 0.09f, 1f);

        public void SetFlameBase(float w, float h) { flameBase = new Vector3(w, h, 1f); }
    }
}
