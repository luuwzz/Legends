using UnityEngine;

namespace DarkAwakening
{
    /// <summary>Gentle wind sway for plants (rotates the object slightly with smooth noise).</summary>
    public class DASway : MonoBehaviour
    {
        public float amplitude = 2.5f;     // degrees
        public float speed = 0.35f;
        float seed;
        Quaternion baseRot;

        void Awake()
        {
            seed = Random.value * 100f;
            baseRot = transform.localRotation;
        }

        void Update()
        {
            float t = Time.time * speed + seed;
            float a = (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * amplitude;
            float b = (Mathf.PerlinNoise(0.7f, t * 1.13f) - 0.5f) * 2f * amplitude;
            transform.localRotation = baseRot * Quaternion.Euler(a, 0f, b);
        }
    }
}
