using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

#pragma warning disable 618

namespace DarkAwakening
{
    /// <summary>
    /// Minimal seated-VR rig (no XRI dependency): head + 2 controllers read through UnityEngine.XR,
    /// pointer ray, trigger, gaze-dwell fallback, fades, "blink" teleports and a desktop (mouse) fallback
    /// so the demo can also be tested in the Editor without a headset.
    /// Hierarchy: DARig (moved by anchors) / TrackingSpace (recenter) / Head(Camera), LeftHand, RightHand
    /// </summary>
    public class DARig : MonoBehaviour
    {
        [Header("Wiring (set by the builder)")]
        public Transform trackingSpace;
        public Transform head;
        public Camera cam;
        public Transform leftHand;
        public Transform rightHand;
        public LineRenderer rayLine;
        public Transform reticle;            // small billboard quad
        public Renderer reticleRenderer;
        public Renderer reticleRing;         // dwell progress ring (scaled)
        public Renderer fadeQuad;            // black quad in front of the eyes
        public Renderer vignetteQuad;        // fear vignette
        public Renderer grainQuad;           // film grain
        public Transform heldRoot;           // follows the pointer pose (lighter / branch are children)

        [Header("Settings")]
        public float dwellSeconds = 2.0f;
        public float eyeHeight = 1.15f;
        public bool allowDwell = true;
        public bool freeTeleport = false;     // click on the floor to walk there (only while the game waits for the player)
        bool teleporting;
        public System.Action OnBlink;
        public float maxRayDistance = 9f;

        // ---- public state ----
        public bool XRActive { get; private set; }
        public bool ControllersTracked { get; private set; }
        public bool TriggerHeld { get; private set; }
        public float PointerSpeed { get; private set; }     // controller / mouse speed (m/s)
        public float ShakeCharge { get; set; }              // grows when the controller is shaken or flicked (throw gesture)
        Vector3 prevPtr; bool havePtr;
        public bool TriggerDown { get; private set; }       // true only on the frame the trigger goes down
        public Ray PointerRay { get; private set; }
        public DAInteractable Hovered { get; private set; }
        public float DwellProgress { get; private set; }
        public Vector3 HeadPos { get { return head.position; } }
        public Vector3 HeadForward { get { return head.forward; } }

        public float Fear { get; set; }                     // 0..1 drives vignette
        public float Fade { get; private set; }             // 0 = clear, 1 = black

        InputAction rPointPos, rPointRot, lPointPos, lPointRot;
        bool prevTrigger;
        bool useRight = true;
        float yaw, pitch;                                   // desktop look
        float fovFearOffset;
        float shakeAmp, shakeT;
        Material fadeMat, vigMat, grainMat, reticleMat, ringMat;
        Vector3 shakeOffset;
        Coroutine moveRoutine;
        bool recenterQueued = true;
        float recenterTimer = 0.6f;
        float noControllerTime;
        DAInteractable dwellTarget;

        void Awake()
        {
            if (cam == null && head != null) cam = head.GetComponent<Camera>();
            rPointPos = new InputAction("rpp", InputActionType.Value, "<XRController>{RightHand}/pointerPosition");
            rPointRot = new InputAction("rpr", InputActionType.Value, "<XRController>{RightHand}/pointerRotation");
            lPointPos = new InputAction("lpp", InputActionType.Value, "<XRController>{LeftHand}/pointerPosition");
            lPointRot = new InputAction("lpr", InputActionType.Value, "<XRController>{LeftHand}/pointerRotation");
            rPointPos.Enable(); rPointRot.Enable(); lPointPos.Enable(); lPointRot.Enable();

            if (fadeQuad != null) fadeMat = fadeQuad.material;
            if (vignetteQuad != null) vigMat = vignetteQuad.material;
            if (grainQuad != null) { grainQuad.gameObject.SetActive(false); }   // film-grain overlay removed (looked like TV static)
            if (reticleRenderer != null) reticleMat = reticleRenderer.material;
            if (reticleRing != null) ringMat = reticleRing.material;
            SetFade(1f);
        }

        void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        void OnDestroy()
        {
            if (rPointPos != null) rPointPos.Dispose();
            if (rPointRot != null) rPointRot.Dispose();
            if (lPointPos != null) lPointPos.Dispose();
            if (lPointRot != null) lPointRot.Dispose();
        }

        // ------------------------------------------------------------------ per-frame
        void Update()
        {
            ReadHeadAndHands();
            ReadTrigger();
            UpdatePointer();
            UpdateOverlays();
            UpdateShake();
            if (recenterQueued)
            {
                recenterTimer -= Time.deltaTime;
                if (recenterTimer <= 0f) { recenterQueued = false; Recenter(true); }
            }
        }

        void OnBeforeRender()
        {
            // re-apply the very latest head pose just before rendering (low latency)
            if (XRActive) ApplyHeadPose();
        }

        void ApplyHeadPose()
        {
            InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!hmd.isValid) return;
            Vector3 p; Quaternion r;
            if (!hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out p))
                hmd.TryGetFeatureValue(CommonUsages.devicePosition, out p);
            if (!hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out r))
                hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out r);
            head.localPosition = p + shakeOffset;
            head.localRotation = r;
        }

        void ReadHeadAndHands()
        {
            InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            XRActive = hmd.isValid;
            if (XRActive)
            {
                ApplyHeadPose();
            }
            else
            {
                // desktop fallback: hold right mouse button to look around
                if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                {
                    Vector2 d = Mouse.current.delta.ReadValue();
                    yaw += d.x * 0.12f;
                    pitch = Mathf.Clamp(pitch - d.y * 0.12f, -80f, 80f);
                }
                head.localPosition = new Vector3(0f, 0f, 0f) + shakeOffset;
                head.localRotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            bool lt = UpdateHand(XRNode.LeftHand, leftHand, lPointPos, lPointRot);
            bool rt = UpdateHand(XRNode.RightHand, rightHand, rPointPos, rPointRot);
            ControllersTracked = lt || rt;
            if (XRActive && !ControllersTracked) noControllerTime += Time.deltaTime; else noControllerTime = 0f;
        }

        bool UpdateHand(XRNode node, Transform t, InputAction posAct, InputAction rotAct)
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            bool tracked = false;
            if (d.isValid) d.TryGetFeatureValue(CommonUsages.isTracked, out tracked);
            if (t != null) t.gameObject.SetActive(tracked);
            if (!tracked || t == null) return false;
            Vector3 p; Quaternion r;
            Vector3 pp = posAct.ReadValue<Vector3>();
            Quaternion pr = rotAct.ReadValue<Quaternion>();
            bool pointerOk = (pr.x * pr.x + pr.y * pr.y + pr.z * pr.z + pr.w * pr.w) > 0.5f;
            if (pointerOk)
            {
                t.localPosition = pp;
                t.localRotation = pr;
            }
            else
            {
                if (d.TryGetFeatureValue(CommonUsages.devicePosition, out p)) t.localPosition = p;
                if (d.TryGetFeatureValue(CommonUsages.deviceRotation, out r)) t.localRotation = r;
            }
            return true;
        }

        void ReadTrigger()
        {
            bool now = false;
            bool anyRight = false, anyLeft = false;
            if (XRActive)
            {
                anyRight = ButtonPressed(XRNode.RightHand);
                anyLeft = ButtonPressed(XRNode.LeftHand);
                if (anyRight) useRight = true; else if (anyLeft) useRight = false;
                now = anyRight || anyLeft;
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) now = true;
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) now = true;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) now = true;
            TriggerDown = now && !prevTrigger;
            TriggerHeld = now;
            prevTrigger = now;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Recenter(true);
        }

        static bool ButtonPressed(XRNode node)
        {
            InputDevice d = InputDevices.GetDeviceAtXRNode(node);
            if (!d.isValid) return false;
            bool b;
            if (d.TryGetFeatureValue(CommonUsages.triggerButton, out b) && b) return true;
            if (d.TryGetFeatureValue(CommonUsages.primaryButton, out b) && b) return true;
            return false;
        }

        // ------------------------------------------------------------------ pointer
        void UpdatePointer()
        {
            Ray ray;
            bool gazeMode = false;
            if (XRActive && ControllersTracked)
            {
                Transform h = (useRight && rightHand != null && rightHand.gameObject.activeSelf) ? rightHand :
                              (leftHand != null && leftHand.gameObject.activeSelf ? leftHand : rightHand);
                ray = new Ray(h.position, h.forward);
            }
            else if (XRActive)
            {
                // no controllers (hand tracking only): gaze pointer
                ray = new Ray(head.position, head.forward);
                gazeMode = true;
            }
            else
            {
                Vector2 mp = Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                ray = cam.ScreenPointToRay(new Vector3(mp.x, mp.y, 0f));
            }
            PointerRay = ray;

            // throw gesture: speed of the controller (m/s) or of the mouse (flick), accumulated into ShakeCharge
            {
                float dtp = Mathf.Max(Time.deltaTime, 0.0001f);
                float spd = 0f;
                if (XRActive && ControllersTracked)
                {
                    if (havePtr) spd = (ray.origin - prevPtr).magnitude / dtp;
                    prevPtr = ray.origin; havePtr = true;
                    if (spd > 12f) spd = 0f;                       // teleport / recenter jump, not a gesture
                }
                else
                {
                    havePtr = false;
                    if (!XRActive && Mouse.current != null) spd = Mouse.current.delta.ReadValue().magnitude / dtp / 450f;
                }
                PointerSpeed = Mathf.Lerp(PointerSpeed, spd, 0.5f);
                ShakeCharge = Mathf.Clamp(ShakeCharge + Mathf.Max(0f, PointerSpeed - 1.3f) * dtp - 0.5f * dtp, 0f, 2f);
            }

            RaycastHit hit;
            DAInteractable found = null;
            Vector3 endPoint = ray.origin + ray.direction * 3f;
            if (Physics.Raycast(ray, out hit, maxRayDistance, ~0, QueryTriggerInteraction.Collide))
            {
                endPoint = hit.point;
                found = hit.collider.GetComponentInParent<DAInteractable>();
                if (found != null && !found.interactable) found = null;
            }

            if (found != Hovered)
            {
                if (Hovered != null) Hovered.SetHovered(false);
                Hovered = found;
                if (Hovered != null) Hovered.SetHovered(true);
                DwellProgress = 0f;
            }

            // activation: trigger or dwell
            if (Hovered != null)
            {
                if (TriggerDown) { DwellProgress = 0f; Hovered.Activate(); }
                else if (allowDwell && Hovered.useDwell && XRActive)
                {
                    // dwell only counts when the pointer is the head (gaze) or when the user stays still on the target
                    bool dwellOk = gazeMode || (Vector3.Angle(head.forward, ray.direction) < 12f);
                    if (dwellOk)
                    {
                        DwellProgress += Time.deltaTime / dwellSeconds;
                        if (DwellProgress >= 1f) { DwellProgress = 0f; Hovered.Activate(); }
                    }
                    else DwellProgress = 0f;
                }
            }
            else
            {
                DwellProgress = 0f;
                if (freeTeleport && TriggerDown && !teleporting && Fade < 0.05f)
                {
                    float dy = ray.direction.y;
                    if (dy < -0.02f)
                    {
                        float tt = (0f - ray.origin.y) / dy;
                        Vector3 gp = ray.origin + ray.direction * tt;
                        gp.x = Mathf.Clamp(gp.x, -2.1f, 2.0f);
                        gp.z = Mathf.Clamp(gp.z, -1.95f, 2.0f);
                        gp.y = 0f;
                        StartCoroutine(FreeBlink(gp));
                    }
                }
            }

            // visuals
            if (rayLine != null)
            {
                bool showRay = XRActive && ControllersTracked;
                rayLine.enabled = showRay;
                if (showRay)
                {
                    rayLine.SetPosition(0, ray.origin + ray.direction * 0.03f);
                    rayLine.SetPosition(1, endPoint);
                }
            }
            if (reticle != null)
            {
                bool showRet = true;
                reticle.gameObject.SetActive(showRet);
                Vector3 p = (Hovered != null || (XRActive && ControllersTracked)) ? endPoint : (head.position + head.forward * 2.2f);
                if (!XRActive) p = endPoint;
                float dist = Vector3.Distance(head.position, p);
                reticle.position = p;
                reticle.rotation = Quaternion.LookRotation(p - head.position);
                float s = Mathf.Clamp(dist * 0.012f, 0.012f, 0.06f) * (Hovered != null ? 1.5f : 1f);
                reticle.localScale = Vector3.one * s;
                if (reticleMat != null)
                    reticleMat.color = Hovered != null ? new Color(1f, 0.85f, 0.55f, 0.95f) : new Color(1f, 1f, 1f, 0.35f);
                if (reticleRing != null)
                {
                    reticleRing.enabled = DwellProgress > 0.01f;
                    reticleRing.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 2.6f, DwellProgress);
                }
            }

            if (heldRoot != null)
            {
                if (XRActive && ControllersTracked)
                {
                    heldRoot.position = ray.origin;
                    heldRoot.rotation = Quaternion.LookRotation(ray.direction, Vector3.up);
                }
                else
                {
                    Vector3 o = head.position + head.right * 0.22f - head.up * 0.2f + head.forward * 0.45f;
                    Vector3 dir = (endPoint - o);
                    if (dir.sqrMagnitude < 0.01f) dir = head.forward;
                    heldRoot.position = o;
                    heldRoot.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                }
            }
        }

        /// <summary>True when the pointer is aimed at the given collider (or one of its children)</summary>
        public bool IsAimingAt(Collider col, out float angleDeg)
        {
            angleDeg = 180f;
            if (col == null) return false;
            RaycastHit hit;
            if (Physics.Raycast(PointerRay, out hit, maxRayDistance, ~0, QueryTriggerInteraction.Collide))
            {
                Vector3 to = (col.bounds.center - PointerRay.origin).normalized;
                angleDeg = Vector3.Angle(PointerRay.direction, to);
                return hit.collider == col || hit.collider.transform.IsChildOf(col.transform);
            }
            return false;
        }

        /// <summary>Hold-to-use: trigger held, or (no controller) gaze held on target.</summary>
        public bool UseHeld
        {
            get { return TriggerHeld || (XRActive && !ControllersTracked); }
        }

        // ------------------------------------------------------------------ overlays
        void UpdateOverlays()
        {
            if (vigMat != null)
            {
                float a = Mathf.Clamp01(0.30f + Fear * 0.65f);
                Color c = new Color(0.02f, 0f, 0f, a);
                vigMat.color = c;
                float sc = Mathf.Lerp(1.0f, 0.55f, Fear);
                vignetteQuad.transform.localScale = new Vector3(sc * 0.9f, sc * 0.9f, 1f) * vigBaseScale;
            }
            if (grainMat != null && Time.frameCount % 2 == 0)
            {
                grainMat.mainTextureOffset = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
            }
        }

        public float vigBaseScale = 1f;

        public void SetFade(float v)
        {
            Fade = Mathf.Clamp01(v);
            if (fadeMat != null)
            {
                Color c = fadeMat.color; c.a = Fade; fadeMat.color = c;
                fadeQuad.enabled = Fade > 0.001f;
            }
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            float start = Fade;
            float t = 0f;
            if (seconds <= 0f) { SetFade(target); yield break; }
            while (t < seconds)
            {
                t += Time.deltaTime;
                SetFade(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetFade(target);
        }

        // ------------------------------------------------------------------ movement
        public void SnapTo(Transform anchor, float eye)
        {
            transform.position = new Vector3(anchor.position.x, anchor.position.y + eye, anchor.position.z);
            transform.rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);
            Recenter(false);
        }

        /// <summary>Comfortable "blink" teleport to an anchor (fade out, move, fade in).</summary>
        public IEnumerator BlinkTo(Transform anchor, float eye)
        {
            if (OnBlink != null) OnBlink();
            Vector3 from = transform.position;
            Vector3 to = new Vector3(anchor.position.x, anchor.position.y + eye, anchor.position.z);
            float dist = Vector3.Distance(from, to);
            if (dist > 7f || Fade > 0.4f)
            {
                yield return FadeTo(1f, 0.28f);
                SnapTo(anchor, eye);
                yield return new WaitForSeconds(0.12f);
                yield return FadeTo(0f, 0.32f);
                yield break;
            }
            // quick comfortable "dash": fast eased glide with a dark pulse, a small dip, and the turn hidden at the darkest moment
            float dur = Mathf.Clamp(0.18f + 0.06f * dist, 0.24f, 0.45f);
            float t = 0f;
            bool turned = false;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float e = k * k * (3f - 2f * k);
                Vector3 p = Vector3.Lerp(from, to, e);
                p.y -= Mathf.Sin(k * Mathf.PI) * 0.04f;
                transform.position = p;
                SetFade(0.82f * Mathf.Sin(k * Mathf.PI));
                if (!turned && k >= 0.5f) { transform.rotation = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f); turned = true; }
                yield return null;
            }
            SnapTo(anchor, eye);
            SetFade(0f);
        }

        IEnumerator FreeBlink(Vector3 ground)
        {
            teleporting = true;
            GameObject tmp = new GameObject("tmpAnchor");
            tmp.transform.position = ground;
            tmp.transform.rotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            yield return BlinkTo(tmp.transform, eyeHeight);
            Destroy(tmp);
            teleporting = false;
        }

        public IEnumerator RaiseEye(float fromY, float toY, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                Vector3 p = transform.position;
                p.y = Mathf.Lerp(fromY, toY, Mathf.SmoothStep(0f, 1f, t / seconds));
                transform.position = p;
                yield return null;
            }
        }

        public void Recenter(bool full = true)
        {
            // full: also put the tracking origin at the HMD ("device" origin, ideal for seated play) and reset yaw
            if (XRActive)
            {
                if (full)
                try
                {
                    List<XRInputSubsystem> subs = new List<XRInputSubsystem>();
                    SubsystemManager.GetSubsystems(subs);
                    for (int i = 0; i < subs.Count; i++)
                    {
                        subs[i].TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
                        subs[i].TryRecenter();
                    }
                }
                catch (Exception) { }
                InputDevice hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
                Vector3 p; Quaternion r;
                if (hmd.isValid)
                {
                    if (!hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out p)) hmd.TryGetFeatureValue(CommonUsages.devicePosition, out p);
                    if (!hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out r)) hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out r);
                    if (full) trackingSpace.localRotation = Quaternion.Euler(0f, -r.eulerAngles.y, 0f);
                    Quaternion q = trackingSpace.localRotation;
                    trackingSpace.localPosition = -(q * p);
                }
            }
            else
            {
                trackingSpace.localPosition = Vector3.zero;
                trackingSpace.localRotation = Quaternion.identity;
                yaw = 0f; pitch = 0f;
            }
        }

        // ------------------------------------------------------------------ juice
        public void Shake(float amplitude, float seconds)
        {
            shakeAmp = amplitude;
            shakeT = seconds;
        }

        void UpdateShake()
        {
            if (shakeT > 0f)
            {
                shakeT -= Time.deltaTime;
                shakeOffset = UnityEngine.Random.insideUnitSphere * shakeAmp * Mathf.Clamp01(shakeT * 2f);
            }
            else shakeOffset = Vector3.zero;
        }

        public IEnumerator WaitForTrigger()
        {
            yield return null;
            while (!TriggerDown) yield return null;
        }
    }
}
