#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using DarkAwakening;

namespace DarkAwakening.EditorTools
{
    /// <summary>
    /// Menu:  Dark Awakening > Build Demo Scene
    /// Imports/configures the assets, creates the materials and generates Assets/Scenes/TheDarkAwakening.unity
    /// (house, field, rig, UI, audio, Krasue, game director).
    /// </summary>
    public static class DABuilder
    {
        const string Root = "Assets/DarkAwakening";
        const string ScenePath = "Assets/Scenes/TheDarkAwakening.unity";
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        static int interiorLayer = -1;

        // ------------------------------------------------------------------ entry points
        [MenuItem("Dark Awakening/Build Demo Scene", false, 1)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("Dark Awakening/Open Demo Scene", false, 2)]
        public static void OpenMenu()
        {
            if (!File.Exists(ScenePath)) { BuildMenu(); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        const int BuildVersion = 25;                       // bump to make the scene rebuild itself once after an update
        static string DoneKey { get { return "DA_autobuild_done_" + BuildVersion; } }

        [InitializeOnLoadMethod]
        static void AutoInit()
        {
            if (SessionState.GetBool(DoneKey, false)) return;
            autoFrames = 0;
            EditorApplication.update += AutoUpdate;
        }

        static int autoFrames;
        static void AutoUpdate()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) { autoFrames = 0; return; }
            if (++autoFrames < 90) return;
            EditorApplication.update -= AutoUpdate;
            SessionState.SetBool(DoneKey, true);
            bool upToDate = File.Exists(ScenePath) && EditorPrefs.GetInt("DA_build_version", 0) >= BuildVersion;
            if (upToDate) return;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Krasue/krasue.fbx") == null) return;   // assets not imported yet
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            bool isOurs = active.path == ScenePath;
            if (active.isDirty && !isOurs) { Debug.Log("[DarkAwakening] Demo scene needs a rebuild: use the menu 'Dark Awakening > Build Demo Scene'."); return; }
            try { Build(); }
            catch (Exception e) { Debug.LogError("[DarkAwakening] Auto build failed, use the menu 'Dark Awakening > Build Demo Scene'. " + e); }
        }

        // The project's URP configs ship with "Additional Lights" disabled, which silences every point light (candle, fills, lantern).
        static void EnableAdditionalLights()
        {
            var seen = new HashSet<RenderPipelineAsset>();
            var list = new List<RenderPipelineAsset>();
            if (GraphicsSettings.defaultRenderPipeline != null) list.Add(GraphicsSettings.defaultRenderPipeline);
            for (int i = 0; i < QualitySettings.names.Length; i++) { var a = QualitySettings.GetRenderPipelineAssetAt(i); if (a != null) list.Add(a); }
            foreach (var rp in list)
            {
                if (!seen.Add(rp)) continue;
                var so = new SerializedObject(rp);
                var mode = so.FindProperty("m_AdditionalLightsRenderingMode");
                var limit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
                if (mode == null || limit == null) continue;
                mode.intValue = 1;                         // per pixel
                if (limit.intValue < 4) limit.intValue = 4;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(rp);
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ main build
        public static void Build()
        {
            cache.Clear();
            EnsureFolder(Root + "/Materials");
            EnsureFolder(Root + "/Generated");
            EnsureFolder("Assets/Scenes");
            interiorLayer = EnsureLayer("DAInterior");
            EnableAdditionalLights();

            ConfigureImporters();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- shared materials
            Material unlitBase = new Material(Shader.Find("DarkAwakening/Unlit"));
            Material wallMat = Lit("wall", Tex("Procedural/planks_wall.jpg"), null, 0f, 0.12f, new Color(1f, 1f, 1f));
            Material floorMat = Lit("floor", Tex("Procedural/planks_floor.jpg"), null, 0f, 0.15f, new Color(1f, 1f, 1f));
            Material darkWood = Lit("darkwood", Tex("Procedural/dark_wood.jpg"), null, 0f, 0.1f, new Color(1f, 1f, 1f));
            Material tableWood = Lit("tablewood", Tex("Procedural/table_wood.jpg"), null, 0f, 0.2f, new Color(1f, 1f, 1f));
            Material groundMat = Lit("ground", Tex("Procedural/ground.jpg"), null, 0f, 0.05f, Color.white);
            groundMat.SetTextureScale("_BaseMap", new Vector2(90f, 90f));
            EditorUtility.SetDirty(groundMat);
            Material grassMat = Lit("grass", Tex("Procedural/grass.png"), null, 0f, 0.05f, Color.white, true, true);
            Material bambooMat = Lit("bamboo", Tex("Procedural/bamboo_stalk.jpg"), null, 0f, 0.3f, Color.white);
            bambooMat.SetTextureScale("_BaseMap", new Vector2(1f, 14f));
            Material leafMat = Lit("foliage", Tex("Procedural/leaf_cluster.png"), null, 0f, 0.1f, Color.white, true, true);
            Material bambooLeafMat = Lit("bambooleaf", Tex("Procedural/bamboo_leaf.png"), null, 0f, 0.1f, Color.white, true, true);
            Material bananaMat = Lit("banana", Tex("Procedural/banana_leaf.png"), null, 0f, 0.25f, Color.white, true, true);
            Material bananaStem = Lit("bananastem", null, null, 0f, 0.15f, new Color(0.36f, 0.46f, 0.26f));
            Material trunkMat = Lit("trunk", Tex("Procedural/bark.jpg"), null, 0f, 0.1f, Color.white);
            trunkMat.SetTextureScale("_BaseMap", new Vector2(1.5f, 5f));
            EditorUtility.SetDirty(bambooMat); EditorUtility.SetDirty(trunkMat);
            Material metalMat = Lit("metal", null, null, 0.8f, 0.55f, new Color(0.55f, 0.55f, 0.58f));
            Material clothMat = Lit("cloth", null, null, 0f, 0.1f, new Color(0.35f, 0.08f, 0.07f));

            Material fadeMat = Unlit("fade", Tex("Procedural/dot.png"), new Color(0f, 0f, 0f, 1f), 8, 10, 4002);
            Material vigMat = Unlit("vignette", Tex("Procedural/vignette.png"), new Color(0.02f, 0f, 0f, 0.3f), 8, 10, 4001);
            Material grainMat = Unlit("grain", Tex("Procedural/grain.png"), new Color(1f, 1f, 1f, 0.09f), 8, 10, 4000);
            Material reticleMat = Unlit("reticle", Tex("Procedural/dot.png"), new Color(1f, 1f, 1f, 0.4f), 8, 10, 4003);
            Material ringMat = Unlit("ring", Tex("Procedural/ring.png"), new Color(1f, 0.85f, 0.5f, 0.9f), 8, 10, 4003);
            Material rayMat = Unlit("ray", Tex("Procedural/dot.png"), new Color(1f, 0.9f, 0.7f, 0.35f), 4, 10, 3100);
            Material glowMat = Unlit("markerglow", Tex("Procedural/glow.png"), new Color(1f, 0.82f, 0.45f, 0.9f), 4, 1, 3050);   // additive, depth tested
            Material flameMat = Unlit("flame", Tex("Procedural/flame.png"), Color.white, 4, 1, 3060);
            Material moonMat = Unlit("moon", Tex("Procedural/moon.png"), new Color(1f, 1f, 1f, 1f), 4, 10, 3000);
            Material photoMat = Unlit("photo", Tex("Procedural/photo_friends.jpg"), new Color(1f, 1f, 1f, 0f), 8, 10, 3500);
            Material erawanMat = Lit("erawan", Tex("Procedural/erawan_albedo.png"), Tex("Procedural/erawan_normal.png"), 0f, 0.55f, Color.white);
            Material faceMat = Unlit("krasueface", Tex("Procedural/krasue_face.png"), new Color(1f, 1f, 1f, 0f), 8, 10, 3501);
            Material subBg = Unlit("subbg", Tex("Procedural/dot.png"), new Color(0f, 0f, 0f, 0.6f), 8, 10, 4008);
            Material btnA = Unlit("btnA", Tex("Procedural/dot.png"), new Color(0.36f, 0.06f, 0.06f, 0.92f), 8, 10, 4008);
            Material btnB = Unlit("btnB", Tex("Procedural/dot.png"), new Color(0.08f, 0.22f, 0.26f, 0.92f), 8, 10, 4008);
            Material tmpOverlay = MakeTmpOverlay();

            Material skyMat = new Material(Shader.Find("Skybox/Panoramic"));
            skyMat.SetTexture("_MainTex", Tex("Procedural/sky_pano.jpg"));
            skyMat.SetColor("_Tint", new Color(0.09f, 0.13f, 0.26f));
            skyMat.SetFloat("_Exposure", 1f);
            SaveMaterial(skyMat, "sky");

            // ---- render settings
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.31f, 0.44f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.10f, 0.14f, 0.21f);
            RenderSettings.fogDensity = 0.015f;
            RenderSettings.skybox = skyMat;
            RenderSettings.reflectionIntensity = 0.25f;

            // ---- root objects
            GameObject systems = new GameObject("DA_Systems");
            GameObject houseRoot = new GameObject("House");
            GameObject outRoot = new GameObject("Outdoors");
            GameObject introRoot = new GameObject("IntroSet");
            introRoot.transform.position = new Vector3(0f, 0f, -300f);

            // ---- anchors
            Transform aIntro = Anchor("A_Intro", systems.transform, new Vector3(0f, 0f, -300f), 0f);
            Transform aBed = Anchor("A_Bed", systems.transform, new Vector3(-0.95f, 0f, 0.10f), 10f);
            Transform aWindow = Anchor("A_Window", systems.transform, new Vector3(-0.6f, 0f, 1.65f), 0f);
            Transform aTable = Anchor("A_Table", systems.transform, new Vector3(0.55f, 0f, 0.95f), 58f);
            Transform aDoor = Anchor("A_Door", systems.transform, new Vector3(1.35f, 0f, -0.55f), 90f);
            Transform aConfront = Anchor("A_Confront", systems.transform, new Vector3(-0.6f, 0f, 1.78f), 0f);
            Transform aHide = Anchor("A_Hide", systems.transform, new Vector3(-1.55f, 0f, -1.75f), 0f);

            // ---- build the world
            BuildHouse(houseRoot.transform, wallMat, floorMat, darkWood, tableWood, metalMat, clothMat);
            BuildProps(houseRoot.transform, tableWood, metalMat, darkWood);
            BuildOutdoors(outRoot.transform, groundMat, grassMat, bambooMat, leafMat, bambooLeafMat, bananaMat, bananaStem, trunkMat, darkWood, glowMat);

            // ---- intro set (a floating old photo in the dark)
            GameObject photo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            photo.name = "Photo";
            Strip(photo);
            photo.transform.SetParent(introRoot.transform, false);
            photo.transform.localPosition = new Vector3(0f, 1.6f, 2.3f);
            photo.transform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            photo.transform.localScale = new Vector3(1.3f, 1.034f, 1f);
            photo.GetComponent<Renderer>().sharedMaterial = photoMat;

            // ---- interior key objects (interactables)
            Transform props = houseRoot.transform;
            DAInteractable windowMark = MakeInteractable("Interact_Window", props, new Vector3(-0.6f, 1.45f, 2.0f), new Vector3(1.3f, 1.2f, 0.25f), "Window", glowMat, 0.28f, new Vector3(0f, 0f, -0.2f), true);
            DAInteractable shutterPick = MakeInteractable("Interact_Shutters", props, new Vector3(-0.6f, 1.45f, 2.0f), new Vector3(1.3f, 1.2f, 0.25f), "Close the shutters", glowMat, 0.28f, new Vector3(0f, 0f, -0.2f), true);
            DAInteractable lighterPick = MakeInteractable("Interact_Lighter", props, new Vector3(1.38f, 0.82f, 1.38f), new Vector3(0.3f, 0.25f, 0.3f), "Candle", glowMat, 0.14f, new Vector3(0f, 0.12f, 0f), true);
            DAInteractable branchPick = MakeInteractable("Interact_ThornBranch", props, new Vector3(2.38f, 1.35f, -1.55f), new Vector3(0.5f, 0.8f, 0.9f), "Jujube thorn branch", glowMat, 0.2f, new Vector3(-0.15f, 0.1f, 0f), true);
            DAInteractable hidePick = MakeInteractable("Interact_HideSpot", props, new Vector3(-1.55f, 0.55f, -1.75f), new Vector3(0.7f, 0.9f, 0.6f), "Hide here", glowMat, 0.22f, new Vector3(0f, 0.2f, 0f), true);

            // second candle on the table (the one the player picks up)
            GameObject lighterVis = MakeErawanCandle("Candle_Pickup", props, new Vector3(1.38f, 0.765f, 1.38f), 0.22f, erawanMat);
            lighterVis.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            if (lighterVis == null) lighterVis = new GameObject("Candle_Pickup");
            Light pcl = new GameObject("PickupCandleGlow").AddComponent<Light>();
            pcl.transform.SetParent(lighterVis.transform, true);
            pcl.transform.position = new Vector3(1.38f, 1.0f, 1.38f);
            pcl.type = LightType.Point; pcl.color = new Color(1f, 0.7f, 0.4f); pcl.range = 1.6f; pcl.intensity = 0.5f; pcl.shadows = LightShadows.None;
            lighterPick.visualToHide = lighterVis;

            // thorn branch on the wall
            GameObject branchWall = Place("Branch", "branch", "Branch_OnWall", props, new Vector3(2.40f, 1.0f, -1.55f), new Vector3(0f, 90f, 0f), 1f);
            branchPick.visualToHide = branchWall;

            // ---- Krasue
            GameObject krasueRoot = new GameObject("Krasue");
            DAKrasue krasue = krasueRoot.AddComponent<DAKrasue>();
            GameObject krasueModel = Place("Krasue", "krasue", "Model", krasueRoot.transform, Vector3.zero, new Vector3(-90f, 0f, 0f), 1f);
            krasue.model = krasueModel != null ? krasueModel.transform : null;
            if (krasueModel != null)
            {
                Animator an = krasueModel.GetComponent<Animator>();
                if (an == null) an = krasueModel.AddComponent<Animator>();
                an.runtimeAnimatorController = BuildKrasueController();
                an.applyRootMotion = false;
                an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                krasue.animator = an;
                foreach (SkinnedMeshRenderer smr in krasueModel.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    smr.updateWhenOffscreen = true;
                    smr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            GameObject glowGo = new GameObject("KrasueGlow");
            glowGo.transform.SetParent(krasueRoot.transform, false);
            glowGo.transform.localPosition = new Vector3(0f, 1.5f, 0.45f);
            Light kl = glowGo.AddComponent<Light>();
            kl.type = LightType.Point; kl.color = new Color(1f, 0.1f, 0.06f); kl.range = 6f; kl.intensity = 0f; kl.shadows = LightShadows.None;
            krasue.glow = kl;
            krasue.glowTransform = glowGo.transform;
            // red aura halo around the head (additive soft glow, always facing the player)
            Material auraMat = Unlit("kaura", Tex("Procedural/glow.png"), new Color(1f, 0.08f, 0.05f, 0.4f), 4, 1, 3100);
            GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Quad);
            aura.name = "KrasueAura"; Strip(aura);
            aura.transform.SetParent(krasueRoot.transform, false);
            aura.transform.localPosition = new Vector3(0f, 1.47f, 0.12f);
            aura.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            aura.GetComponent<Renderer>().sharedMaterial = auraMat;
            aura.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            krasue.auraTransform = aura.transform;
            krasueRoot.SetActive(false);

            Transform krasueFar = Anchor("Krasue_Far", systems.transform, new Vector3(-1.6f, 1.3f, 17f), 0f);
            Transform krasueMid = Anchor("Krasue_Mid", systems.transform, new Vector3(-0.6f, 1.5f, 6.2f), 0f);
            Transform krasueWindow = Anchor("Krasue_Window", systems.transform, new Vector3(-0.6f, 1.45f, 2.4f), 0f);

            // repel zones (vents)
            DARepelZone doorZone = MakeZone("Zone_DoorVent", props, new Vector3(2.44f, 1.6f, -0.5f), new Vector3(0.3f, 0.36f, 0.62f), new Vector3(3.05f, 1.6f, -0.5f), new Vector3(-1f, 0f, 0f), DARepelZone.Need.Fire);
            DARepelZone windowZone = MakeZone("Zone_Window", props, new Vector3(-0.6f, 1.45f, 2.22f), new Vector3(1.1f, 0.9f, 0.3f), new Vector3(-0.6f, 1.45f, 2.38f), new Vector3(0f, 0f, -1f), DARepelZone.Need.Fire);
            DARepelZone wallZone = MakeZone("Zone_WallVent", props, new Vector3(1.1f, 1.6f, 2.0f), new Vector3(0.62f, 0.36f, 0.3f), new Vector3(1.1f, 1.6f, 2.72f), new Vector3(0f, 0f, -1f), DARepelZone.Need.Thorns);

            // ---- lights
            Light moon = NewLight("Moon", systems.transform, LightType.Directional, new Color(0.62f, 0.74f, 1f), 1.25f);
            moon.transform.rotation = Quaternion.LookRotation(new Vector3(25f, -40f, -55f).normalized);
            moon.shadows = LightShadows.None;
            Light sun = NewLight("Sun", systems.transform, LightType.Directional, new Color(1f, 0.72f, 0.46f), 0f);
            sun.transform.rotation = Quaternion.Euler(14f, -110f, 0f);
            sun.shadows = LightShadows.None;
            if (interiorLayer >= 0)
            {
                moon.cullingMask = ~(1 << interiorLayer);
                sun.cullingMask = ~(1 << interiorLayer);
            }
            Light shaft = NewLight("MoonShaft", systems.transform, LightType.Spot, new Color(0.5f, 0.62f, 1f), 0f);
            shaft.transform.position = new Vector3(-0.6f, 2.3f, 3.4f);
            shaft.transform.rotation = Quaternion.LookRotation(new Vector3(0f, -2.4f, -3.0f).normalized);
            shaft.spotAngle = 62f; shaft.range = 9f; shaft.shadows = LightShadows.None;
            if (interiorLayer >= 0) shaft.cullingMask = 1 << interiorLayer;

            // candle on the table (initially lit)
            GameObject candleGo = new GameObject("CandleLight");
            candleGo.transform.SetParent(props, false);
            candleGo.transform.position = new Vector3(1.068f, 1.06f, 1.635f);
            Light cl = candleGo.AddComponent<Light>();
            cl.type = LightType.Point; cl.color = new Color(1f, 0.62f, 0.3f); cl.range = 8f; cl.intensity = 2.2f; cl.shadows = LightShadows.None;
            DALightFlicker candleFlicker = candleGo.AddComponent<DALightFlicker>();
            candleFlicker.target = cl; candleFlicker.baseIntensity = 2.2f; candleFlicker.amount = 0.25f; candleFlicker.fadeSpeed = 4f;
            GameObject candleFlame = MakeFlame("CandleFlame", candleGo.transform, flameMat, new Vector3(0f, -0.012f, 0f));
            candleFlicker.flameVisual = candleFlame.transform.GetChild(0).gameObject;
            candleFlicker.SetFlameBase(0.045f, 0.085f);

            // soft interior fill so the room is readable (oil-lamp glow from the ceiling)
            Light fill = NewLight("RoomFill", houseRoot.transform, LightType.Point, new Color(1f, 0.78f, 0.58f), 1.6f);
            fill.transform.position = new Vector3(0.1f, 2.3f, 0.3f);
            fill.range = 9f; fill.shadows = LightShadows.None;
            Light fill2 = NewLight("RoomFillBed", houseRoot.transform, LightType.Point, new Color(0.72f, 0.82f, 1f), 1.1f);
            fill2.transform.position = new Vector3(-1.4f, 2.1f, -0.4f);
            fill2.range = 7f; fill2.shadows = LightShadows.None;
            Light fill3 = NewLight("RoomFillTable", houseRoot.transform, LightType.Point, new Color(1f, 0.74f, 0.5f), 1.0f);
            fill3.transform.position = new Vector3(1.5f, 2.2f, 1.1f);
            fill3.range = 7f; fill3.shadows = LightShadows.None;
            if (interiorLayer >= 0) { fill.cullingMask = 1 << interiorLayer; fill2.cullingMask = 1 << interiorLayer; fill3.cullingMask = 1 << interiorLayer; }

            // dawn leaks (interior warm light at vents / window)
            Light[] leaks = new Light[3];
            Vector3[] leakPos = { new Vector3(2.1f, 1.6f, -0.5f), new Vector3(1.1f, 1.6f, 1.8f), new Vector3(-0.6f, 1.5f, 1.7f) };
            for (int i = 0; i < 3; i++)
            {
                leaks[i] = NewLight("DawnLeak" + i, houseRoot.transform, LightType.Point, new Color(1f, 0.68f, 0.4f), 0f);
                leaks[i].transform.position = leakPos[i];
                leaks[i].range = 4f; leaks[i].shadows = LightShadows.None; leaks[i].enabled = false;
            }

            // moon sprite
            GameObject moonSprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
            moonSprite.name = "MoonSprite"; Strip(moonSprite);
            moonSprite.transform.SetParent(outRoot.transform, false);
            moonSprite.transform.position = new Vector3(-30f, 55f, 95f);
            moonSprite.transform.localScale = Vector3.one * 16f;
            moonSprite.GetComponent<Renderer>().sharedMaterial = moonMat;
            moonSprite.AddComponent<DABillboard>();

            // ---- rig
            DARig rig = BuildRig(systems.transform, fadeMat, vigMat, grainMat, reticleMat, ringMat, rayMat, metalMat, flameMat, out DALightFlicker heldFlame, out GameObject heldLighter, out GameObject heldBranch);

            // ---- UI
            DAUI ui = BuildUI(systems.transform, rig, subBg, btnA, btnB, tmpOverlay);

            // ---- audio
            DAAudio audio = systems.AddComponent<DAAudio>();
            audio.wind = Clip("wind_loop"); audio.night = Clip("night"); audio.birds = Clip("birds");
            audio.heartbeat = Clip("heartbeat"); audio.flame = Clip("flame_loop");
            audio.musicIntro = Clip("music_intro"); audio.musicTension = Clip("music_tension"); audio.musicDawn = Clip("music_dawn");
            audio.creak = Clip("creak"); audio.thump = Clip("thump"); audio.doorBang = Clip("door_bang"); audio.scratch = Clip("scratch");
            audio.scream = Clip("scream"); audio.growl = Clip("growl"); audio.whisper = Clip("whisper");
            audio.lighter = Clip("lighter"); audio.pickup = Clip("pickup"); audio.slam = Clip("window_slam"); audio.click = Clip("ui_click");
            audio.bite = Clip("bite"); audio.footsteps = Clip("footsteps"); audio.windowOpen = Clip("window_open");

            // ---- game director
            DAGame game = systems.AddComponent<DAGame>();
            game.rig = rig; game.ui = ui; game.sfx = audio;
            game.aIntro = aIntro; game.aBed = aBed; game.aWindow = aWindow; game.aTable = aTable; game.aDoor = aDoor; game.aHide = aHide; game.aConfront = aConfront;
            game.introSet = introRoot; game.houseSet = houseRoot; game.outdoorSet = outRoot;
            game.photoRenderer = photo.GetComponent<Renderer>();
            GameObject faceCard = GameObject.CreatePrimitive(PrimitiveType.Quad);
            faceCard.name = "KrasueFaceCard";
            UnityEngine.Object.DestroyImmediate(faceCard.GetComponent<Collider>());
            faceCard.transform.SetParent(systems.transform, false);
            faceCard.GetComponent<Renderer>().sharedMaterial = faceMat;
            faceCard.SetActive(false);
            game.krasueFaceRenderer = faceCard.GetComponent<Renderer>();
            game.shutterL = houseRoot.transform.Find("ShutterL");
            game.shutterR = houseRoot.transform.Find("ShutterR");
            game.doorPivot = houseRoot.transform.Find("DoorPivot");
            game.windowMark = windowMark; game.shutterPick = shutterPick; game.lighterPick = lighterPick; game.branchPick = branchPick; game.hidePick = hidePick;
            game.moon = moon; game.sun = sun; game.moonShaft = shaft; game.candleFlicker = candleFlicker; game.dawnLeaks = leaks;
            game.krasue = krasue; game.krasueFar = krasueFar; game.krasueMid = krasueMid; game.krasueWindow = krasueWindow;
            game.doorZone = doorZone; game.windowZone = windowZone; game.wallZone = wallZone;
            game.heldLighter = heldLighter; game.heldFlame = heldFlame; game.heldBranch = heldBranch;
            game.skyMat = skyMat;

            if (interiorLayer >= 0) SetLayerRecursive(houseRoot, interiorLayer);

            // ---- save + register
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterScene();
            EditorPrefs.SetInt("DA_build_version", BuildVersion);
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log("[DarkAwakening] Demo scene built: " + ScenePath + "  -  press Play (mouse: hold right button to look, left click = trigger, R = recenter).");
        }

        static void RegisterScene()
        {
            List<EditorBuildSettingsScene> list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ house
        static void BuildHouse(Transform parent, Material wall, Material floor, Material dark, Material table, Material metal, Material cloth)
        {
            // room shell: low-poly Thai house interior asset (floor, walls with window/vent/door openings, ceiling, beams, frames)
            Place("ThaiHouse", "ThaiHouse_Interior", "HouseShell", parent, Vector3.zero, Vector3.zero, 1f);
            float[] sx = { -2.45f, 0f, 2.45f };
            float[] sz = { -1.95f, 0f, 1.95f };
            for (int a = 0; a < sx.Length; a++)
                for (int b = 0; b < sz.Length; b++)
                    Box("Stilt", parent, new Vector3(sx[a], -0.8f, sz[b]), new Vector3(0.2f, 1.6f, 0.2f), dark, 0.6f);

            // shutters (pivots at the hinge, leaves swing inward)
            GameObject sl = new GameObject("ShutterL");
            sl.transform.SetParent(parent, false);
            sl.transform.position = new Vector3(-1.2f, 1.45f, 2.07f);
            Box("Leaf", sl.transform, new Vector3(0.2925f, 0f, 0f), new Vector3(0.585f, 1.1f, 0.04f), dark, 0.7f, true);
            GameObject sr = new GameObject("ShutterR");
            sr.transform.SetParent(parent, false);
            sr.transform.position = new Vector3(0.0f, 1.45f, 2.07f);
            Box("Leaf", sr.transform, new Vector3(-0.2925f, 0f, 0f), new Vector3(0.585f, 1.1f, 0.04f), dark, 0.7f, true);

            // door: hinge on the z=0 side, leaf extends toward -z, vent hole at z -0.75..-0.25, y 1.5..1.7
            GameObject dp = new GameObject("DoorPivot");
            dp.transform.SetParent(parent, false);
            dp.transform.position = new Vector3(2.50f, 0f, 0.0f);
            Box("DoorBottom", dp.transform, new Vector3(0f, 0.75f, -0.5f), new Vector3(0.05f, 1.5f, 1.0f), dark, 0.8f, true);
            Box("DoorTop", dp.transform, new Vector3(0f, 1.85f, -0.5f), new Vector3(0.05f, 0.3f, 1.0f), dark, 0.8f, true);
            Box("DoorMidL", dp.transform, new Vector3(0f, 1.6f, -0.875f), new Vector3(0.05f, 0.2f, 0.25f), dark, 0.8f, true);
            Box("DoorMidR", dp.transform, new Vector3(0f, 1.6f, -0.125f), new Vector3(0.05f, 0.2f, 0.25f), dark, 0.8f, true);
            Box("DoorHandle", dp.transform, new Vector3(-0.05f, 1.0f, -0.12f), new Vector3(0.05f, 0.04f, 0.16f), metal, 1f);
            Box("DoorBarBracket", parent, new Vector3(2.46f, 1.0f, 0.1f), new Vector3(0.06f, 0.06f, 0.2f), dark, 1f);

            // furniture: table, stool, shelf
            Box("TableTop", parent, new Vector3(1.45f, 0.74f, 1.55f), new Vector3(1.1f, 0.05f, 0.6f), table, 0.8f);
            float[] lx = { -0.5f, 0.5f };
            float[] lz = { -0.25f, 0.25f };
            for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++)
                    Box("TableLeg", parent, new Vector3(1.45f + lx[a], 0.36f, 1.55f + lz[b]), new Vector3(0.06f, 0.72f, 0.06f), table, 0.5f);
            Box("StoolSeat", parent, new Vector3(0.75f, 0.42f, 1.0f), new Vector3(0.36f, 0.05f, 0.36f), table, 0.5f);
            for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++)
                    Box("StoolLeg", parent, new Vector3(0.75f + (a - 0.5f) * 0.26f, 0.2f, 1.0f + (b - 0.5f) * 0.26f), new Vector3(0.05f, 0.4f, 0.05f), table, 0.5f);
            Box("Shelf", parent, new Vector3(2.05f, 0.95f, -1.85f), new Vector3(0.95f, 0.04f, 0.4f), table, 0.8f);
            Box("ShelfBracketA", parent, new Vector3(1.7f, 0.85f, -2.0f), new Vector3(0.04f, 0.2f, 0.12f), dark, 0.5f);
            Box("ShelfBracketB", parent, new Vector3(2.4f, 0.85f, -2.0f), new Vector3(0.04f, 0.2f, 0.12f), dark, 0.5f);
            // blanket / rug / floor mat
            Box("FloorMat", parent, new Vector3(0.3f, 0.005f, 0.1f), new Vector3(1.8f, 0.01f, 1.2f), cloth, 1f);
        }

        static void BuildProps(Transform parent, Material table, Material metal, Material dark)
        {
            // bed + pillow
            Place("Bed", "bed", "Bed", parent, new Vector3(-1.55f, 0f, -0.3f), Vector3.zero, 1f);
            Place("Pillow", "pillow", "Pillow", parent, new Vector3(-1.55f, 0.5f, 0.45f), new Vector3(0f, 0f, 0f), 1f);
            // table top
            Place("Candle", "candle", "CandleStand", parent, new Vector3(1.05f, 0.765f, 1.62f), Vector3.zero, 1f);
            Place("Fruits", "fruits", "Fruits", parent, new Vector3(1.55f, 0.765f, 1.66f), new Vector3(0f, 20f, 0f), 1f);
            Place("TeaCup", "teacup", "TeaCup", parent, new Vector3(1.78f, 0.765f, 1.34f), new Vector3(0f, 140f, 0f), 1f);
            Place("Books", "books", "Books", parent, new Vector3(1.86f, 0.765f, 1.7f), new Vector3(0f, -15f, 0f), 1f);
            // shelf corner (offerings)
            Place("Benjarong", "benjarong", "Benjarong", parent, new Vector3(2.25f, 0.97f, -1.85f), Vector3.zero, 1f);
            Place("Garland", "garland", "Garland", parent, new Vector3(1.78f, 0.97f, -1.85f), new Vector3(0f, 30f, 0f), 1f);
            Place("GhostJar", "ghostjar", "GhostJar", parent, new Vector3(2.0f, 0f, -1.4f), Vector3.zero, 1f);
            // walls
            Place("WoodCarving", "woodcarving", "WoodCarving", parent, new Vector3(-2.47f, 1.7f, -0.3f), new Vector3(0f, -90f, 0f), 1f);
            // worn wooden bench against the south wall (replaces the TV cabinet; no modern technology in the room)
            Place("Bench", "bench", "WornWoodenBench", parent, new Vector3(0.45f, 0f, -1.68f), Vector3.zero, 0.5f);
        }

        static void WallN(Transform p, Material m, float x0, float x1, float y0, float y1)
        {
            Box("Wall_N", p, new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, 2.16f), new Vector3(x1 - x0, y1 - y0, 0.12f), m, 1.2f);
        }

        static void WallE(Transform p, Material m, float z0, float z1, float y0, float y1)
        {
            Box("Wall_E", p, new Vector3(2.56f, (y0 + y1) / 2f, (z0 + z1) / 2f), new Vector3(0.12f, y1 - y0, z1 - z0), m, 1.2f);
        }

        // ------------------------------------------------------------------ outdoors
        static void BuildOutdoors(Transform parent, Material ground, Material grass, Material bamboo, Material leaf, Material bambooLeaf, Material banana, Material bananaStem,
                                  Material trunk, Material dark, Material glow)
        {
            System.Random rnd = new System.Random(42);
            Func<float, float, float> R = (a, b) => a + (float)rnd.NextDouble() * (b - a);
            const float GY = -1.5f;   // ground height (house stands on stilts)

            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "Ground"; Strip(g);
            g.transform.SetParent(parent, false);
            g.transform.position = new Vector3(0f, GY, 0f);
            g.transform.localScale = new Vector3(60f, 1f, 60f);
            g.GetComponent<Renderer>().sharedMaterial = ground;

            // ---- sugar-cane field patches (from the shared sugar-plantation asset), clustered around the clearing
            GameObject sugarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/Sugar/sugar_field.obj");
            if (sugarPrefab != null)
            {
                Material sugarMat = Lit("sugar", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Sugar/sugar_palette.jpg"), null, 0f, 0.08f, new Color(0.78f, 0.82f, 0.86f), false, true);
                GameObject fieldRoot = new GameObject("SugarField");
                fieldRoot.transform.SetParent(parent, false);
                Vector3[] patches = {
                    new Vector3(9f, 0f, 20f), new Vector3(-13f, 0f, 14f), new Vector3(14f, 0f, 15f),
                    new Vector3(-17f, 0f, 2f), new Vector3(17f, 0f, 3f), new Vector3(-12f, 0f, -12f),
                    new Vector3(13f, 0f, -11f), new Vector3(0f, 0f, -18f), new Vector3(-26f, 0f, 20f), new Vector3(27f, 0f, 20f) };
                for (int i = 0; i < patches.Length; i++)
                {
                    GameObject f = (GameObject)PrefabUtility.InstantiatePrefab(sugarPrefab, fieldRoot.transform);
                    f.name = "SugarPatch" + i;
                    f.transform.position = new Vector3(patches[i].x, GY + 0.02f, patches[i].z);
                    f.transform.rotation = Quaternion.Euler(0f, i * 73f % 360f, 0f);
                    f.transform.localScale = Vector3.one * 2.4f;
                    foreach (Renderer r in f.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterial = sugarMat;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    if (i == 0) { Bounds bb = f.GetComponentInChildren<Renderer>().bounds; Debug.Log("[DarkAwakening] sugar patch bounds " + bb.size + " centre " + bb.center); }
                }
            }
            else Debug.LogWarning("[DarkAwakening] sugar_field.obj missing (not imported yet)");

            Mesh tuft = CardsMesh("Tuft", 2, false, 1f, 0.8f);
            Mesh leafCard = CardsMesh("LeafCard", 1, false, 1f, 1f);
            Mesh crownCards = CardsMesh("CrownCards", 3, true, 1f, 1f);

            // ---- grass: dense near the house, sparser further out
            GameObject tufts = new GameObject("Grass");
            tufts.transform.SetParent(parent, false);
            for (int i = 0; i < 520; i++)
            {
                float ang = R(0f, Mathf.PI * 2f);
                float rad = i < 300 ? R(2.8f, 15f) : R(15f, 48f);
                Vector3 pos = new Vector3(Mathf.Cos(ang) * rad, GY, Mathf.Sin(ang) * rad + 3f);
                if (Mathf.Abs(pos.x) < 3.4f && pos.z < 2.6f && pos.z > -3f) continue;   // keep the area under the house open
                GameObject t = new GameObject("Tuft");
                t.transform.SetParent(tufts.transform, false);
                t.transform.position = pos;
                t.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
                float s = R(0.8f, 1.7f);
                t.transform.localScale = new Vector3(s * 1.5f, s, s * 1.5f);
                t.AddComponent<MeshFilter>().sharedMesh = tuft;
                MeshRenderer mr = t.AddComponent<MeshRenderer>();
                mr.sharedMaterial = grass; mr.shadowCastingMode = ShadowCastingMode.Off;
                GameObjectUtility.SetStaticEditorFlags(t, StaticEditorFlags.BatchingStatic);
            }

            // ---- bamboo clumps (segmented stalks + leafy tops, swaying)
            GameObject bam = new GameObject("Bamboo");
            bam.transform.SetParent(parent, false);
            Vector3[] fixedClumps = { new Vector3(4.6f, GY, 7.5f), new Vector3(-5.5f, GY, 9f), new Vector3(8.5f, GY, 4.5f), new Vector3(-9f, GY, 5f) };
            for (int c = 0; c < 20; c++)
            {
                Vector3 centre;
                if (c < fixedClumps.Length) centre = fixedClumps[c];
                else
                {
                    float ang = R(0f, Mathf.PI * 2f);
                    float rad = R(12f, 38f);
                    centre = new Vector3(Mathf.Cos(ang) * rad, GY, Mathf.Sin(ang) * rad + 6f);
                    if (centre.z < 4f) centre.z += 14f;
                }
                GameObject clump = new GameObject("BambooClump");
                clump.transform.SetParent(bam.transform, false);
                clump.transform.position = centre;
                DASway sw = clump.AddComponent<DASway>();
                sw.amplitude = 1.6f; sw.speed = R(0.22f, 0.4f);
                int n = 6 + rnd.Next(4);
                float tallest = 0f;
                for (int k = 0; k < n; k++)
                {
                    float h = R(6.5f, 9.5f); tallest = Mathf.Max(tallest, h);
                    GameObject st = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    st.name = "Stalk"; Strip(st);
                    st.transform.SetParent(clump.transform, false);
                    Vector3 off = new Vector3(R(-0.7f, 0.7f), 0f, R(-0.7f, 0.7f));
                    st.transform.localPosition = off + Vector3.up * (h * 0.5f);
                    st.transform.localScale = new Vector3(R(0.07f, 0.11f), h * 0.5f, R(0.07f, 0.11f));
                    st.transform.localRotation = Quaternion.Euler(R(-5f, 5f), 0f, R(-5f, 5f));
                    st.GetComponent<Renderer>().sharedMaterial = bamboo;
                    st.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    // leafy branches near the top of each stalk
                    for (int l = 0; l < 3; l++)
                    {
                        GameObject lf = new GameObject("BambooLeaves");
                        lf.transform.SetParent(clump.transform, false);
                        lf.transform.localPosition = off + Vector3.up * (h * R(0.55f, 0.98f));
                        lf.transform.localRotation = Quaternion.Euler(R(35f, 70f), R(0f, 360f), 0f);
                        lf.transform.localScale = new Vector3(R(1.1f, 1.6f), R(2.0f, 2.8f), 1f);
                        lf.AddComponent<MeshFilter>().sharedMesh = leafCard;
                        MeshRenderer lr = lf.AddComponent<MeshRenderer>();
                        lr.sharedMaterial = bambooLeaf; lr.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }

            // ---- banana plants (large torn leaves)
            GameObject bn = new GameObject("Bananas");
            bn.transform.SetParent(parent, false);
            Vector3[] bananaSpots = { new Vector3(-2.6f, GY, 4.8f), new Vector3(2.9f, GY, 5.6f), new Vector3(-6.5f, GY, 7.4f), new Vector3(6.8f, GY, 8.6f), new Vector3(1.2f, GY, 12.5f), new Vector3(-3.8f, GY, 14f), new Vector3(9.5f, GY, 11f) };
            foreach (Vector3 bp in bananaSpots)
            {
                GameObject plant = new GameObject("BananaPlant");
                plant.transform.SetParent(bn.transform, false);
                plant.transform.position = bp;
                DASway sw = plant.AddComponent<DASway>();
                sw.amplitude = 1.2f; sw.speed = R(0.3f, 0.5f);
                float sh = R(1.8f, 2.6f);
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = "Stem"; Strip(stem);
                stem.transform.SetParent(plant.transform, false);
                stem.transform.localPosition = new Vector3(0f, sh * 0.5f, 0f);
                stem.transform.localScale = new Vector3(0.22f, sh * 0.5f, 0.22f);
                stem.GetComponent<Renderer>().sharedMaterial = bananaStem;
                stem.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                int leaves = 7 + rnd.Next(3);
                for (int l = 0; l < leaves; l++)
                {
                    GameObject lf = new GameObject("BananaLeaf");
                    lf.transform.SetParent(plant.transform, false);
                    lf.transform.localPosition = new Vector3(0f, sh * R(0.85f, 1f), 0f);
                    lf.transform.localRotation = Quaternion.Euler(R(40f, 80f), (360f / leaves) * l + R(-15f, 15f), 0f);
                    lf.transform.localScale = new Vector3(R(0.8f, 1.1f), R(2.4f, 3.4f), 1f);
                    lf.AddComponent<MeshFilter>().sharedMesh = leafCard;
                    MeshRenderer lr = lf.AddComponent<MeshRenderer>();
                    lr.sharedMaterial = banana; lr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // ---- trees: bark trunks with several leaf-card clusters
            GameObject trees = new GameObject("Trees");
            trees.transform.SetParent(parent, false);
            Vector3[] treeSpots = { new Vector3(-8.5f, GY, 13f), new Vector3(10.5f, GY, 16f), new Vector3(2f, GY, 24f), new Vector3(-15f, GY, 18f) };
            for (int i = 0; i < 14; i++)
            {
                Vector3 p;
                if (i < treeSpots.Length) p = treeSpots[i];
                else
                {
                    float ang = R(0f, Mathf.PI * 2f);
                    float rad = R(18f, 46f);
                    p = new Vector3(Mathf.Cos(ang) * rad, GY, Mathf.Sin(ang) * rad + 6f);
                    if (p.z < 6f) p.z += 18f;
                }
                float h = R(5.5f, 9f);
                GameObject tree = new GameObject("Tree");
                tree.transform.SetParent(trees.transform, false);
                tree.transform.position = p;
                DASway sw = tree.AddComponent<DASway>();
                sw.amplitude = 0.7f; sw.speed = R(0.15f, 0.28f);
                GameObject tr = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tr.name = "Trunk"; Strip(tr);
                tr.transform.SetParent(tree.transform, false);
                tr.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
                tr.transform.localScale = new Vector3(R(0.45f, 0.65f), h * 0.5f, R(0.45f, 0.65f));
                tr.GetComponent<Renderer>().sharedMaterial = trunk;
                tr.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                int clusters = 9;
                for (int c = 0; c < clusters; c++)
                {
                    GameObject cl = new GameObject("Crown");
                    cl.transform.SetParent(tree.transform, false);
                    float a = R(0f, Mathf.PI * 2f), rr = R(0f, 2.4f);
                    cl.transform.localPosition = new Vector3(Mathf.Cos(a) * rr, h + R(-0.8f, 2.2f), Mathf.Sin(a) * rr);
                    cl.transform.localRotation = Quaternion.Euler(R(-15f, 15f), R(0f, 360f), R(-15f, 15f));
                    float s = R(4.2f, 6.0f);
                    cl.transform.localScale = new Vector3(s, s * 0.8f, s);
                    cl.AddComponent<MeshFilter>().sharedMesh = crownCards;
                    MeshRenderer cr = cl.AddComponent<MeshRenderer>();
                    cr.sharedMaterial = leaf; cr.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // ---- field fence in front of the house
            GameObject fence = new GameObject("Fence");
            fence.transform.SetParent(parent, false);
            for (int i = 0; i < 14; i++)
            {
                float x = -9f + i * 1.4f;
                Box("Post", fence.transform, new Vector3(x, -0.75f, 9.5f), new Vector3(0.12f, 1.5f, 0.12f), dark, 0.5f);
            }
            Box("Rail1", fence.transform, new Vector3(0.1f, -0.55f, 9.5f), new Vector3(18.2f, 0.07f, 0.05f), dark, 0.5f);
            Box("Rail2", fence.transform, new Vector3(0.1f, -0.1f, 9.5f), new Vector3(18.2f, 0.07f, 0.05f), dark, 0.5f);

            // ---- fireflies drifting over the yard
            GameObject ff = new GameObject("Fireflies");
            ff.transform.SetParent(parent, false);
            for (int i = 0; i < 30; i++)
            {
                GameObject f = GameObject.CreatePrimitive(PrimitiveType.Quad);
                f.name = "Firefly"; Strip(f);
                f.transform.SetParent(ff.transform, false);
                f.transform.position = new Vector3(R(-9f, 9f), R(-1.1f, 0.9f), R(4f, 17f));
                f.GetComponent<Renderer>().sharedMaterial = glow;
                f.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                DAFirefly fly = f.AddComponent<DAFirefly>();
                fly.rend = f.GetComponent<Renderer>();
            }

            // ---- the neighbour's stilt house (Isan style) with a warm lamp
            Place("IsanHouse", "isanhouse", "NeighbourHouse", parent, new Vector3(-3.5f, -1.5f, 25f), new Vector3(0f, 180f, 0f), 1f);   // straight ahead through the window
            GameObject lamp = new GameObject("NeighbourLamp");
            lamp.transform.SetParent(parent, false);
            lamp.transform.position = new Vector3(-3.5f, 1.2f, 19.0f);
            Light ll = lamp.AddComponent<Light>();
            ll.type = LightType.Point; ll.color = new Color(1f, 0.6f, 0.3f); ll.range = 12f; ll.intensity = 3.0f; ll.shadows = LightShadows.None;
        }

        // crossed vertical quads (grass, leaves, tree crowns); normals point up so cards are lit softly from every side
        static Mesh CardsMesh(string name, int planes, bool centered, float width, float height)
        {
            string path = Root + "/Generated/" + name + ".asset";
            AssetDatabase.DeleteAsset(path);
            List<Vector3> v = new List<Vector3>(); List<Vector3> n = new List<Vector3>(); List<Vector2> uv = new List<Vector2>(); List<int> t = new List<int>();
            for (int p = 0; p < planes; p++)
            {
                float a = Mathf.PI * p / planes;
                Vector3 right = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (width * 0.5f);
                float y0 = centered ? -height * 0.5f : 0f, y1 = centered ? height * 0.5f : height;
                int b = v.Count;
                v.Add(-right + Vector3.up * y0); v.Add(right + Vector3.up * y0); v.Add(right + Vector3.up * y1); v.Add(-right + Vector3.up * y1);
                for (int i = 0; i < 4; i++) n.Add(Vector3.up);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            Mesh m = new Mesh { name = name };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ rig
        static DARig BuildRig(Transform parent, Material fadeMat, Material vigMat, Material grainMat, Material reticleMat, Material ringMat, Material rayMat,
                              Material metalMat, Material flameMat, out DALightFlicker heldFlame, out GameObject heldLighter, out GameObject heldBranch)
        {
            GameObject rigGo = new GameObject("DA Rig");
            rigGo.transform.position = new Vector3(0f, 1.15f, -300f);
            DARig rig = rigGo.AddComponent<DARig>();
            GameObject ts = new GameObject("TrackingSpace");
            ts.transform.SetParent(rigGo.transform, false);
            GameObject headGo = new GameObject("Head");
            headGo.transform.SetParent(ts.transform, false);
            headGo.tag = "MainCamera";
            Camera cam = headGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 400f; cam.fieldOfView = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            headGo.AddComponent<AudioListener>();

            Transform lh = MakeController("LeftHand", ts.transform, metalMat);
            Transform rh = MakeController("RightHand", ts.transform, metalMat);

            // overlays on the head
            Renderer fade = HeadQuad("Fade", headGo.transform, new Vector3(0f, 0f, 0.22f), new Vector3(1.6f, 1.6f, 1f), fadeMat);
            Renderer vig = HeadQuad("Vignette", headGo.transform, new Vector3(0f, 0f, 0.3f), new Vector3(0.9f, 0.9f, 1f), vigMat);
            Renderer grain = HeadQuad("Grain", headGo.transform, new Vector3(0f, 0f, 0.27f), new Vector3(1.3f, 1.3f, 1f), grainMat);
            grain.gameObject.SetActive(false);   // the static-like grain is disabled (also in edit mode)
            grainMat.mainTexture.wrapMode = TextureWrapMode.Repeat;
            grainMat.SetTextureScale("_MainTex", new Vector2(3f, 3f));

            // pointer ray + reticle
            GameObject rayGo = new GameObject("PointerRay");
            rayGo.transform.SetParent(rigGo.transform, false);
            LineRenderer lr = rayGo.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.widthMultiplier = 0.0035f; lr.useWorldSpace = true; lr.sharedMaterial = rayMat;
            lr.shadowCastingMode = ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.alignment = LineAlignment.View;
            GameObject ret = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ret.name = "Reticle"; Strip(ret);
            ret.transform.SetParent(rigGo.transform, false);
            ret.GetComponent<Renderer>().sharedMaterial = reticleMat;
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ring.name = "DwellRing"; Strip(ring);
            ring.transform.SetParent(ret.transform, false);
            ring.GetComponent<Renderer>().sharedMaterial = ringMat;

            // held items (lighter + thorn branch) follow the pointer
            GameObject held = new GameObject("HeldRoot");
            held.transform.SetParent(rigGo.transform, false);
            GameObject lighter = new GameObject("HeldLighter");
            lighter.transform.SetParent(held.transform, false);
            Material erawanHeld = Lit("erawan", Tex("Procedural/erawan_albedo.png"), Tex("Procedural/erawan_normal.png"), 0f, 0.55f, Color.white);
            GameObject heldCandle = MakeErawanCandle("HeldCandle", lighter.transform, new Vector3(0f, -0.11f, 0.2f), 0.22f, erawanHeld);
            GameObject flame = MakeFlame("HeldFlame", lighter.transform, flameMat, new Vector3(0f, 0.112f, 0.2f));
            Light fl = flame.AddComponent<Light>();
            fl.type = LightType.Point; fl.color = new Color(1f, 0.65f, 0.3f); fl.range = 3.2f; fl.intensity = 1.2f; fl.shadows = LightShadows.None;
            DALightFlicker flick = flame.AddComponent<DALightFlicker>();
            flick.target = fl; flick.baseIntensity = 1.3f; flick.amount = 0.35f; flick.on = false; flick.fadeSpeed = 10f;
            flick.SetFlameBase(0.04f, 0.075f);
            flick.flameVisual = flame.transform.GetChild(0).gameObject;
            GameObject branch = Place("Branch", "branch", "HeldBranch", held.transform, new Vector3(0.1f, -0.16f, 0.12f), Vector3.zero, 0.7f);
            if (branch == null) { branch = new GameObject("HeldBranch"); branch.transform.SetParent(held.transform, false); }
            lighter.SetActive(false);
            branch.SetActive(false);

            rig.trackingSpace = ts.transform;
            rig.head = headGo.transform;
            rig.cam = cam;
            rig.leftHand = lh; rig.rightHand = rh;
            rig.rayLine = lr;
            rig.reticle = ret.transform; rig.reticleRenderer = ret.GetComponent<Renderer>();
            rig.reticleRing = ring.GetComponent<Renderer>();
            rig.fadeQuad = fade; rig.vignetteQuad = vig; rig.grainQuad = grain;
            rig.heldRoot = held.transform;
            rig.vigBaseScale = 1f;

            heldFlame = flick;
            heldLighter = lighter;
            heldBranch = branch;
            return rig;
        }

        static Transform MakeController(string name, Transform parent, Material m)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MakePart("Body", go.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.0f), new Vector3(0.045f, 0.035f, 0.12f), m);
            MakePart("Ring", go.transform, PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.05f), new Vector3(0.09f, 0.012f, 0.05f), m);
            go.SetActive(false);
            return go.transform;
        }

        static Renderer HeadQuad(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name; Strip(q);
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localScale = scale;
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        // ------------------------------------------------------------------ UI
        static DAUI BuildUI(Transform parent, DARig rig, Material subBg, Material btnA, Material btnB, Material tmpOverlay)
        {
            GameObject uiGo = new GameObject("DA_UI");
            uiGo.transform.SetParent(parent, false);
            DAUI ui = uiGo.AddComponent<DAUI>();
            ui.rig = rig;

            // subtitles
            GameObject sub = new GameObject("SubtitleRoot");
            sub.transform.SetParent(uiGo.transform, false);
            TextMeshPro st = MakeTmp("Subtitle", sub.transform, new Vector3(0f, 0f, 0f), 4.4f, new Vector2(1.7f, 0.4f), Color.white, tmpOverlay);
            Renderer sbg = HeadlessQuad("SubtitleBg", sub.transform, new Vector3(0f, 0f, 0.01f), new Vector3(1.7f, 0.2f, 1f), subBg);
            TextMeshPro ht = MakeTmp("Hint", sub.transform, new Vector3(0f, -0.12f, 0f), 3.0f, new Vector2(1.7f, 0.2f), new Color(1f, 0.9f, 0.65f), tmpOverlay);
            Renderer hbg = HeadlessQuad("HintBg", sub.transform, new Vector3(0f, -0.12f, 0.01f), new Vector3(1.7f, 0.1f, 1f), subBg);
            ui.subRoot = sub.transform; ui.subtitle = st; ui.subtitleBg = sbg; ui.hint = ht; ui.hintBg = hbg;
            st.alpha = 0f; ht.alpha = 0f; sbg.enabled = false; hbg.enabled = false;

            // centre panel
            GameObject cen = new GameObject("CenterRoot");
            cen.transform.SetParent(uiGo.transform, false);
            TextMeshPro ct = MakeTmp("CenterText", cen.transform, Vector3.zero, 4.6f, new Vector2(2.6f, 1.9f), Color.white, tmpOverlay);
            ct.alpha = 0f;
            ui.centerRoot = cen.transform; ui.center = ct;

            // choice panel
            GameObject ch = new GameObject("ChoiceRoot");
            ch.transform.SetParent(uiGo.transform, false);
            Material panelMat = Unlit("choicepanel", Tex("Procedural/dot.png"), new Color(0.03f, 0.02f, 0.02f, 0.82f), 8, 10, 4005);
            HeadlessQuad("Panel", ch.transform, new Vector3(0f, 0.08f, 0.02f), new Vector3(1.75f, 0.72f, 1f), panelMat);
            TextMeshPro q = MakeTmp("Question", ch.transform, new Vector3(0f, 0.24f, 0f), 4.4f, new Vector2(1.4f, 0.3f), Color.white, tmpOverlay);
            DAInteractable ba = MakeButton("ButtonA", ch.transform, new Vector3(-0.38f, -0.04f, 0f), btnA, tmpOverlay, out TextMeshPro ta);
            DAInteractable bb = MakeButton("ButtonB", ch.transform, new Vector3(0.38f, -0.04f, 0f), btnB, tmpOverlay, out TextMeshPro tb);
            ui.choiceRoot = ch.transform; ui.choiceQuestion = q; ui.choiceA = ba; ui.choiceB = bb; ui.choiceAText = ta; ui.choiceBText = tb;
            ch.SetActive(false);
            return ui;
        }

        static DAInteractable MakeButton(string name, Transform parent, Vector3 pos, Material bg, Material tmpOverlay, out TextMeshPro text)
        {
            GameObject b = new GameObject(name);
            b.transform.SetParent(parent, false);
            b.transform.localPosition = pos;
            Material frameMat = Unlit("btnFrame", Tex("Procedural/dot.png"), new Color(0.91f, 0.78f, 0.48f, 1f), 8, 10, 4007);
            HeadlessQuad("Frame", b.transform, new Vector3(0f, 0f, 0.002f), new Vector3(0.70f, 0.30f, 1f), frameMat);
            Renderer fill = HeadlessQuad("Fill", b.transform, new Vector3(0f, 0f, 0f), new Vector3(0.65f, 0.25f, 1f), bg);
            fill.gameObject.name = "Fill";
            text = MakeTmp("Label", b.transform, new Vector3(0f, 0f, -0.01f), 5.0f, new Vector2(0.62f, 0.24f), Color.white, tmpOverlay);
            text.alpha = 1f;
            text.fontStyle = FontStyles.Bold;
            BoxCollider bc = b.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.70f, 0.30f, 0.08f); bc.isTrigger = true;
            DAInteractable it = b.AddComponent<DAInteractable>();
            it.useDwell = false;
            return it;
        }

        static Renderer HeadlessQuad(string name, Transform parent, Vector3 pos, Vector3 scale, Material m)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name; Strip(q);
            q.transform.SetParent(parent, false);
            q.transform.localPosition = pos;
            q.transform.localScale = scale;
            Renderer r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return r;
        }

        static TextMeshPro MakeTmp(string name, Transform parent, Vector3 pos, float size, Vector2 rect, Color color, Material overlay)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            TextMeshPro t = go.AddComponent<TextMeshPro>();
            t.text = "";
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.enableAutoSizing = false;
            t.rectTransform.sizeDelta = rect;
            if (overlay != null) t.fontSharedMaterial = overlay;
            return t;
        }

        static Material MakeTmpOverlay()
        {
            string path = Root + "/Materials/TMP_Overlay.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Shader sh = Shader.Find("TextMeshPro/Distance Field Overlay");
            if (font == null || sh == null) { Debug.LogWarning("[DarkAwakening] TMP default font or overlay shader missing, subtitles may be hidden behind walls."); return null; }
            if (m == null)
            {
                m = new Material(font.material);
                m.shader = sh;
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = sh;
            m.renderQueue = 4010;
            // crisper, more legible subtitles: sharper SDF edge, slight bold, thin dark outline
            if (m.HasProperty("_Sharpness")) m.SetFloat("_Sharpness", 0.6f);
            if (m.HasProperty(ShaderUtilities.ID_FaceDilate)) m.SetFloat(ShaderUtilities.ID_FaceDilate, 0.12f);
            m.EnableKeyword("OUTLINE_ON");
            if (m.HasProperty(ShaderUtilities.ID_OutlineColor)) m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0f, 0f, 0f, 1f));
            if (m.HasProperty(ShaderUtilities.ID_OutlineWidth)) m.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.16f);
            if (m.HasProperty(ShaderUtilities.ID_OutlineSoftness)) m.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0.05f);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------ small builders
        static Transform Anchor(string name, Transform parent, Vector3 pos, float yaw)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return go.transform;
        }

        static Light NewLight(string name, Transform parent, LightType type, Color color, float intensity)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Light l = go.AddComponent<Light>();
            l.type = type; l.color = color; l.intensity = intensity;
            return l;
        }

        static GameObject MakeFlame(string name, Transform parent, Material flameMat, Vector3 localPos)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "FlameQuad"; Strip(q);
            q.transform.SetParent(holder.transform, false);
            q.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            q.transform.localScale = new Vector3(0.045f, 0.085f, 1f);
            q.GetComponent<Renderer>().sharedMaterial = flameMat;
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            q.AddComponent<DABillboard>();
            return holder;
        }

        static DAInteractable MakeInteractable(string name, Transform parent, Vector3 pos, Vector3 colSize, string label, Material glow, float markerSize, Vector3 markerOffset, bool startOff)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = colSize; bc.isTrigger = true;
            DAInteractable it = go.AddComponent<DAInteractable>();
            it.label = label;
            it.markerSize = markerSize;
            GameObject m = GameObject.CreatePrimitive(PrimitiveType.Quad);
            m.name = "Marker"; Strip(m);
            m.transform.SetParent(go.transform, false);
            m.transform.localPosition = markerOffset;
            m.GetComponent<Renderer>().sharedMaterial = glow;
            m.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            it.marker = m.transform; it.markerRenderer = m.GetComponent<Renderer>();
            GameObject lab = new GameObject("Label");
            lab.transform.SetParent(go.transform, false);
            lab.transform.localPosition = markerOffset + new Vector3(0f, 0.2f, 0f);
            TextMeshPro t = lab.AddComponent<TextMeshPro>();
            t.text = label; t.fontSize = 2.6f; t.alignment = TextAlignmentOptions.Center; t.color = new Color(1f, 0.92f, 0.7f);
            t.rectTransform.sizeDelta = new Vector2(0.8f, 0.2f);
            it.labelText = t;
            lab.SetActive(false);
            if (startOff) { bc.enabled = false; m.SetActive(false); }
            it.interactable = !startOff;
            return it;
        }

        static DARepelZone MakeZone(string name, Transform parent, Vector3 pos, Vector3 size, Vector3 peek, Vector3 facing, DARepelZone.Need need)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.size = size; bc.isTrigger = true;
            DARepelZone z = go.AddComponent<DARepelZone>();
            z.need = need; z.zoneCollider = bc; z.facing = facing;
            GameObject p = new GameObject(name + "_Peek");
            p.transform.SetParent(go.transform, true);
            p.transform.position = peek;
            z.peekPoint = p.transform;
            return z;
        }

        // Thai "Erawan" carved candle (yellow wax, blue band), built as a lathe mesh. Base at local origin, height h.
        static GameObject MakeErawanCandle(string name, Transform parent, Vector3 localPos, float h, Material m)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            const int seg = 28, rings = 40;
            float R = 0.0165f;
            Vector3[] vert = new Vector3[(seg + 1) * (rings + 1)];
            Vector2[] uv = new Vector2[vert.Length];
            for (int j = 0; j <= rings; j++)
            {
                float t = j / (float)rings;
                float r = R;
                if (t > 0.80f) { float k = (t - 0.80f) / 0.20f; r = R * (0.22f + 0.78f * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k))); }
                for (int i = 0; i <= seg; i++)
                {
                    float a = (i / (float)seg) * Mathf.PI * 2f;
                    int idx = j * (seg + 1) + i;
                    vert[idx] = new Vector3(Mathf.Cos(a) * r, t * h, Mathf.Sin(a) * r);
                    uv[idx] = new Vector2(i / (float)seg, t);
                }
            }
            int[] tri = new int[seg * rings * 6];
            int n = 0;
            for (int j = 0; j < rings; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a0 = j * (seg + 1) + i, b0 = a0 + 1, c0 = a0 + seg + 1, d0 = c0 + 1;
                    tri[n++] = a0; tri[n++] = c0; tri[n++] = b0;
                    tri[n++] = b0; tri[n++] = c0; tri[n++] = d0;
                }
            Mesh mesh = new Mesh();
            mesh.name = name + "_mesh";
            mesh.vertices = vert; mesh.uv = uv; mesh.triangles = tri;
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject MakePart(string name, Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name; Strip(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static void Strip(GameObject go)
        {
            Collider c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        static GameObject Box(string name, Transform parent, Vector3 worldCenterOrLocal, Vector3 size, Material m, float tile, bool localSpace = false)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = worldCenterOrLocal;
            Vector3 uvCentre = localSpace ? parent.TransformPoint(worldCenterOrLocal) : worldCenterOrLocal;
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = BoxMesh(size, uvCentre, tile);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            if (!localSpace) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // box mesh with world-scaled UVs (so planks keep their size on every wall)
        static Mesh BoxMesh(Vector3 size, Vector3 centre, float tile)
        {
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            List<Vector3> verts = new List<Vector3>();
            List<Vector3> norms = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            Vector3 h = size * 0.5f;
            float[] ext = { h.x, h.y, h.z };
            for (int a = 0; a < 3; a++)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    int ia = (a + 1) % 3, ib = (a + 2) % 3;
                    Vector3 n = axes[a] * s;
                    Vector3 u = axes[ia], v = axes[ib];
                    Vector3 c = n * ext[a];
                    float eu = ext[ia], ev = ext[ib];
                    Vector3[] p =
                    {
                        c - u * eu - v * ev,
                        c + u * eu - v * ev,
                        c + u * eu + v * ev,
                        c - u * eu + v * ev
                    };
                    int bi = verts.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        verts.Add(p[k]);
                        norms.Add(n);
                        Vector3 w = p[k] + centre;
                        // planks run along the longest dimension of the face
                        float du = Vector3.Dot(w, u) / tile;
                        float dv = Vector3.Dot(w, v) / tile;
                        uvs.Add(eu >= ev ? new Vector2(du, dv) : new Vector2(dv, du));
                    }
                    if (s > 0) { tris.AddRange(new[] { bi, bi + 1, bi + 2, bi, bi + 2, bi + 3 }); }
                    else { tris.AddRange(new[] { bi, bi + 2, bi + 1, bi, bi + 3, bi + 2 }); }
                }
            }
            Mesh m = new Mesh();
            m.name = "BoxMesh";
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        static Mesh TuftMesh()
        {
            string path = Root + "/Generated/TuftMesh.asset";
            Mesh m = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (m != null) return m;
            m = new Mesh();
            m.name = "TuftMesh";
            Vector3[] v =
            {
                new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 0.7f, 0f), new Vector3(-0.5f, 0.7f, 0f),
                new Vector3(0f, 0f, -0.5f), new Vector3(0f, 0f, 0.5f), new Vector3(0f, 0.7f, 0.5f), new Vector3(0f, 0.7f, -0.5f)
            };
            Vector2[] uv = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.vertices = v; m.uv = uv;
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            Vector3[] n = new Vector3[8];
            for (int i = 0; i < 8; i++) n[i] = Vector3.up;
            m.normals = n;
            m.RecalculateBounds();
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // ------------------------------------------------------------------ assets -> instances
        static GameObject Place(string asset, string file, string name, Transform parent, Vector3 pos, Vector3 euler, float scale)
        {
            string path = Root + "/Models/" + asset + "/" + file + ".fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { path = Root + "/Models/" + asset + "/" + file + ".obj"; prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); }
            GameObject go;
            if (prefab == null)
            {
                Debug.LogWarning("[DarkAwakening] Model missing: " + path + " (placeholder used)");
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Strip(go);
                go.transform.localScale = Vector3.one * 0.1f;
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.position = pos;
                return go;
            }
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(euler);
            go.transform.localScale = Vector3.one * scale;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = AssetMat(asset, mats[i] != null ? mats[i].name : "default");
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            return go;
        }

        static Material AssetMat(string asset, string matName)
        {
            string key = Regex.Replace(matName, @"\.\d{3}$", "");
            key = key.Replace(" (Instance)", "");
            string ck = asset + "_" + key;
            Material cached;
            if (cache.TryGetValue(ck, out cached)) return cached;

            string dir = Root + "/Textures/" + asset;
            Texture2D alb = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + key + "_albedo.jpg");
            bool cutout = false;
            if (alb == null)
            {
                alb = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + key + "_albedo_a.png");
                cutout = alb != null;
            }
            Texture2D nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "/" + key + "_normal.png");
            float metallic = 0f, smooth = 0.3f;
            Color col = new Color(0.5f, 0.5f, 0.5f);
            string pp = dir + "/" + key + "_params.txt";
            if (File.Exists(pp))
            {
                string[] f = File.ReadAllText(pp).Trim().Split(',');
                if (f.Length >= 5)
                {
                    float.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out metallic);
                    float.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out smooth);
                    float r, g, b;
                    float.TryParse(f[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out r);
                    float.TryParse(f[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out g);
                    float.TryParse(f[4], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out b);
                    col = new Color(r, g, b);
                }
            }
            Color tint = alb != null ? Color.white : col;
            bool twoSided = asset == "Krasue" || asset == "Branch" || asset == "Garland" || cutout;
            Material m = Lit(ck, alb, nrm, metallic, smooth, tint, cutout, twoSided);
            cache[ck] = m;
            return m;
        }

        // ------------------------------------------------------------------ materials
        static Material Lit(string name, Texture2D albedo, Texture2D normal, float metallic, float smooth, Color tint, bool cutout = false, bool twoSided = false)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_BaseMap", albedo);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
            m.SetFloat("_Cull", twoSided ? 0f : 2f);
            if (cutout)
            {
                m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2450;
            }
            else
            {
                m.SetFloat("_AlphaClip", 0f);
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = -1;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material Unlit(string name, Texture2D tex, Color color, int ztest, int dstBlend, int queue)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = Shader.Find("DarkAwakening/Unlit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetTexture("_MainTex", tex);
            m.SetColor("_Color", color);
            m.SetFloat("_ZTest", ztest);
            m.SetFloat("_DstBlend", dstBlend);
            m.renderQueue = queue;
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SaveMaterial(Material m, string name)
        {
            string path = Root + "/Materials/" + name + ".mat";
            Material ex = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (ex == null) AssetDatabase.CreateAsset(m, path);
            else { ex.shader = m.shader; ex.CopyPropertiesFromMaterial(m); EditorUtility.SetDirty(ex); }
        }

        static Texture2D Tex(string rel) { return AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + rel); }
        static AudioClip Clip(string n)
        {
            // sound-bank files (sb_*) take priority over the generated placeholders
            string[] tries = { Root + "/Audio/sb_" + n + ".wav", Root + "/Audio/sb_" + n + ".ogg", Root + "/Audio/" + n + ".wav" };
            foreach (string t in tries) { AudioClip c = AssetDatabase.LoadAssetAtPath<AudioClip>(t); if (c != null) return c; }
            string legacy = n == "night" ? "amb_night" : n == "birds" ? "birds_dawn" : n == "scream" ? "krasue_scream" : n == "growl" ? "krasue_growl" : n == "whisper" ? "krasue_whisper" : null;
            return legacy != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + legacy + ".wav") : null;
        }

        // ------------------------------------------------------------------ importers
        static void ConfigureImporters()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    TextureImporter ti = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (ti == null) continue;
                    string fn = Path.GetFileName(p).ToLowerInvariant();
                    bool changed = false;
                    TextureImporterType want = fn.EndsWith("_normal.png") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    if (ti.textureType != want) { ti.textureType = want; changed = true; }
                    bool procedural = p.Contains("/Procedural/");
                    bool clamp = procedural && !(fn.StartsWith("planks") || fn.StartsWith("dark_wood") || fn.StartsWith("table_wood") || fn.StartsWith("ground") || fn.StartsWith("grain") || fn.StartsWith("bark") || fn.StartsWith("bamboo_stalk"));
                    TextureWrapMode wm = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                    if (ti.wrapMode != wm) { ti.wrapMode = wm; changed = true; }
                    bool alpha = fn.EndsWith("_albedo_a.png") || (procedural && (fn.StartsWith("glow") || fn.StartsWith("dot") || fn.StartsWith("flame") || fn.StartsWith("ring") || fn.StartsWith("vignette") || fn.StartsWith("moon") || fn.StartsWith("grass") || fn.StartsWith("grain") || fn.StartsWith("leaf") || fn.StartsWith("banana") || fn.StartsWith("bamboo_leaf") || fn.StartsWith("krasue_face")));
                    if (want == TextureImporterType.Default && ti.alphaIsTransparency != alpha) { ti.alphaIsTransparency = alpha; changed = true; }
                    if (!ti.mipmapEnabled && !(procedural && clamp)) { ti.mipmapEnabled = true; changed = true; }
                    if (ti.anisoLevel < 4 && !clamp) { ti.anisoLevel = 4; changed = true; }
                    if (changed) ti.SaveAndReimport();
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    ModelImporter mi = AssetImporter.GetAtPath(p) as ModelImporter;
                    if (mi == null) continue;
                    bool krasue = p.EndsWith("/krasue.fbx");
                    bool changed = false;
                    if (mi.importCameras) { mi.importCameras = false; changed = true; }
                    if (mi.importLights) { mi.importLights = false; changed = true; }
                    if (mi.importAnimation != krasue) { mi.importAnimation = krasue; changed = true; }
                    if (mi.isReadable) { mi.isReadable = false; changed = true; }
                    if (mi.meshCompression != ModelImporterMeshCompression.Off) { mi.meshCompression = ModelImporterMeshCompression.Off; changed = true; }
                    if (krasue)
                    {
                        if (mi.animationType != ModelImporterAnimationType.Generic) { mi.animationType = ModelImporterAnimationType.Generic; changed = true; }
                        ModelImporterClipAnimation[] defs = mi.defaultClipAnimations;
                        if (defs != null && defs.Length > 0)
                        {
                            bool need = mi.clipAnimations == null || mi.clipAnimations.Length != defs.Length;
                            if (need)
                            {
                                foreach (ModelImporterClipAnimation d in defs)
                                    d.loopTime = (d.name == "idle" || d.name == "walk" || d.name == "run");
                                mi.clipAnimations = defs;
                                changed = true;
                            }
                        }
                    }
                    if (changed) mi.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        static RuntimeAnimatorController BuildKrasueController()
        {
            string path = Root + "/Generated/Krasue.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(Root + "/Models/Krasue/krasue.fbx");
            AnimationClip[] clips = all.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            string[] wanted = { "idle", "walk", "run", "attack", "Attack1", "Hit", "death" };
            AnimatorState first = null;
            foreach (string w in wanted)
            {
                AnimationClip clip = clips.FirstOrDefault(c => c.name == w);
                if (clip == null) continue;
                AnimatorState st = sm.AddState(w);
                st.motion = clip;
                if (first == null) first = st;
            }
            if (first != null) sm.defaultState = first;
            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }

        // ------------------------------------------------------------------ project plumbing
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;
            try
            {
                SerializedObject tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                SerializedProperty layers = tm.FindProperty("layers");
                for (int i = 8; i < 32; i++)
                {
                    SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(sp.stringValue))
                    {
                        sp.stringValue = layerName;
                        tm.ApplyModifiedProperties();
                        return i;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DarkAwakening] Could not create layer: " + e.Message); }
            return -1;
        }
    }
}
#endif
