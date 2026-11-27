using System.Collections;
using UnityEngine;

namespace DarkAwakening
{
    /// <summary>
    /// Controls the floating Krasue. The imported FBX is Z-up (hair streams to +Y, entrails hang down along Z),
    /// so the builder puts it under 'Model' with a fixed rotation. The head sits at 'headOffset' above the root
    /// and we position the creature by where its HEAD should be.
    /// </summary>
    public class DAKrasue : MonoBehaviour
    {
        public Transform model;
        public Animator animator;
        public Light glow;
        public Transform glowTransform;     // follows the head, on the side facing the player
        public Transform auraTransform;     // soft red halo behind the head, facing the player
        public Vector3 headOffset = new Vector3(0f, 1.47f, 0f);
        public float bobAmplitude = 0.05f;
        public float modelYawOffset = 0f;       // tweak in the Inspector if she faces the wrong way

        Vector3 basePos;
        Quaternion faceRot = Quaternion.identity;
        Quaternion targetRot = Quaternion.identity;
        float seed;
        float intensityTarget = 1f;

        void Awake()
        {
            seed = Random.value * 20f;
            if (model != null) model.localRotation = Quaternion.Euler(-90f, modelYawOffset, 0f);
        }

        public void Show(bool v)
        {
            gameObject.SetActive(v);
            if (v) Play("idle");
        }

        public void PlaceHead(Vector3 headPos, Vector3 facing)
        {
            basePos = headPos;
            SetFacing(facing, true);
            Apply();
        }

        public void SetFacing(Vector3 dir, bool instant)
        {
            if (dir.sqrMagnitude < 0.0001f) return;
            Vector3 d = dir.normalized;
            // limit pitch so she never looks straight up/down
            d.y = Mathf.Clamp(d.y, -0.45f, 0.45f);
            targetRot = Quaternion.LookRotation(d.normalized, Vector3.up);
            if (instant) faceRot = targetRot;
        }

        Material auraMat; Color auraBase;

        void Update()
        {
            faceRot = Quaternion.Slerp(faceRot, targetRot, 1f - Mathf.Exp(-6f * Time.deltaTime));
            Apply();
            Camera cam = Camera.main;
            if (cam != null && (glowTransform != null || auraTransform != null))
            {
                Vector3 toCam = cam.transform.position - basePos;
                float d = toCam.magnitude;
                Vector3 dir = d > 0.01f ? toCam / d : Vector3.back;
                // light sits in front of her face (towards the player), never beyond the player
                if (glowTransform != null) glowTransform.position = basePos + dir * Mathf.Min(0.35f, d * 0.5f) + Vector3.up * 0.04f;
                if (auraTransform != null)
                {
                    auraTransform.position = basePos - dir * 0.12f;
                    // the halo only shows when she is close: far away it would read as a red disc at the window
                    if (auraMat == null) { Renderer ar = auraTransform.GetComponent<Renderer>(); if (ar != null) { auraMat = ar.material; auraBase = auraMat.color; } }
                    if (auraMat != null) { Color ac = auraBase; ac.a *= Mathf.Clamp01(1.25f - d / 6f); auraMat.color = ac; }
                    auraTransform.rotation = Quaternion.LookRotation(auraTransform.position - cam.transform.position, Vector3.up);
                }
            }
            if (glow != null)
            {
                float f = 0.8f + 0.2f * Mathf.PerlinNoise(Time.time * 6f, seed);
                glow.intensity = Mathf.Lerp(glow.intensity, 2.2f * intensityTarget * f, 1f - Mathf.Exp(-8f * Time.deltaTime));
            }
        }

        void Apply()
        {
            Vector3 bob = new Vector3(Mathf.Sin(Time.time * 0.9f + seed) * 0.03f,
                                      Mathf.Sin(Time.time * 1.7f + seed) * bobAmplitude,
                                      Mathf.Cos(Time.time * 1.1f + seed) * 0.03f);
            transform.rotation = faceRot;
            transform.position = basePos + bob - faceRot * headOffset;
        }

        public Vector3 HeadPosition { get { return basePos; } }

        public void SetGlow(float k) { intensityTarget = k; }

        public void Play(string state)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            animator.CrossFade(state, 0.15f);
        }

        public IEnumerator MoveHead(Vector3 to, float seconds)
        {
            Vector3 from = basePos;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, seconds));
                basePos = Vector3.Lerp(from, to, k);
                yield return null;
            }
            basePos = to;
        }

        /// <summary>Quick strike toward a point and back (jump-scare).</summary>
        public IEnumerator Lunge(Vector3 strikePoint, float inSeconds, float outSeconds)
        {
            Vector3 from = basePos;
            float t = 0f;
            while (t < inSeconds)
            {
                t += Time.deltaTime;
                float k = t / Mathf.Max(0.01f, inSeconds);
                basePos = Vector3.Lerp(from, strikePoint, k * k);
                yield return null;
            }
            basePos = strikePoint;
            t = 0f;
            while (t < outSeconds)
            {
                t += Time.deltaTime;
                basePos = Vector3.Lerp(strikePoint, from, Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, outSeconds)));
                yield return null;
            }
            basePos = from;
        }
    }
}
