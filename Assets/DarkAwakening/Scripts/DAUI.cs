using System;
using System.Collections;
using UnityEngine;

namespace DarkAwakening
{
    /// <summary>World-space UI for VR: lazy-follow subtitles, hint line, big centre text, 2-button choice panel.</summary>
    public class DAUI : MonoBehaviour
    {
        public DARig rig;

        [Header("Subtitles (lazy follow)")]
        public Transform subRoot;
        public TMPro.TextMeshPro subtitle;
        public Renderer subtitleBg;
        public TMPro.TextMeshPro hint;
        public Renderer hintBg;
        public float subDistance = 1.55f;
        public float subDrop = 0.36f;

        [Header("Centre panel (title, end cards, choice)")]
        public Transform centerRoot;
        public TMPro.TextMeshPro center;
        public Renderer centerBg;
        public DAInteractable choiceA;
        public DAInteractable choiceB;
        public TMPro.TextMeshPro choiceAText;
        public TMPro.TextMeshPro choiceBText;
        public TMPro.TextMeshPro choiceQuestion;
        public Transform choiceRoot;

        float lagYaw;
        float subAlpha, subTarget;
        float hintAlpha, hintTarget;
        float centerAlpha, centerTarget;
        Material subBgMat, hintBgMat, centerBgMat;
        Action<int> choiceCallback;
        readonly System.Collections.Generic.Dictionary<Renderer, Color> baseColors = new System.Collections.Generic.Dictionary<Renderer, Color>();
        bool inited;

        public const string FriendColor = "#9FD3B0";
        public const string FriendName = "Mali";

        [Tooltip("Global multiplier for every text size (lower = smaller text)")]
        public float textScale = 0.2f;
        public float centerScale = 0.3f;

        void Awake()
        {
            if (subtitle != null) subtitle.fontSize *= textScale;
            if (hint != null) hint.fontSize *= textScale;
            if (center != null) center.fontSize *= centerScale;
            if (choiceQuestion != null) choiceQuestion.fontSize *= textScale;
            if (choiceAText != null) choiceAText.fontSize *= textScale;
            if (choiceBText != null) choiceBText.fontSize *= textScale;
            if (subtitleBg != null) subBgMat = subtitleBg.material;
            if (hintBg != null) hintBgMat = hintBg.material;
            if (centerBg != null) centerBgMat = centerBg.material;
        }

        void Start()
        {
            if (choiceA != null) choiceA.Activated += OnChoiceA;
            if (choiceB != null) choiceB.Activated += OnChoiceB;
            if (choiceRoot != null) choiceRoot.gameObject.SetActive(false);
            ApplyAlphas();
        }

        void OnChoiceA(DAInteractable i) { Pick(0); }
        void OnChoiceB(DAInteractable i) { Pick(1); }

        void Pick(int idx)
        {
            if (choiceCallback == null) return;
            Action<int> cb = choiceCallback;
            choiceCallback = null;
            if (choiceRoot != null) choiceRoot.gameObject.SetActive(false);
            cb(idx);
        }

        void LateUpdate()
        {
            if (rig == null || rig.head == null) return;
            Transform h = rig.head;
            float yaw = h.eulerAngles.y;
            if (!inited) { lagYaw = yaw; inited = true; }
            float d = Mathf.DeltaAngle(lagYaw, yaw);
            if (Mathf.Abs(d) > 18f) lagYaw += d * (1f - Mathf.Exp(-2.6f * Time.deltaTime));

            if (subRoot != null)
            {
                Vector3 fwd = Quaternion.Euler(0f, lagYaw, 0f) * Vector3.forward;
                Vector3 p = h.position + fwd * subDistance;
                p.y = h.position.y - subDrop;
                subRoot.position = Vector3.Lerp(subRoot.position, p, 1f - Mathf.Exp(-9f * Time.deltaTime));
                Vector3 look = subRoot.position - h.position;
                look.y *= 0.3f;
                if (look.sqrMagnitude > 0.0001f) subRoot.rotation = Quaternion.LookRotation(look, Vector3.up);
            }

            subAlpha = Mathf.MoveTowards(subAlpha, subTarget, Time.deltaTime * 4f);
            hintAlpha = Mathf.MoveTowards(hintAlpha, hintTarget, Time.deltaTime * 3f);
            centerAlpha = Mathf.MoveTowards(centerAlpha, centerTarget, Time.deltaTime * 1.5f);
            ApplyAlphas();

            // choice button hover feedback
            if (choiceRoot != null && choiceRoot.gameObject.activeSelf)
            {
                Hover(choiceA);
                Hover(choiceB);
            }
        }

        void Hover(DAInteractable b)
        {
            if (b == null) return;
            bool hov = rig.Hovered == b;
            float target = hov ? 1.08f : 1f;
            b.transform.localScale = Vector3.Lerp(b.transform.localScale, new Vector3(target, target, target), 1f - Mathf.Exp(-12f * Time.deltaTime));
            Transform fill = b.transform.Find("Fill");
            if (fill != null)
            {
                Renderer fr = fill.GetComponent<Renderer>();
                if (fr != null)
                {
                    if (!baseColors.ContainsKey(fr)) baseColors[fr] = fr.material.color;
                    Color bc = baseColors[fr];
                    Color hc = new Color(Mathf.Min(1f, bc.r * 1.9f + 0.12f), Mathf.Min(1f, bc.g * 1.9f + 0.12f), Mathf.Min(1f, bc.b * 1.9f + 0.12f), 1f);
                    fr.material.color = Color.Lerp(fr.material.color, hov ? hc : bc, 1f - Mathf.Exp(-14f * Time.deltaTime));
                }
            }
        }

        void ApplyAlphas()
        {
            if (subtitle != null) subtitle.alpha = subAlpha;
            if (subBgMat != null) { Color c = subBgMat.color; c.a = 0.62f * subAlpha; subBgMat.color = c; }
            if (hint != null) hint.alpha = hintAlpha;
            if (hintBgMat != null) { Color c = hintBgMat.color; c.a = 0.5f * hintAlpha; hintBgMat.color = c; }
            if (center != null) center.alpha = centerAlpha;
            if (centerBgMat != null) { Color c = centerBgMat.color; c.a = 0.0f * centerAlpha; centerBgMat.color = c; }
            if (subtitleBg != null) subtitleBg.enabled = subAlpha > 0.01f;
            if (hintBg != null) hintBg.enabled = hintAlpha > 0.01f;
            if (centerBg != null) centerBg.enabled = false;
        }

        // ------------------------------------------------------------------ subtitles
        public void Say(string speaker, string text)
        {
            string s = text;
            if (!string.IsNullOrEmpty(speaker))
            {
                string col = speaker == FriendName ? FriendColor : "#E8C77A";
                s = "<color=" + col + "><b>" + speaker + "</b></color>  " + text;
            }
            subtitle.text = s;
            subtitle.ForceMeshUpdate();
            float w = Mathf.Min(1.7f, Mathf.Max(0.5f, subtitle.preferredWidth));
            float hgt = Mathf.Max(0.07f, subtitle.preferredHeight);
            if (subtitleBg != null)
            {
                subtitleBg.transform.localScale = new Vector3(w + 0.12f, hgt + 0.07f, 1f);
            }
            subTarget = 1f;
        }

        public void ClearSay() { subTarget = 0f; }

        public IEnumerator SayFor(string speaker, string text, float seconds)
        {
            Say(speaker, text);
            yield return new WaitForSeconds(seconds);
            ClearSay();
            yield return new WaitForSeconds(0.35f);
        }

        public void Hint(string text)
        {
            hint.text = text;
            hint.ForceMeshUpdate();
            float w = Mathf.Min(1.7f, Mathf.Max(0.4f, hint.preferredWidth));
            float hgt = Mathf.Max(0.05f, hint.preferredHeight);
            if (hintBg != null) hintBg.transform.localScale = new Vector3(w + 0.1f, hgt + 0.05f, 1f);
            hintTarget = 1f;
        }

        public void ClearHint() { hintTarget = 0f; }

        // ------------------------------------------------------------------ centre panel
        public void ShowCenter(string text, float distance = 2.0f)
        {
            if (rig != null && rig.head != null && centerRoot != null)
            {
                Vector3 fwd = rig.head.forward; fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                fwd.Normalize();
                centerRoot.position = rig.head.position + fwd * distance;
                centerRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
            center.text = text;
            centerTarget = 1f;
        }

        public void HideCenter() { centerTarget = 0f; }

        public bool CenterVisible { get { return centerAlpha > 0.05f; } }

        public void ShowChoice(string question, string a, string b, Action<int> callback)
        {
            if (choiceRoot == null) return;
            Vector3 fwd = rig.head.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            choiceRoot.position = rig.head.position + fwd * 1.35f + Vector3.down * 0.02f;
            choiceRoot.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            choiceQuestion.text = question;
            choiceAText.text = a;
            choiceBText.text = b;
            choiceCallback = callback;
            choiceRoot.gameObject.SetActive(true);
        }
    }
}
