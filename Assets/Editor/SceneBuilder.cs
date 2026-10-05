using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.Events;
using TMPro;

/// <summary>
/// Hoppy Heels > Build Skeleton Scene
///
/// Builds the full scene hierarchy, creates platform prefabs, adds and
/// configures all components, wires all cross-references, and creates the UI.
/// </summary>
public static class SceneBuilder
{
    const int PlatformLayer = 6;

    // -----------------------------------------------------------------------
    //  Entry point
    // -----------------------------------------------------------------------

    [MenuItem("Hoppy Heels/Build Skeleton Scene")]
    static void Build()
    {
        EnsureTags("Player", "BouncePlatform", "HazardPlatform", "Gem");
        EnsureLayer("Platform", PlatformLayer);

        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");

        var staticPrefab    = GetOrCreatePlatformPrefab("StaticPlatform",    typeof(PlatformBase),       new Color(0.55f, 0.35f, 0.15f), null);
        var movingPrefab    = GetOrCreatePlatformPrefab("MovingPlatform",    typeof(MovingPlatform),      new Color(0.3f,  0.55f, 0.3f),  null);
        var breakablePrefab = GetOrCreatePlatformPrefab("BreakablePlatform", typeof(BreakablePlatform),   new Color(0.7f,  0.55f, 0.2f),  null);
        var gemPrefab       = GetOrCreateGemPrefab();

        var gameManagerGO = CreateGameManagerGO();
        var audioManagerGO = CreateAudioManagerGO();
        var playerGO      = CreatePlayerGO();
        var _             = SetupCamera(playerGO);
        var spawnerGO     = CreatePlatformSpawner(staticPrefab, movingPrefab, breakablePrefab, gemPrefab);
        CreateBackground();
        var ui = CreateUI(gameManagerGO);

        WireGameManagerEvents(gameManagerGO, ui);
        CreateEventSystem();

        // Mark the scene dirty so Unity prompts a save
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Selection.activeGameObject = gameManagerGO;

        Debug.Log(
            "[HoppyHeels] Scene built successfully!\n" +
            "Platform prefabs saved to Assets/Prefabs/.\n" +
            "Optional: assign AudioClips to AudioManager, then save the scene (Ctrl+S).");
    }

    // -----------------------------------------------------------------------
    //  Tags
    // -----------------------------------------------------------------------

    static void EnsureTags(params string[] tags)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tagsProp = tagManager.FindProperty("tags");

        foreach (string tag in tags)
        {
            bool exists = false;
            for (int i = 0; i < tagsProp.arraySize; i++)
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    { exists = true; break; }

            if (!exists)
            {
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            }
        }

        tagManager.ApplyModifiedProperties();
    }

    static void EnsureLayer(string name, int index)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        var slot = layers.GetArrayElementAtIndex(index);
        if (string.IsNullOrEmpty(slot.stringValue))
        {
            slot.stringValue = name;
            tagManager.ApplyModifiedProperties();
        }
    }

    // -----------------------------------------------------------------------
    //  GameManager + ScoreManager
    // -----------------------------------------------------------------------

    static GameObject CreateGameManagerGO()
    {
        var go = NewGO("GameManager");
        go.AddComponent<GameManager>();
        go.AddComponent<ScoreManager>();
        go.AddComponent<SettingsManager>();
        return go;
    }

    // -----------------------------------------------------------------------
    //  AudioManager
    // -----------------------------------------------------------------------

    static GameObject CreateAudioManagerGO()
    {
        var go = NewGO("AudioManager");
        go.AddComponent<AudioManager>();
        return go;
    }

    // -----------------------------------------------------------------------
    //  Player
    // -----------------------------------------------------------------------

    static GameObject CreatePlayerGO()
    {
        // Re-use existing prefab so manual edits (sprite, collider size) survive rebuilds
        var prefab = GetOrCreatePlayerPrefab();
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.name = "Player";
        go.transform.position = Vector3.zero;

        // Ensure PlayerAnimator is present (may be missing on older prefabs)
        if (!go.GetComponent<PlayerAnimator>())
            go.AddComponent<PlayerAnimator>();

        Undo.RegisterCreatedObjectUndo(go, "Create Player");
        return go;
    }

    static GameObject GetOrCreatePlayerPrefab()
    {
        const string path = "Assets/Prefabs/Player.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject("Player");
        go.tag = "Player";

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SavePlaceholderSprite("Player", 32, 32, new Color(0.85f, 0.65f, 0.35f));
        sr.sortingOrder = 1;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2.5f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.6f, 0.8f);

        go.AddComponent<PlayerController>();
        go.AddComponent<PlayerAnimator>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // -----------------------------------------------------------------------
    //  Camera
    // -----------------------------------------------------------------------

    static GameObject SetupCamera(GameObject player)
    {
        var camGO = Camera.main?.gameObject;
        if (camGO == null)
        {
            camGO = NewGO("Main Camera");
            camGO.tag = "MainCamera";
            camGO.AddComponent<Camera>();
            camGO.AddComponent<AudioListener>();
        }

        var cam = camGO.GetComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = 7f;
        cam.backgroundColor  = new Color(0.18f, 0.12f, 0.08f); // dark roast bg
        camGO.transform.position = new Vector3(0f, 0f, -10f);

        // Remove stale CameraFollow if the scene is being rebuilt
        var old = camGO.GetComponent<CameraFollow>();
        if (old != null) Object.DestroyImmediate(old);

        var follow = camGO.AddComponent<CameraFollow>();
        SetRef(follow, "target", player.transform);

        return camGO;
    }

    // -----------------------------------------------------------------------
    //  Background + Parallax
    // -----------------------------------------------------------------------

    static void CreateBackground()
    {
        var bgPrefab = GetOrCreateBackgroundPrefab("Background",
            new Color(0.08f, 0.02f, 0.14f),   // deep purple (bottom)
            new Color(0.02f, 0.01f, 0.04f),   // near-black  (top)
            -20, 0.05f);
        var bgGO = (GameObject)PrefabUtility.InstantiatePrefab(bgPrefab);
        bgGO.name = "Background";
        Undo.RegisterCreatedObjectUndo(bgGO, "Create Background");

        var neonPrefab = GetOrCreateBackgroundPrefab("NeonSigns",
            new Color(0.9f, 0.2f, 0.8f, 0.15f),   // faint magenta (bottom)
            new Color(0.1f, 0.4f, 0.9f, 0.10f),   // faint blue    (top)
            -10, 0.4f);
        var neonGO = (GameObject)PrefabUtility.InstantiatePrefab(neonPrefab);
        neonGO.name = "NeonSigns";
        Undo.RegisterCreatedObjectUndo(neonGO, "Create NeonSigns");
    }

    static GameObject GetOrCreateBackgroundPrefab(string name, Color bottomColor, Color topColor,
        int sortingOrder, float parallaxFactor)
    {
        string path = $"Assets/Prefabs/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject(name);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite       = SaveGradientSprite(name, 512, 1024, bottomColor, topColor);
        sr.sortingOrder = sortingOrder;

        go.AddComponent<ParallaxLayer>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);

        // Wire parallaxFactor on the saved prefab asset (after the temp GO is gone)
        var prefabParallax = prefab.GetComponent<ParallaxLayer>();
        SetFloat(prefabParallax, "parallaxFactor", parallaxFactor);

        return prefab;
    }

    // -----------------------------------------------------------------------
    //  Platform Spawner + Pools
    // -----------------------------------------------------------------------

    static GameObject CreatePlatformSpawner(GameObject staticPrefab, GameObject movingPrefab,
        GameObject breakablePrefab, GameObject gemPrefab)
    {
        var spawnerGO = NewGO("PlatformSpawner");
        var spawner   = spawnerGO.AddComponent<PlatformSpawner>();

        var staticPool    = CreatePoolChild(spawnerGO, "StaticPool",    staticPrefab);
        var movingPool    = CreatePoolChild(spawnerGO, "MovingPool",    movingPrefab);
        var breakablePool = CreatePoolChild(spawnerGO, "BreakablePool", breakablePrefab);
        var gemPool       = CreatePoolChild(spawnerGO, "GemPool",       gemPrefab);

        SetRef(spawner, "staticPool",    staticPool);
        SetRef(spawner, "movingPool",    movingPool);
        SetRef(spawner, "breakablePool", breakablePool);
        SetRef(spawner, "gemPool",       gemPool);

        return spawnerGO;
    }

    static ObjectPool CreatePoolChild(GameObject parent, string name, GameObject prefab)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        var pool = go.AddComponent<ObjectPool>();
        SetRef(pool, "prefab", prefab);
        return pool;
    }

    // -----------------------------------------------------------------------
    //  Platform prefab creation
    // -----------------------------------------------------------------------

    static GameObject GetOrCreatePlatformPrefab(string name, System.Type platformScript, Color color, string tag)
    {
        string path = $"Assets/Prefabs/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject(name);

        if (tag != null) go.tag = tag;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SavePlaceholderSprite(name, 64, 16, color);
        sr.sortingOrder = 0;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(1.2f, 0.3f);

        go.layer = PlatformLayer;

        go.AddComponent(platformScript);

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject GetOrCreateGemPrefab()
    {
        const string path = "Assets/Prefabs/Gem.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject("Gem");
        go.tag = "Gem";

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SavePlaceholderSprite("Gem", 16, 16, new Color(0.2f, 0.85f, 0.95f));
        sr.sortingOrder = 2; // above platforms (0) and player (1)

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.3f;
        col.isTrigger = true;

        go.AddComponent<Collectible>();

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Sprite SavePlaceholderSprite(string name, int w, int h, Color color)
    {
        string texPath = $"Assets/Prefabs/{name}_tex.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        if (existing != null) return existing;

        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();

        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = Mathf.Min(w, h);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
    }

    /// <summary>
    /// Saves a vertical gradient PNG for background art. Skips if the file
    /// already exists so the user's custom art survives rebuilds.
    /// PPU is set so the sprite covers ~8 × 16 world units (fills the screen).
    /// </summary>
    static Sprite SaveGradientSprite(string name, int w, int h, Color bottomColor, Color topColor)
    {
        string texPath = $"Assets/Prefabs/{name}_tex.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
        if (existing != null) return existing;

        var tex    = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c = Color.Lerp(bottomColor, topColor, t);
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply();

        System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.textureType         = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 64;   // 512/64 = 8 units wide, 1024/64 = 16 units tall
        importer.wrapMode            = TextureWrapMode.Repeat;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(texPath);
    }

    // -----------------------------------------------------------------------
    //  UI Canvas + HUD + Panels
    // -----------------------------------------------------------------------

    struct UIRefs
    {
        public GameObject gameOverGO, mainMenuGO, pauseGO, pauseButtonGO, settingsGO;
    }

    static UIRefs CreateUI(GameObject gameManagerGO)
    {
        var gm = gameManagerGO.GetComponent<GameManager>();
        var sm = gameManagerGO.GetComponent<ScoreManager>();

        // Canvas
        var canvasGO = NewGO("UI Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        // ── HUD ─────────────────────────────────────────────────────────────
        var hudGO = ChildGO(canvasGO, "HUD");
        Stretch(hudGO);
        hudGO.AddComponent<SafeArea>();

        var scoreText = TMPLabel(hudGO, "ScoreText", "0", 72,
            anchor: new Vector2(0f, 1f),
            pivot:  new Vector2(0f, 1f),
            pos:    new Vector2(20f, -20f),
            size:   new Vector2(400f, 90f));
        scoreText.alignment = TextAlignmentOptions.TopLeft;
        scoreText.fontStyle  = FontStyles.Bold;

        var bestText = TMPLabel(hudGO, "BestText", "Best: 0", 30,
            anchor: new Vector2(1f, 1f),
            pivot:  new Vector2(1f, 1f),
            pos:    new Vector2(-20f, -20f),
            size:   new Vector2(300f, 50f));
        bestText.alignment = TextAlignmentOptions.TopRight;

        var hudGemsText = TMPLabel(hudGO, "HudGemsText", "Gems: 0", 30,
            anchor: new Vector2(0f, 1f),
            pivot:  new Vector2(0f, 1f),
            pos:    new Vector2(20f, -80f),
            size:   new Vector2(300f, 50f));
        hudGemsText.alignment = TextAlignmentOptions.TopLeft;

        // Pause button (child of HUD so SafeArea applies)
        var pauseButtonGO = ChildGO(hudGO, "PauseButton");
        var pbRT = pauseButtonGO.GetComponent<RectTransform>();
        pbRT.anchorMin = pbRT.anchorMax = new Vector2(1f, 1f);
        pbRT.pivot = new Vector2(0.5f, 0.5f);
        pbRT.anchoredPosition = new Vector2(-60f, -110f);
        pbRT.sizeDelta = new Vector2(80f, 80f);

        var pbImg = pauseButtonGO.AddComponent<Image>();
        pbImg.color = new Color(1f, 1f, 1f, 0.3f);

        var pbBtn = pauseButtonGO.AddComponent<Button>();
        pbBtn.targetGraphic = pbImg;
        UnityEventTools.AddPersistentListener(pbBtn.onClick,
            new UnityAction(gm.PauseGame));

        var pbLblGO = ChildGO(pauseButtonGO, "Label");
        var pbLbl   = pbLblGO.AddComponent<TextMeshProUGUI>();
        pbLbl.text      = "||";
        pbLbl.fontSize  = 40;
        pbLbl.fontStyle = FontStyles.Bold;
        pbLbl.color     = Color.white;
        pbLbl.alignment = TextAlignmentOptions.Center;
        var pbLblRT = pbLblGO.GetComponent<RectTransform>();
        pbLblRT.anchorMin = Vector2.zero;
        pbLblRT.anchorMax = Vector2.one;
        pbLblRT.sizeDelta = Vector2.zero;

        pauseButtonGO.AddComponent<PauseButton>();
        pauseButtonGO.SetActive(false);

        // ── Game Over Panel ─────────────────────────────────────────────────
        var gameOverGO = ChildGO(canvasGO, "GameOverPanel");
        Stretch(gameOverGO);

        var goBg = gameOverGO.AddComponent<Image>();
        goBg.color = new Color(0f, 0f, 0f, 0.78f);

        TMPLabel(gameOverGO, "TitleText", "Game Over", 80,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 260f),
            size:   new Vector2(700f, 110f))
            .alignment = TextAlignmentOptions.Center;

        var goScoreText = TMPLabel(gameOverGO, "ScoreText", "Score: 0", 52,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 140f),
            size:   new Vector2(600f, 75f));
        goScoreText.alignment = TextAlignmentOptions.Center;

        var goHighText = TMPLabel(gameOverGO, "HighScoreText", "Best: 0", 38,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 60f),
            size:   new Vector2(500f, 60f));
        goHighText.alignment = TextAlignmentOptions.Center;

        var goGemsText = TMPLabel(gameOverGO, "GemsText", "Gems: 0", 34,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, -10f),
            size:   new Vector2(400f, 50f));
        goGemsText.alignment = TextAlignmentOptions.Center;

        var goTotalGemsText = TMPLabel(gameOverGO, "TotalGemsText", "Total Gems: 0", 30,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, -60f),
            size:   new Vector2(400f, 45f));
        goTotalGemsText.alignment = TextAlignmentOptions.Center;

        var tryAgainBtn = CreateButton(gameOverGO, "RestartButton", "Try Again",
            pos: new Vector2(0f, -150f), size: new Vector2(320f, 90f));
        UnityEventTools.AddPersistentListener(tryAgainBtn.onClick,
            new UnityAction(gm.RestartGame));

        gameOverGO.AddComponent<GameOverPanel>();
        gameOverGO.SetActive(false);

        // ── Main Menu Panel ─────────────────────────────────────────────────
        var mainMenuGO = ChildGO(canvasGO, "MainMenuPanel");
        Stretch(mainMenuGO);

        var menuBg = mainMenuGO.AddComponent<Image>();
        menuBg.color = new Color(0.18f, 0.12f, 0.08f, 1f);

        TMPLabel(mainMenuGO, "TitleText", "HOPPY HEELS", 90,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 300f),
            size:   new Vector2(800f, 130f))
            .alignment = TextAlignmentOptions.Center;

        var menuHighText = TMPLabel(mainMenuGO, "HighScoreText", "Best: 0", 42,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 120f),
            size:   new Vector2(500f, 70f));
        menuHighText.alignment = TextAlignmentOptions.Center;

        var menuTotalGemsText = TMPLabel(mainMenuGO, "TotalGemsText", "Total Gems: 0", 34,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 50f),
            size:   new Vector2(500f, 50f));
        menuTotalGemsText.alignment = TextAlignmentOptions.Center;

        var playBtn = CreateButton(mainMenuGO, "PlayButton", "Play",
            pos: new Vector2(0f, -50f), size: new Vector2(320f, 100f));
        UnityEventTools.AddPersistentListener(playBtn.onClick,
            new UnityAction(gm.StartGame));

        // Settings button will be wired after SettingsPanel is created
        var menuSettingsBtn = CreateButton(mainMenuGO, "SettingsButton", "Settings",
            pos: new Vector2(0f, -180f), size: new Vector2(320f, 90f));

        mainMenuGO.AddComponent<MainMenuPanel>();
        // Starts active (visible on load)

        // ── Pause Panel ─────────────────────────────────────────────────────
        var pauseGO = ChildGO(canvasGO, "PausePanel");
        Stretch(pauseGO);

        var pauseBg = pauseGO.AddComponent<Image>();
        pauseBg.color = new Color(0f, 0f, 0f, 0.78f);

        TMPLabel(pauseGO, "TitleText", "Paused", 70,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 200f),
            size:   new Vector2(600f, 100f))
            .alignment = TextAlignmentOptions.Center;

        var resumeBtn = CreateButton(pauseGO, "ResumeButton", "Resume",
            pos: new Vector2(0f, 40f), size: new Vector2(320f, 90f));
        UnityEventTools.AddPersistentListener(resumeBtn.onClick,
            new UnityAction(gm.ResumeGame));

        var pauseRestartBtn = CreateButton(pauseGO, "RestartButton", "Restart",
            pos: new Vector2(0f, -70f), size: new Vector2(320f, 90f));
        UnityEventTools.AddPersistentListener(pauseRestartBtn.onClick,
            new UnityAction(gm.RestartGame));

        // Settings button will be wired after SettingsPanel is created
        var pauseSettingsBtn = CreateButton(pauseGO, "SettingsButton", "Settings",
            pos: new Vector2(0f, -180f), size: new Vector2(320f, 90f));

        var menuBtn = CreateButton(pauseGO, "MenuButton", "Menu",
            pos: new Vector2(0f, -290f), size: new Vector2(320f, 90f));
        UnityEventTools.AddPersistentListener(menuBtn.onClick,
            new UnityAction(gm.QuitToMenu));

        pauseGO.AddComponent<PausePanel>();
        pauseGO.SetActive(false);

        // ── Settings Panel ──────────────────────────────────────────────────
        var settingsGO = ChildGO(canvasGO, "SettingsPanel");
        Stretch(settingsGO);

        var settingsBg = settingsGO.AddComponent<Image>();
        settingsBg.color = new Color(0.05f, 0.03f, 0.1f, 0.95f);

        TMPLabel(settingsGO, "TitleText", "Settings", 70,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(0f, 280f),
            size:   new Vector2(600f, 100f))
            .alignment = TextAlignmentOptions.Center;

        // Sound toggle row
        TMPLabel(settingsGO, "SoundLabel", "Sound", 40,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(-100f, 120f),
            size:   new Vector2(250f, 60f))
            .alignment = TextAlignmentOptions.MidlineRight;

        var soundToggleGO = ChildGO(settingsGO, "SoundToggle");
        var stRT = soundToggleGO.GetComponent<RectTransform>();
        stRT.anchorMin = stRT.anchorMax = new Vector2(0.5f, 0.5f);
        stRT.pivot = new Vector2(0.5f, 0.5f);
        stRT.anchoredPosition = new Vector2(80f, 120f);
        stRT.sizeDelta = new Vector2(80f, 50f);

        var toggleBgGO = ChildGO(soundToggleGO, "Background");
        var toggleBgRT = toggleBgGO.GetComponent<RectTransform>();
        toggleBgRT.anchorMin = Vector2.zero;
        toggleBgRT.anchorMax = Vector2.one;
        toggleBgRT.sizeDelta = Vector2.zero;
        var toggleBgImg = toggleBgGO.AddComponent<Image>();
        toggleBgImg.color = new Color(0.3f, 0.3f, 0.3f);

        var checkGO = ChildGO(soundToggleGO, "Checkmark");
        var checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = Vector2.zero;
        checkRT.anchorMax = Vector2.one;
        checkRT.sizeDelta = new Vector2(-10f, -10f);
        var checkImg = checkGO.AddComponent<Image>();
        checkImg.color = new Color(0.4f, 0.9f, 0.4f);

        var soundToggle = soundToggleGO.AddComponent<Toggle>();
        soundToggle.targetGraphic = toggleBgImg;
        soundToggle.graphic = checkImg;
        soundToggle.isOn = true;

        // Sensitivity slider row
        TMPLabel(settingsGO, "SensLabel", "Tilt", 40,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(-100f, 20f),
            size:   new Vector2(250f, 60f))
            .alignment = TextAlignmentOptions.MidlineRight;

        var sliderGO = ChildGO(settingsGO, "SensitivitySlider");
        var slRT = sliderGO.GetComponent<RectTransform>();
        slRT.anchorMin = slRT.anchorMax = new Vector2(0.5f, 0.5f);
        slRT.pivot = new Vector2(0.5f, 0.5f);
        slRT.anchoredPosition = new Vector2(80f, 20f);
        slRT.sizeDelta = new Vector2(300f, 40f);

        // Slider background
        var sliderBgGO = ChildGO(sliderGO, "Background");
        var sliderBgRT = sliderBgGO.GetComponent<RectTransform>();
        sliderBgRT.anchorMin = new Vector2(0f, 0.25f);
        sliderBgRT.anchorMax = new Vector2(1f, 0.75f);
        sliderBgRT.sizeDelta = Vector2.zero;
        var sliderBgImg = sliderBgGO.AddComponent<Image>();
        sliderBgImg.color = new Color(0.3f, 0.3f, 0.3f);

        // Fill area
        var fillAreaGO = ChildGO(sliderGO, "Fill Area");
        var fillAreaRT = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRT.sizeDelta = Vector2.zero;

        var fillGO = ChildGO(fillAreaGO, "Fill");
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        var fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.9f, 0.6f, 0.2f);

        // Handle slide area
        var handleAreaGO = ChildGO(sliderGO, "Handle Slide Area");
        var handleAreaRT = handleAreaGO.GetComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.sizeDelta = Vector2.zero;

        var handleGO = ChildGO(handleAreaGO, "Handle");
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin = handleRT.anchorMax = Vector2.zero;
        handleRT.sizeDelta = new Vector2(30f, 40f);
        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = Color.white;

        var slider = sliderGO.AddComponent<Slider>();
        slider.targetGraphic = handleImg;
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.minValue = 10f;
        slider.maxValue = 40f;
        slider.value = 22f;

        // Sensitivity value label
        var sensValueText = TMPLabel(settingsGO, "SensValueText", "22", 36,
            anchor: new Vector2(0.5f, 0.5f),
            pos:    new Vector2(270f, 20f),
            size:   new Vector2(80f, 50f));
        sensValueText.alignment = TextAlignmentOptions.Center;

        // Back button
        var settingsBackBtn = CreateButton(settingsGO, "BackButton", "Back",
            pos: new Vector2(0f, -150f), size: new Vector2(260f, 80f));

        var settingsPanel = settingsGO.AddComponent<SettingsPanel>();
        SetRef(settingsPanel, "soundToggle", soundToggle);
        SetRef(settingsPanel, "sensitivitySlider", slider);
        SetRef(settingsPanel, "sensitivityValueText", sensValueText);
        settingsGO.SetActive(false);

        // Wire settings buttons to show/hide the panel
        UnityEventTools.AddPersistentListener(menuSettingsBtn.onClick,
            new UnityAction(settingsPanel.Show));
        UnityEventTools.AddPersistentListener(pauseSettingsBtn.onClick,
            new UnityAction(settingsPanel.Show));
        UnityEventTools.AddPersistentListener(settingsBackBtn.onClick,
            new UnityAction(settingsPanel.Hide));

        // ── Wire ScoreManager text refs ─────────────────────────────────────
        SetRef(sm, "scoreText",              scoreText);
        SetRef(sm, "highScoreText",          bestText);
        SetRef(sm, "hudGemsText",            hudGemsText);
        SetRef(sm, "gameOverScoreText",      goScoreText);
        SetRef(sm, "gameOverHighScoreText",  goHighText);
        SetRef(sm, "menuHighScoreText",      menuHighText);
        SetRef(sm, "gameOverGemsText",       goGemsText);
        SetRef(sm, "menuTotalGemsText",      menuTotalGemsText);
        SetRef(sm, "gameOverTotalGemsText",  goTotalGemsText);

        return new UIRefs
        {
            gameOverGO    = gameOverGO,
            mainMenuGO    = mainMenuGO,
            pauseGO       = pauseGO,
            pauseButtonGO = pauseButtonGO,
            settingsGO    = settingsGO
        };
    }

    // -----------------------------------------------------------------------
    //  Event wiring
    // -----------------------------------------------------------------------

    static void WireGameManagerEvents(GameObject gameManagerGO, UIRefs ui)
    {
        var gm    = gameManagerGO.GetComponent<GameManager>();
        var sm    = gameManagerGO.GetComponent<ScoreManager>();
        var goPanel    = ui.gameOverGO.GetComponent<GameOverPanel>();
        var menuPanel  = ui.mainMenuGO.GetComponent<MainMenuPanel>();
        var pausePanel = ui.pauseGO.GetComponent<PausePanel>();
        var pauseBtn   = ui.pauseButtonGO.GetComponent<PauseButton>();

        var settingsPanel = ui.settingsGO.GetComponent<SettingsPanel>();

        // onGameStarted
        UnityEventTools.AddPersistentListener(gm.onGameStarted,
            new UnityAction(menuPanel.Hide));
        UnityEventTools.AddPersistentListener(gm.onGameStarted,
            new UnityAction(settingsPanel.Hide));
        UnityEventTools.AddPersistentListener(gm.onGameStarted,
            new UnityAction(pauseBtn.Show));
        UnityEventTools.AddPersistentListener(gm.onGameStarted,
            new UnityAction(sm.StartTracking));

        // onGameOver
        UnityEventTools.AddPersistentListener(gm.onGameOver,
            new UnityAction(goPanel.Show));
        UnityEventTools.AddPersistentListener(gm.onGameOver,
            new UnityAction(pauseBtn.Hide));

        // onGamePaused
        UnityEventTools.AddPersistentListener(gm.onGamePaused,
            new UnityAction(pausePanel.Show));
        UnityEventTools.AddPersistentListener(gm.onGamePaused,
            new UnityAction(pauseBtn.Hide));

        // onGameResumed
        UnityEventTools.AddPersistentListener(gm.onGameResumed,
            new UnityAction(pausePanel.Hide));
        UnityEventTools.AddPersistentListener(gm.onGameResumed,
            new UnityAction(settingsPanel.Hide));
        UnityEventTools.AddPersistentListener(gm.onGameResumed,
            new UnityAction(pauseBtn.Show));
    }

    // -----------------------------------------------------------------------
    //  EventSystem
    // -----------------------------------------------------------------------

    static void CreateEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;
        var go = NewGO("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    // -----------------------------------------------------------------------
    //  UI helpers
    // -----------------------------------------------------------------------

    static TextMeshProUGUI TMPLabel(GameObject parent, string name, string text,
        float fontSize, Vector2 anchor, Vector2 pos, Vector2 size,
        Vector2? pivot = null)
    {
        var go  = ChildGO(parent, name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = text;
        tmp.fontSize = fontSize;
        tmp.color    = Color.white;

        var rt        = go.GetComponent<RectTransform>();
        rt.anchorMin  = anchor;
        rt.anchorMax  = anchor;
        rt.pivot      = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta  = size;

        return tmp;
    }

    static Button CreateButton(GameObject parent, string name, string label,
        Vector2 pos, Vector2 size)
    {
        var go = ChildGO(parent, name);
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.9f, 0.6f, 0.2f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        // Label
        var lblGO = ChildGO(go, "Label");
        var tmp   = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = label;
        tmp.fontSize  = 40;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color     = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        var lblRT    = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.sizeDelta = Vector2.zero;

        return btn;
    }

    static void Stretch(GameObject go)
    {
        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    // -----------------------------------------------------------------------
    //  General helpers
    // -----------------------------------------------------------------------

    /// <summary>Create a root-level GO and register it with Undo.</summary>
    static GameObject NewGO(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    /// <summary>Create a child GO. Adds a RectTransform when the parent has one (UI hierarchy).</summary>
    static GameObject ChildGO(GameObject parent, string name)
    {
        var go = parent.GetComponent<RectTransform>() != null
            ? new GameObject(name, typeof(RectTransform))
            : new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    /// <summary>Set a serialized object-reference field by name.</summary>
    static void SetRef(Component comp, string field, Object value)
    {
        var so   = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[SceneBuilder] Field '{field}' not found on {comp.GetType().Name}");
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    /// <summary>Set a serialized float field by name.</summary>
    static void SetFloat(Component comp, string field, float value)
    {
        var so   = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[SceneBuilder] Field '{field}' not found on {comp.GetType().Name}");
            return;
        }
        prop.floatValue = value;
        so.ApplyModifiedProperties();
    }

    /// <summary>
    /// Creates a solid-colour Sprite at runtime for editor-mode placeholder visuals.
    /// Replace with real art before shipping.
    /// </summary>
    static Sprite CreatePlaceholder(int w, int h, Color color)
    {
        var tex    = new Texture2D(w, h);
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Mathf.Min(w, h));
    }
}
