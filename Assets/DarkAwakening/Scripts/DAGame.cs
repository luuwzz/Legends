using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkAwakening
{
    /// <summary>
    /// "The Dark Awakening" - 3 to 5 minute VR demo director.
    /// Intro (afternoon, photo) -> Phase 1 (wake up, window, Krasue) -> Phase 2 (find lighter + thorns,
    /// repel her at the door and wall, hide) -> Dawn (choice) -> Conclusion (text per choice).
    /// </summary>
    public class DAGame : MonoBehaviour
    {
        [Header("Systems")]
        public DARig rig;
        public DAUI ui;
        public DAAudio sfx;

        [Header("Anchors (floor level; the rig adds eye height)")]
        public Transform aIntro;
        public Transform aBed;
        public Transform aWindow;
        public Transform aTable;
        public Transform aDoor;
        public Transform aHide;

        [Header("Scene sets")]
        public GameObject introSet;
        public GameObject houseSet;
        public GameObject outdoorSet;
        public Renderer photoRenderer;
        public Renderer krasueFaceRenderer;   // close-up portrait of her face, shown next to the photo

        [Header("Moving parts")]
        public Transform shutterL;
        public Transform shutterR;
        public Transform doorPivot;

        [Header("Interactables")]
        public DAInteractable windowMark;
        public DAInteractable shutterPick;
        public DAInteractable lighterPick;
        public DAInteractable branchPick;
        public DAInteractable hidePick;
        public DAInteractable hideWestPick;
        public DAInteractable hideKitPick;
        public Transform aAltar, aKitchenBranch, aHideWest, aHideKit;
        public DARepelZone westZone, kitchenZone;

        [Header("Lights")]
        public Light moon;
        public Light sun;
        public Light moonShaft;
        public DALightFlicker candleFlicker;
        public Light[] dawnLeaks;

        [Header("Krasue")]
        public DAKrasue krasue;
        public Transform krasueFar;
        public Transform krasueMid;
        public Transform krasueWindow;
        public DARepelZone doorZone;
        public DARepelZone wallZone;
        public DARepelZone windowZone;
        public Transform aConfront;

        [Header("Held items (children of rig.heldRoot)")]
        public GameObject heldLighter;
        public DALightFlicker heldFlame;
        public GameObject heldBranch;

        [Header("Sky")]
        public Material skyMat;

        // -------------------------------------------------------------- state
        class Mood
        {
            public Color ambient, fog, sky, moonColor;
            public float fogDensity, moonIntensity, exposure, sunIntensity, leak;
        }

        static Mood MakeMood(Color amb, Color fog, float dens, Color sky, float exposure, Color moonCol, float moonI, float sunI, float leak)
        {
            Mood m = new Mood();
            m.ambient = amb; m.fog = fog; m.fogDensity = dens; m.sky = sky; m.exposure = exposure;
            m.moonColor = moonCol; m.moonIntensity = moonI; m.sunIntensity = sunI; m.leak = leak;
            return m;
        }

        Mood moodBlack, moodNight, moodDark, moodDawn;
        Mood current;

        bool hasLighter, hasBranch, heldVisible;
        enum HoldMode { Fire, Thorns }
        HoldMode holdMode = HoldMode.Fire;
        bool lighterLit, branchRaised;
        float fearTarget;
        bool waitOk;
        int choice = -1;
        bool scaring;
        bool pickBusy;
        bool gotLighter, gotBranch;

        const string Friend = "Chai";
        string Girl { get { return DAUI.FriendName; } }

        // -------------------------------------------------------------- lifecycle
        void Awake()
        {
            moodBlack = MakeMood(Color.black, Color.black, 0.03f, Color.black, 0f, Color.black, 0f, 0f, 0f);
            moodNight = MakeMood(new Color(0.26f, 0.31f, 0.44f), new Color(0.10f, 0.14f, 0.21f), 0.015f,
                                 new Color(0.09f, 0.13f, 0.26f), 1.0f, new Color(0.62f, 0.74f, 1f), 1.25f, 0f, 0f);
            moodDark = MakeMood(new Color(0.14f, 0.18f, 0.28f), new Color(0.07f, 0.10f, 0.16f), 0.019f,
                                new Color(0.06f, 0.09f, 0.2f), 1.0f, new Color(0.55f, 0.68f, 1f), 0.9f, 0f, 0f);
            moodDawn = MakeMood(new Color(0.40f, 0.33f, 0.30f), new Color(0.72f, 0.50f, 0.40f), 0.016f,
                                new Color(1f, 0.62f, 0.42f), 1.5f, new Color(0.7f, 0.75f, 1f), 0f, 1.15f, 2.2f);
            current = moodBlack;
        }

        void Start()
        {
            if (rig == null) rig = FindAnyObjectByType<DARig>();
            if (rig != null) rig.OnBlink = delegate { if (sfx != null && sfx.footsteps != null) sfx.Play2D(sfx.footsteps, 0.55f); };
            if (ui == null) ui = FindAnyObjectByType<DAUI>();
            if (sfx == null) sfx = FindAnyObjectByType<DAAudio>();
            StartCoroutine(Run());
        }

        void Update()
        {
            // held items
            bool lit = false, raised = false;
            if (heldVisible && rig != null)
            {
                if (holdMode == HoldMode.Fire) lit = hasLighter && rig.UseHeld;
                else raised = hasBranch && rig.UseHeld;
            }
            if (lit != lighterLit)
            {
                lighterLit = lit;
                if (lit) sfx.Play2D(sfx.lighter, 0.7f);
            }
            branchRaised = raised;
            if (heldLighter != null) heldLighter.SetActive(hasLighter && heldVisible);
            if (heldFlame != null) heldFlame.on = lighterLit;
            sfx.Bed2D("flame", sfx.flame, lighterLit ? 0.35f : 0f, 0.2f);
            if (heldBranch != null)
            {
                heldBranch.SetActive(hasBranch && heldVisible);
                Vector3 pLow = new Vector3(0.10f, -0.16f, 0.12f);
                Vector3 pHigh = new Vector3(0.0f, -0.02f, 0.06f);
                Quaternion rLow = Quaternion.Euler(10f, 0f, 8f);
                Quaternion rHigh = Quaternion.Euler(-90f, 0f, 0f);
                float k = branchRaised ? 1f : 0f;
                Transform t = heldBranch.transform;
                t.localPosition = Vector3.Lerp(t.localPosition, Vector3.Lerp(pLow, pHigh, k), 1f - Mathf.Exp(-9f * Time.deltaTime));
                t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Slerp(rLow, rHigh, k), 1f - Mathf.Exp(-9f * Time.deltaTime));
            }

            // fear / heartbeat
            if (rig != null)
            {
                rig.Fear = Mathf.MoveTowards(rig.Fear, fearTarget, Time.deltaTime * 0.35f);
                if (rig.Fear > 0.03f)
                    sfx.Bed2D("heart", sfx.heartbeat, Mathf.Lerp(0.15f, 0.95f, rig.Fear), 0.4f, Mathf.Lerp(0.9f, 1.55f, rig.Fear));
                else
                    sfx.StopBed("heart", 1f);
            }
        }

        // -------------------------------------------------------------- helpers
        static float Dur(string s) { return Mathf.Clamp(1.1f + s.Length * 0.040f, 2.0f, 5.5f); }

        IEnumerator Line(string who, string text)
        {
            yield return ui.SayFor(who, text, Dur(text));
        }

        IEnumerator Wait(float s) { yield return new WaitForSeconds(s); }

        IEnumerator WaitFor(DAInteractable i, float timeout)
        {
            bool done = false;
            Action<DAInteractable> h = delegate (DAInteractable x) { done = true; };
            i.Activated += h;
            float t = 0f;
            rig.freeTeleport = true;
            while (!done && t < timeout) { t += Time.deltaTime; yield return null; }
            rig.freeTeleport = false;
            i.Activated -= h;
            waitOk = done;
        }

        void ApplyMood(Mood m)
        {
            current = m;
            RenderSettings.ambientLight = m.ambient;
            RenderSettings.fogColor = m.fog;
            RenderSettings.fogDensity = m.fogDensity;
            if (moon != null) { moon.color = m.moonColor; moon.intensity = m.moonIntensity; }
            if (sun != null) sun.intensity = m.sunIntensity;
            if (moonShaft != null) moonShaft.intensity = m.moonIntensity * 5f;
            if (skyMat != null)
            {
                skyMat.SetColor("_Tint", m.sky);
                skyMat.SetFloat("_Exposure", m.exposure);
            }
            if (dawnLeaks != null)
                for (int i = 0; i < dawnLeaks.Length; i++) if (dawnLeaks[i] != null) { dawnLeaks[i].intensity = m.leak; dawnLeaks[i].enabled = m.leak > 0.01f; }
        }

        IEnumerator MoodTo(Mood target, float secs)
        {
            Mood a = new Mood();
            a.ambient = current.ambient; a.fog = current.fog; a.sky = current.sky; a.moonColor = current.moonColor;
            a.fogDensity = current.fogDensity; a.moonIntensity = current.moonIntensity; a.exposure = current.exposure;
            a.sunIntensity = current.sunIntensity; a.leak = current.leak;
            float t = 0f;
            while (t < secs)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, secs));
                Mood m = new Mood();
                m.ambient = Color.Lerp(a.ambient, target.ambient, k);
                m.fog = Color.Lerp(a.fog, target.fog, k);
                m.sky = Color.Lerp(a.sky, target.sky, k);
                m.moonColor = Color.Lerp(a.moonColor, target.moonColor, k);
                m.fogDensity = Mathf.Lerp(a.fogDensity, target.fogDensity, k);
                m.moonIntensity = Mathf.Lerp(a.moonIntensity, target.moonIntensity, k);
                m.exposure = Mathf.Lerp(a.exposure, target.exposure, k);
                m.sunIntensity = Mathf.Lerp(a.sunIntensity, target.sunIntensity, k);
                m.leak = Mathf.Lerp(a.leak, target.leak, k);
                ApplyMood(m);
                yield return null;
            }
            ApplyMood(target);
        }

        void SetInteractables(bool on)
        {
            DAInteractable[] all = { windowMark, shutterPick, lighterPick, branchPick, hidePick, hideWestPick, hideKitPick };
            for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].SetInteractable(on);
        }

        IEnumerator SetShutters(float openDeg, float secs, bool slam)
        {
            float from = ShutterAngle;
            float t = 0f;
            while (t < secs)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.01f, secs));
                ShutterAngle = Mathf.Lerp(from, openDeg, k);
                yield return null;
            }
            ShutterAngle = openDeg;
            if (slam)
            {
                Vector3 p = shutterL != null ? shutterL.position : Vector3.zero;
                sfx.Play3D(sfx.slam, p, 1f);
            }
        }

        float shutterAngle;
        float ShutterAngle
        {
            get { return shutterAngle; }
            set
            {
                shutterAngle = value;
                if (shutterL != null) shutterL.localRotation = Quaternion.Euler(0f, value, 0f);
                if (shutterR != null) shutterR.localRotation = Quaternion.Euler(0f, -value, 0f);
            }
        }

        IEnumerator ShakeDoor(float seconds, float amp)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float decay = 1f - t / seconds;
                if (doorPivot != null) doorPivot.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 55f) * amp * decay, 0f);
                yield return null;
            }
            if (doorPivot != null) doorPivot.localRotation = Quaternion.identity;
        }

        IEnumerator ShakeShutters(float seconds, float amp)
        {
            float t = 0f;
            float baseA = ShutterAngle;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float decay = 1f - t / seconds;
                ShutterAngle = baseA + Mathf.Sin(t * 48f) * amp * decay;
                yield return null;
            }
            ShutterAngle = baseA;
        }

        // -------------------------------------------------------------- main flow
        IEnumerator Run()
        {
            yield return null;
            // initial state: everything dark, only the title
            if (introSet != null) introSet.SetActive(true);
            if (houseSet != null) houseSet.SetActive(false);
            if (outdoorSet != null) outdoorSet.SetActive(false);
            if (krasue != null) krasue.Show(false);
            SetInteractables(false);
            heldVisible = false;
            ShutterAngle = 0f;
            rig.cam.clearFlags = CameraClearFlags.SolidColor;
            rig.cam.backgroundColor = Color.black;
            RenderSettings.fog = false;
            ApplyMood(moodBlack);
            rig.SnapTo(aIntro, rig.eyeHeight);
            rig.SetFade(1f);
            yield return Wait(0.5f);
            yield return rig.FadeTo(0f, 1.2f);

            ui.ShowCenter("<size=150%><b>THE DARK AWAKENING</b></size>\n<size=70%>a VR horror demo  -  about 4 minutes  -  seated</size>\n\n" +
                          "<size=60%><color=#B8B8B8>Contains horror themes and a reference to suicide.\nHeadphones recommended.</color></size>\n\n" +
                          "<size=75%><color=#E8C77A>Pull the trigger to begin</color></size>", 2.2f);
            sfx.Music(sfx.musicIntro, 0.5f, 3f);
            yield return rig.WaitForTrigger();
            ui.HideCenter();
            sfx.Play2D(sfx.click, 0.6f);
            rig.Recenter(true);
            yield return Wait(1.2f);

            yield return Intro();
            yield return Phase1();
            yield return Phase2();
            yield return Ending();
            yield return Conclusion();
        }

        // -------------------------------------------------------------- INTRO
        IEnumerator Intro()
        {
            ui.ShowCenter("<size=110%>The afternoon before</size>", 1.2f);
            sfx.Bed2D("night", sfx.birds, 0.18f, 2f);
            yield return Wait(1.5f);
            ui.HideCenter();
            yield return Wait(0.3f);

            yield return Line(Friend, "Remember what the elders say about the Krasue?");
            yield return Line(Friend, "By day, a woman like any other. By night, her head drifts off, trailing her organs.");
            yield return Line(Friend, "Only two things can hurt her: fire, and the thorns of the jujube tree.");
            yield return Line("You", "Why tell me this?");
            StartCoroutine(PhotoIn());
            yield return Line(Friend, "Look at " + Girl + ". Pale, thin, always alone. She used to laugh so much.");
            rig.Shake(0.01f, 0.4f);
            sfx.Bed2D("heart", sfx.heartbeat, 0.5f, 0.6f, 1.1f);
            yield return Line(Friend, "And last week, three chickens were found torn open behind her house.");
            yield return Line("You", "Stop it. She is our friend.");
            yield return Line(Friend, "I hope I am wrong. Be careful tonight.");

            sfx.Bed2D("heart", sfx.heartbeat, 0f, 0.8f);
            sfx.Music(null, 0f, 1.5f);
            sfx.StopBed("night", 1.5f);
            yield return rig.FadeTo(1f, 0.8f);
            if (photoRenderer != null) photoRenderer.gameObject.SetActive(false);
            ui.ShowCenter("<size=130%>That night...</size>", 1.6f);
            yield return Wait(1.7f);
            ui.HideCenter();
            yield return Wait(0.3f);
        }

        IEnumerator PhotoIn()
        {
            if (photoRenderer == null) yield break;
            Material pm = photoRenderer.material;
            Transform pt = photoRenderer.transform;
            Vector3 baseScale = pt.localScale;
            float t = 0f;
            while (t < 0.7f)
            {
                t += Time.deltaTime;
                Color c = pm.color; c.a = Mathf.SmoothStep(0f, 1f, t / 0.7f); pm.color = c;
                pt.localScale = baseScale * Mathf.Lerp(0.88f, 1f, t / 0.7f);
                yield return null;
            }
            yield return PhotoPush(pt, baseScale, 14f);
        }

        IEnumerator PhotoPush(Transform pt, Vector3 baseScale, float seconds)
        {
            float t = 0f;
            while (t < seconds && pt != null && pt.gameObject.activeInHierarchy)
            {
                t += Time.deltaTime;
                pt.localScale = baseScale * Mathf.Lerp(1f, 1.12f, t / seconds);
                yield return null;
            }
        }

        // -------------------------------------------------------------- PHASE 1
        IEnumerator Phase1()
        {
            if (introSet != null) introSet.SetActive(false);
            houseSet.SetActive(true);
            outdoorSet.SetActive(true);
            rig.cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = skyMat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            ApplyMood(moodBlack);
            ShutterAngle = 6f;                               // ajar
            if (candleFlicker != null) { candleFlicker.on = true; }
            rig.smoothMove = false;
            rig.SnapTo(aBed, 0.85f);                         // lying in bed
            StartCoroutine(MoodTo(moodNight, 6f));
            sfx.Bed2D("wind", sfx.wind, 0.40f, 6f);
            sfx.Bed2D("night", sfx.night, 0.30f, 5f);

            yield return rig.FadeTo(0f, 4.0f);               // waking up
            yield return Line("", "<i>(the wind howls outside)</i>");

            // the shutters burst open, the candle dies
            Vector3 wp = shutterL != null ? shutterL.position : Vector3.zero;
            sfx.Play3D(sfx.windowOpen != null ? sfx.windowOpen : sfx.slam, wp, 1f);
            sfx.Play3D(sfx.slam, wp, 0.6f);
            rig.Shake(0.015f, 0.5f);
            StartCoroutine(SetShutters(100f, 0.55f, false));
            yield return Wait(0.9f);
            if (candleFlicker != null) candleFlicker.on = false;
            StartCoroutine(rig.RaiseEye(aBed.position.y + 0.85f, aBed.position.y + rig.eyeHeight, 3.5f));
            yield return Line("", "<i>Something woke me...</i>");
            yield return Line("", "The window. The wind blew the shutters open.");

            windowMark.SetInteractable(true);
            ui.Hint("Look at the glowing window and pull the trigger (or hold your gaze) to go there");
            yield return WaitFor(windowMark, 45f);
            windowMark.SetInteractable(false);
            ui.ClearHint();
            sfx.Play2D(sfx.click, 0.6f);
            yield return rig.BlinkTo(aWindow, rig.eyeHeight);

            yield return Line("", "Let me close it before it wakes the whole house.");
            shutterPick.SetInteractable(true);
            ui.Hint("Aim at the shutters and pull the trigger to close them");
            yield return WaitFor(shutterPick, 45f);
            shutterPick.SetInteractable(false);
            ui.ClearHint();

            // close halfway... then she appears
            sfx.Play3D(sfx.creak, wp, 0.8f);
            yield return SetShutters(38f, 1.6f, false);
            sfx.Bed2D("wind", sfx.wind, 0.10f, 2.5f);
            yield return Wait(0.8f);

            krasue.Show(true);
            krasue.SetGlow(0.7f);
            krasue.PlaceHead(krasueFar.position, (aWindow.position - krasueFar.position));
            sfx.Play3D(sfx.scream, krasueFar.position, 0.35f, 0.9f, 60f);
            yield return Line("", "<i>(a faint, drawn-out wail)</i>");
            yield return Line("", "...What is that, floating out in the field?");

            fearTarget = 0.45f;
            Coroutine approach = StartCoroutine(krasue.MoveHead(krasueMid.position, 7.5f));
            sfx.Play3D(sfx.whisper, krasueMid.position, 0.6f, 1f, 40f);
            yield return Wait(4.5f);
            // she drifts right up to the window so the player can see her face clearly (the "zoom")
            StopCoroutine(approach);
            Vector3 near = krasueWindow.position + new Vector3(0f, 0f, -0.1f);
            krasue.SetFacing(rig.HeadPos - near, false);
            krasue.SetGlow(1.3f);
            StartCoroutine(krasue.MoveHead(near, 3.0f));
            yield return Wait(3.2f);
            fearTarget = 0.7f;
            StartCoroutine(PhotoFlash(5.5f));
            yield return Line("", "Those eyes... It is " + Girl + ". My friend " + Girl + ".");
            yield return Wait(1.0f);

            // she lunges, the shutters slam shut
            sfx.Play3D(sfx.scream, krasueWindow.position, 1f, 1f, 30f);
            krasue.SetGlow(1.4f);
            StartCoroutine(krasue.MoveHead(krasueWindow.position, 0.55f));
            if (sfx.bite != null) sfx.Play3D(sfx.bite, krasueWindow.position, 1f, 1f, 30f);
            yield return Wait(0.35f);
            StartCoroutine(SetShutters(0f, 0.18f, true));
            rig.Shake(0.06f, 0.9f);
            fearTarget = 1f;
            yield return Wait(0.5f);
            yield return rig.FadeTo(1f, 0.5f);
            krasue.Show(false);
            ShutterAngle = 0f;
        }

        // the old photo flashes at the edge of the view so the player can compare the two faces
        IEnumerator PhotoFlash(float seconds)
        {
            if (photoRenderer == null || rig == null || rig.head == null) yield break;
            Material m1;
            GameObject q1 = MakeCard("PhotoFlash", photoRenderer, new Vector3(-0.20f, 0.10f, 0.85f), new Vector3(0.50f, 0.398f, 1f), 3f, out m1);
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float a = Mathf.Min(Mathf.Clamp01(t / 0.6f), Mathf.Clamp01((seconds - t) / 0.8f));
                if (m1 != null) { Color c = m1.color; c.a = 0.95f * a; m1.color = c; }
                yield return null;
            }
            if (q1 != null) Destroy(q1);
        }

        GameObject MakeCard(string name, Renderer source, Vector3 localPos, Vector3 scale, float roll, out Material mat)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Collider col = q.GetComponent<Collider>();
            if (col != null) Destroy(col);
            q.transform.SetParent(rig.head, false);
            q.transform.localPosition = localPos;
            q.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            q.transform.localScale = scale;
            Renderer r = q.GetComponent<Renderer>();
            mat = new Material(source.sharedMaterial);
            r.material = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q;
        }

        // -------------------------------------------------------------- hiding places (one per room)
        class HidePlace
        {
            public DAInteractable pick; public Transform anchor;
            public Vector3[] spots; public Vector3[] faces; public bool main;
            public Action<DAInteractable> handler;
        }
        List<HidePlace> hidePlaces;
        HidePlace chosenHide;

        void BuildHidePlaces()
        {
            if (hidePlaces != null) return;
            hidePlaces = new List<HidePlace>();
            if (hidePick != null)
            {
                HidePlace bed = new HidePlace();
                bed.pick = hidePick; bed.anchor = aHide; bed.main = true;
                bed.spots = new Vector3[] { doorZone.peekPoint.position, wallZone.peekPoint.position, krasueWindow.position };
                bed.faces = new Vector3[] { doorZone.facing, wallZone.facing, Vector3.back };
                hidePlaces.Add(bed);
            }
            if (hideWestPick != null && westZone != null && aHideWest != null)
            {
                HidePlace w = new HidePlace();
                w.pick = hideWestPick; w.anchor = aHideWest;
                Vector3 p = westZone.peekPoint.position;
                w.spots = new Vector3[] { p, p + new Vector3(0f, 0f, 0.42f), p + new Vector3(0f, 0f, -0.42f) };
                w.faces = new Vector3[] { westZone.facing, westZone.facing, westZone.facing };
                hidePlaces.Add(w);
            }
            if (hideKitPick != null && kitchenZone != null && aHideKit != null)
            {
                HidePlace k = new HidePlace();
                k.pick = hideKitPick; k.anchor = aHideKit;
                Vector3 p = kitchenZone.peekPoint.position;
                k.spots = new Vector3[] { p, p + new Vector3(0.42f, 0f, 0f), p + new Vector3(-0.42f, 0f, 0f) };
                k.faces = new Vector3[] { kitchenZone.facing, kitchenZone.facing, kitchenZone.facing };
                hidePlaces.Add(k);
            }
        }

        // the player picks one of the glowing hiding places (any room); result in chosenHide
        IEnumerator ChooseHide(float timeout)
        {
            BuildHidePlaces();
            chosenHide = null;
            foreach (HidePlace hp in hidePlaces)
            {
                HidePlace cap = hp;
                hp.handler = delegate (DAInteractable x) { if (chosenHide == null) chosenHide = cap; };
                hp.pick.Activated += hp.handler;
                hp.pick.SetInteractable(true);
            }
            rig.freeTeleport = true;
            float t = 0f;
            while (chosenHide == null && t < timeout) { t += Time.deltaTime; yield return null; }
            rig.freeTeleport = false;
            foreach (HidePlace hp in hidePlaces) { hp.pick.Activated -= hp.handler; hp.pick.SetInteractable(false); }
            if (chosenHide == null) chosenHide = hidePlaces[0];
        }

        // -------------------------------------------------------------- PHASE 2
        // storyboard 4-9: candles -> scare her off at the window (not enough) -> hide -> special plant -> she flees at dawn
        IEnumerator Phase2()
        {
            StartCoroutine(MoodTo(moodDark, 3f));
            rig.smoothMove = true;                            // free walking (left stick / WASD) from here on; no teleport back to the bed
            sfx.Bed2D("wind", sfx.wind, 0.22f, 3f);
            sfx.Music(sfx.musicTension, 0.8f, 5f);
            fearTarget = 0.4f;
            gotLighter = false; gotBranch = false; hasLighter = false; hasBranch = false; heldVisible = false;
            yield return Wait(1.2f);
            yield return rig.FadeTo(0f, 2.0f);

            // panel 3: hide and watch the room (behind the bed, behind the rice bin, or in the kitchen nook)
            yield return Line("", "<i>(muffled thudding against the walls)</i>");
            yield return Line("", "She is still out there... I have to hide!");
            ui.Hint("Hide! Pull the trigger on a glowing spot: behind the bed, behind the rice bin (west room) or in the kitchen corner");
            yield return ChooseHide(60f);
            HidePlace hp1 = chosenHide;
            ui.ClearHint();
            yield return rig.BlinkTo(hp1.anchor, 0.80f);
            yield return HideSequence(13f, false, hp1);
            yield return rig.RaiseEye(rig.transform.position.y, aBed.position.y + rig.eyeHeight, 1.2f);   // stand back up, no teleport

            // panel 4: spot the candles in the family altar room and walk there
            yield return Line("", "<i>(silence)</i>");
            yield return Line("", "Is she gone? ... Wait. Something is glowing in the altar room.");
            yield return Line("", "Candles. Fire keeps spirits away, " + Friend + " always said.");
            lighterPick.SetInteractable(true);
            ui.Hint("Walk to the altar room (west doorway) with the LEFT STICK (WASD), then aim at the glowing candle and pull the trigger");
            yield return WaitFor(lighterPick, 90f);
            lighterPick.SetInteractable(false);
            ui.ClearHint();
            // panel 5: pick up the candle
            yield return PickLighter();

            // panel 6: she is back, at the altar-room window - recognise her, try to scare her off with the candle
            yield return Wait(1.2f);
            fearTarget = 0.6f;
            Vector3 wPeek = westZone.peekPoint.position;
            sfx.Play3D(sfx.scratch, wPeek, 1f);
            rig.Shake(0.015f, 0.5f);
            yield return Wait(0.8f);
            yield return Line("", "<i>(something is at the window)</i>");
            krasue.Show(true);
            krasue.SetGlow(1.3f);
            krasue.PlaceHead(wPeek, westZone.facing);
            sfx.Play3D(sfx.scream, wPeek, 1f, 1f, 30f);
            rig.Shake(0.05f, 0.7f);
            fearTarget = 0.85f;
            yield return Line("", "My friend " + Girl + "... Stay back! I have the candle!");
            yield return Repel(westZone, "Aim at her head and HOLD the trigger to hold the candle against her", HoldMode.Fire, true);

            // panel 7: not enough - hide again (any room), then spot the special plant in the kitchen
            yield return Line("", "It is not enough! The flame will not hold her!");
            heldVisible = false;
            ui.Hint("Hide again! Pull the trigger on a glowing spot (behind the bed, behind the rice bin or in the kitchen corner)");
            yield return ChooseHide(60f);
            HidePlace hp2 = chosenHide;
            ui.ClearHint();
            yield return rig.BlinkTo(hp2.anchor, 0.80f);
            yield return HideSequence(9f, false, hp2);
            yield return rig.RaiseEye(rig.transform.position.y, aBed.position.y + rig.eyeHeight, 1.2f);   // stand back up, no teleport

            yield return Line("", "Wait... the plant hanging by the hearth. The jujube thorns!");
            yield return Line("", "Grandmother said spirits fear their thorns.");
            branchPick.SetInteractable(true);
            ui.Hint("Walk to the kitchen (south doorway) with the LEFT STICK (WASD), then aim at the thorn branch by the hearth and pull the trigger");
            yield return WaitFor(branchPick, 90f);
            branchPick.SetInteractable(false);
            ui.ClearHint();
            yield return PickBranch();

            // panel 8: grab it and make her run away - she now comes to the kitchen window
            yield return Wait(1.0f);
            fearTarget = 0.7f;
            Vector3 kPeek = kitchenZone.peekPoint.position;
            sfx.Play3D(sfx.scratch, kPeek, 1f);
            rig.Shake(0.02f, 0.6f);
            yield return Line("", "<i>(claws scraping at the kitchen window)</i>");
            yield return Wait(0.6f);
            krasue.Show(true);
            krasue.SetGlow(1.3f);
            krasue.PlaceHead(kPeek, kitchenZone.facing);
            sfx.Play3D(sfx.scream, kPeek, 1f, 1f, 30f);
            rig.Shake(0.05f, 0.7f);
            heldVisible = true;
            holdMode = HoldMode.Thorns;
            yield return Line("", "She is back! The thorns... hold them up!");
            yield return Repel(kitchenZone, "Aim at her head and HOLD the trigger to push the thorn plant at her", HoldMode.Thorns, false);

            // panel 9: she is gone
            heldVisible = false;
            sfx.Bed2D("wind", sfx.wind, 0f, 3f);
            sfx.Bed2D("night", sfx.night, 0f, 3f);
            sfx.Music(null, 0f, 4f);
            fearTarget = 0.1f;
            yield return Wait(3.0f);
            yield return Line("", "<i>(silence)</i>");
            yield return Line("", "...She is gone.");
        }

        void OnLighterPick(DAInteractable i) { if (!pickBusy && !gotLighter) StartCoroutine(PickLighter()); }
        void OnBranchPick(DAInteractable i) { if (!pickBusy && !gotBranch) StartCoroutine(PickBranch()); }

        void UpdatePickHint()
        {
            if (!gotLighter && !gotBranch) ui.Hint("Find the CANDLE in the altar room and the JUJUBE THORN BRANCH in the kitchen");
            else if (!gotLighter) ui.Hint("Now find the candle in the altar room");
            else if (!gotBranch) ui.Hint("Now take the jujube thorn branch in the kitchen");
        }

        IEnumerator PickLighter()
        {
            pickBusy = true;
            lighterPick.SetInteractable(false);
            ui.ClearHint();
            lighterPick.Consume();
            sfx.Play2D(sfx.pickup, 0.9f);
            gotLighter = true; hasLighter = true; heldVisible = true;
            holdMode = HoldMode.Fire;
            yield return Line("", "A candle. Its light is warm.");
            pickBusy = false;
        }

        IEnumerator PickBranch()
        {
            pickBusy = true;
            branchPick.SetInteractable(false);
            ui.ClearHint();
            branchPick.Consume();
            sfx.Play2D(sfx.pickup, 0.9f);
            gotBranch = true; hasBranch = true;
            holdMode = HoldMode.Thorns;
            heldVisible = true;
            yield return Line("", "Jujube thorns... Please let them work.");
            pickBusy = false;
        }

        IEnumerator AmbientScares()
        {
            yield return new WaitForSeconds(6f);
            int n = 0;
            while (scaring)
            {
                n++;
                int pick = n % 4;
                if (pick == 1) { sfx.Play3D(sfx.doorBang, doorZone.transform.position, 0.8f); StartCoroutine(ShakeDoor(1.4f, 1.4f)); rig.Shake(0.01f, 0.6f); }
                else if (pick == 2) sfx.Play3D(sfx.scratch, wallZone.transform.position, 0.8f);
                else if (pick == 3) sfx.Play3D(sfx.growl, doorZone.transform.position + Vector3.right, 0.7f);
                else sfx.Play3D(sfx.creak, rig.HeadPos + new Vector3(1.5f, 0f, 1f), 0.7f);
                float w = 0f;
                while (w < 7f && scaring) { w += Time.deltaTime; yield return null; }
            }
        }

        // -------------------------------------------------------------- Repel the Krasue at a gap
        // the held object is thrown at her face (clone flies, the held copy disappears)
        IEnumerator ThrowHeld(HoldMode mode, DARepelZone zone)
        {
            GameObject src = mode == HoldMode.Fire ? heldLighter : heldBranch;
            if (src != null)
            {
                GameObject c = Instantiate(src, src.transform.position, src.transform.rotation);
                c.transform.SetParent(null, true);
                c.transform.localScale = src.transform.lossyScale;
                c.SetActive(true);
                foreach (Light l in c.GetComponentsInChildren<Light>(true)) l.enabled = false;
                foreach (DALightFlicker f in c.GetComponentsInChildren<DALightFlicker>(true)) f.enabled = false;
                heldVisible = false;
                Vector3 p0 = c.transform.position;
                float tt = 0f, dur = 0.28f;
                while (tt < dur)
                {
                    tt += Time.deltaTime;
                    float k = Mathf.Clamp01(tt / dur);
                    Vector3 p1 = krasue.HeadPosition;
                    c.transform.position = Vector3.Lerp(p0, p1, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.12f;
                    c.transform.Rotate(720f * Time.deltaTime, 360f * Time.deltaTime, 0f, Space.Self);
                    yield return null;
                }
                Destroy(c);
                if (sfx.thump != null) sfx.Play3D(sfx.thump, krasue.HeadPosition, 0.9f);
            }
            rig.Shake(0.02f, 0.2f);
        }

        IEnumerator Repel(DARepelZone zone, string hintText, HoldMode mode, bool insufficient)
        {
            holdMode = mode;
            heldVisible = true;
            krasue.Show(true);
            krasue.SetGlow(1.3f);
            krasue.PlaceHead(zone.peekPoint.position, zone.facing);
            sfx.Play3D(sfx.growl, zone.peekPoint.position, 1f);
            ui.Hint(hintText + "  -  or SHAKE the controller to throw it");
            rig.ShakeCharge = 0f;
            float progress = 0f, t = 0f;
            bool hintedMore = false;
            float target = insufficient ? 0.55f : 1f;
            while (progress < target)
            {
                float dt = Time.deltaTime;
                t += dt;
                bool owns = mode == HoldMode.Fire ? hasLighter : hasBranch;
                float ang;
                bool aim = rig.IsAimingAt(zone.zoneCollider, out ang);
                bool usingItem = owns && rig.UseHeld;
                if (aim && usingItem) progress += dt / zone.holdSeconds;
                else progress -= dt * 0.35f;
                progress = Mathf.Clamp01(progress);

                // throw gesture: shake / flick the controller while aiming at her -> the object flies at her face
                if (owns && aim && rig.ShakeCharge > 0.4f)
                {
                    rig.ShakeCharge = 0f;
                    yield return ThrowHeld(mode, zone);
                    progress = target;
                    break;
                }

                // she reacts: flinch and hiss while burnt / pricked
                krasue.SetGlow(progress > 0.05f ? 1.8f : 1.3f);
                fearTarget = Mathf.Lerp(0.75f, 0.5f, progress);
                if (progress > 0.05f && Time.frameCount % 45 == 0) rig.Shake(0.008f, 0.2f);
                if (t > 14f && !hintedMore) { hintedMore = true; ui.Hint(hintText + "  -  keep aiming at her face, or shake to throw"); }
                if (t > 40f) progress = 1f;                // never leave the player stuck
                yield return null;
            }
            ui.ClearHint();

            if (insufficient)
            {
                // she flinches, then bursts through: the candle is blown out
                sfx.Play3D(sfx.growl, zone.peekPoint.position, 1f);
                yield return krasue.MoveHead(zone.peekPoint.position + (-zone.facing) * -0.5f, 0.5f);
                sfx.Play3D(sfx.scream, zone.peekPoint.position, 1f, 0.9f, 30f);
                hasLighter = false; heldVisible = false;
                if (sfx.bite != null) sfx.Play3D(sfx.bite, krasue.HeadPosition, 1f, 1f, 30f);
                    StartCoroutine(krasue.Lunge(zone.peekPoint.position + zone.facing * 1.4f, 0.35f, 0.5f));
                rig.Shake(0.07f, 1.0f);
                fearTarget = 1f;
                yield return Wait(0.6f);
                yield return krasue.MoveHead(zone.peekPoint.position + (-zone.facing) * 6f + Vector3.up * 1.5f, 1.2f);
                krasue.Show(false);
                fearTarget = 0.7f;
                yield return Wait(0.8f);
                yield break;
            }

            // she recoils
            sfx.Play3D(sfx.scream, zone.peekPoint.position, 1f, 1.1f, 30f);
            rig.Shake(0.03f, 0.5f);
            Vector3 away = zone.peekPoint.position - zone.facing * 0f + (-zone.facing) * 6f + Vector3.up * 1.5f;
            yield return krasue.MoveHead(away, 1.2f);
            krasue.Show(false);
            fearTarget = 0.5f;
            yield return Wait(0.8f);
        }

        // -------------------------------------------------------------- Hide
        IEnumerator HideSequence(float hideTime, bool final, HidePlace place = null)
        {
            BuildHidePlaces();
            bool mainRoom = place == null || place.main;
            fearTarget = 0.7f;
            ui.Hint("Stay still. Do not look at the door or the windows.");
            Vector3[] spots = { doorZone.peekPoint.position, wallZone.peekPoint.position, krasueWindow.position };
            Vector3[] faces = { doorZone.facing, wallZone.facing, Vector3.back };
            if (place != null && place.spots != null) { spots = place.spots; faces = place.faces; }
            float timer = 0f, looked = 0f, nextMove = 0f;
            int notices = 0, idx = 0;
            krasue.Show(true);
            krasue.SetGlow(1.2f);
            krasue.PlaceHead(spots[0], faces[0]);
            while (timer < hideTime)
            {
                float dt = Time.deltaTime;
                nextMove -= dt;
                if (nextMove <= 0f)
                {
                    idx = (idx + 1) % spots.Length;
                    krasue.PlaceHead(spots[idx], faces[idx]);
                    AudioClip c = mainRoom ? (idx == 0 ? sfx.doorBang : (idx == 1 ? sfx.scratch : sfx.thump)) : (idx == 0 ? sfx.scratch : (idx == 1 ? sfx.thump : sfx.creak));
                    sfx.Play3D(c, spots[idx], 0.9f);
                    if (mainRoom && idx == 0) StartCoroutine(ShakeDoor(1.2f, 1.6f));
                    if (mainRoom && idx == 2) StartCoroutine(ShakeShutters(1.2f, 2f));
                    nextMove = 3.6f;
                }
                Vector3 to = (krasue.HeadPosition - rig.HeadPos).normalized;
                float angle = Vector3.Angle(rig.head.forward, to);
                bool looking = angle < 30f;
                if (looking) looked += dt; else looked = Mathf.Max(0f, looked - dt * 0.6f);
                if (looked > 1.4f && notices < 2)
                {
                    notices++;
                    looked = 0f;
                    timer = Mathf.Max(0f, timer - 3f);
                    sfx.Play3D(sfx.scream, krasue.HeadPosition, 1f, 1f, 30f);
                    rig.Shake(0.05f, 0.8f);
                    fearTarget = 1f;
                    if (sfx.bite != null) sfx.Play3D(sfx.bite, krasue.HeadPosition, 1f, 1f, 30f);
                    StartCoroutine(krasue.Lunge(krasue.HeadPosition + (krasue.HeadPosition - rig.HeadPos).normalized * -0.5f, 0.25f, 0.4f));
                }
                else if (!looking) timer += dt;
                fearTarget = Mathf.MoveTowards(fearTarget, 0.7f, dt * 0.15f);
                yield return null;
            }
            ui.ClearHint();

            // she backs off
            krasue.Show(false);
            if (!final) { fearTarget = 0.5f; yield return Wait(1.5f); yield break; }
            sfx.Bed2D("wind", sfx.wind, 0f, 3f);
            sfx.Bed2D("night", sfx.night, 0f, 3f);
            sfx.Music(null, 0f, 4f);
            fearTarget = 0.1f;
            yield return Wait(3.5f);
            yield return Line("", "<i>(silence)</i>");
            yield return Line("", "...Is it over?");
        }

        // -------------------------------------------------------------- ENDING (dawn + choice)
        IEnumerator Ending()
        {
            rig.smoothMove = false;
            yield return rig.FadeTo(1f, 1.4f);
            rig.SnapTo(aWindow, rig.eyeHeight);
            ShutterAngle = 0f;
            fearTarget = 0f;
            StartCoroutine(MoodTo(moodDawn, 16f));
            sfx.Bed2D("night", sfx.birds, 0.55f, 5f);
            sfx.Music(sfx.musicDawn, 0.9f, 4f);
            yield return Wait(1.0f);
            yield return rig.FadeTo(0f, 2.5f);
            StartCoroutine(SetShutters(95f, 7f, false));
            yield return Line("", "<i>(birds begin to sing)</i>");
            yield return Line("", "The first light of dawn. The field is empty. She is gone.");
            yield return Line("", Girl + "... why did it have to be you?");

            choice = -1;
            ui.Hint("Aim at your choice and pull the trigger");
            ui.ShowChoice("The village is waking up. What do you do?", "Denounce her", "Say nothing", delegate (int c) { choice = c; });
            while (choice < 0) yield return null;
            ui.ClearHint();
            sfx.Play2D(sfx.click, 0.8f);
        }

        // -------------------------------------------------------------- CONCLUSION
        IEnumerator Conclusion()
        {
            yield return rig.FadeTo(1f, 2.0f);
            sfx.Music(null, 0f, 3f);
            sfx.Bed2D("night", sfx.birds, 0f, 2f);
            yield return Wait(1.0f);
            string txt;
            if (choice == 0)
            {
                txt = "<size=130%><b>THE VILLAGE</b></size>\n\n" +
                      "You told the elders what you saw.\nBy nightfall, the villagers came with torches.\n\n" +
                      Girl + " was burned at the edge of the village.\n\n" +
                      "The nights became quiet again.\n<i>But never for you.</i>";
            }
            else
            {
                txt = "<size=130%><b>SILENCE</b></size>\n\n" +
                      "You told no one.\n\n" +
                      "The next morning, " + Girl + " was gone.\n" +
                      "Days later you learned she had taken her own life,\nso that she could never hurt her friends again.\n\n" +
                      "The nights stayed quiet.\n<i>You never forgave yourself.</i>\n\n" +
                      "<size=55%><color=#B8B8B8>If you are struggling, you are not alone. Please reach out to someone you trust or a local helpline.</color></size>";
            }
            ui.ShowCenter(txt, 2.2f);
            yield return Wait(14f);
            ui.HideCenter();
            yield return Wait(2.0f);
            ui.ShowCenter("<size=140%><b>THE DARK AWAKENING</b></size>\n<size=70%>end of the demo - thank you for playing</size>\n\n<size=70%><color=#E8C77A>Pull the trigger to play again</color></size>", 2.2f);
            yield return rig.WaitForTrigger();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
