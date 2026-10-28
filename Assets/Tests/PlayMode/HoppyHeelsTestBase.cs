using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Shared setup for play-mode tests: loads the real game scene before each test
/// and restores the player's saved data (high score, gems, settings) afterwards,
/// so running the suite never overwrites real progress.
/// </summary>
public abstract class HoppyHeelsTestBase
{
    protected const string SceneName = "SampleScene";

    static readonly string[] IntKeys   = { "HoppyHeels_HighScore", "HoppyHeels_TotalGems", "HoppyHeels_SoundOn", "HoppyHeels_ControlMode" };
    static readonly string[] FloatKeys = { "HoppyHeels_TiltSensitivity" };

    readonly Dictionary<string, int?>   savedInts   = new Dictionary<string, int?>();
    readonly Dictionary<string, float?> savedFloats = new Dictionary<string, float?>();

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SnapshotPrefs();
        SceneManager.LoadScene(SceneName);
        yield return null;   // Awake / Start
        yield return null;
    }

    [TearDown]
    public void RestorePrefs()
    {
        foreach (var kv in savedInts)
            if (kv.Value.HasValue) PlayerPrefs.SetInt(kv.Key, kv.Value.Value); else PlayerPrefs.DeleteKey(kv.Key);
        foreach (var kv in savedFloats)
            if (kv.Value.HasValue) PlayerPrefs.SetFloat(kv.Key, kv.Value.Value); else PlayerPrefs.DeleteKey(kv.Key);
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        ControlInput.PointerOverride = null;
    }

    void SnapshotPrefs()
    {
        savedInts.Clear(); savedFloats.Clear();
        foreach (var k in IntKeys)   savedInts[k]   = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k)   : (int?)null;
        foreach (var k in FloatKeys) savedFloats[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetFloat(k) : (float?)null;
    }

    // ── Scene access ─────────────────────────────────────────────────

    protected static GameManager GM => GameManager.Instance;

    protected static GameObject Player => GameObject.FindGameObjectWithTag("Player");

    protected static Rigidbody2D PlayerBody => Player.GetComponent<Rigidbody2D>();

    protected static CameraFollow Cam => Camera.main.GetComponent<CameraFollow>();

    protected static ObjectPool Pool(string name)
    {
        var go = GameObject.Find(name);
        Assert.IsNotNull(go, $"Pool '{name}' not found");
        return go.GetComponent<ObjectPool>();
    }

    /// <summary>Finds a UI object under the canvas by path, including inactive ones.</summary>
    protected static GameObject UI(string path)
    {
        var canvas = GameObject.Find("UI Canvas");
        Assert.IsNotNull(canvas, "UI Canvas not found");
        var t = canvas.transform.Find(path);
        Assert.IsNotNull(t, $"UI object '{path}' not found");
        return t.gameObject;
    }

    /// <summary>Every live platform currently managed by the spawner, sorted bottom to top.</summary>
    protected static List<PlatformBase> ActivePlatforms()
    {
        return new[] { "StaticPool", "MovingPool", "BreakablePool" }
            .SelectMany(n => Pool(n).GetComponentsInChildren<PlatformBase>(false))
            .OrderBy(p => p.transform.position.y)
            .ToList();
    }

    /// <summary>Reads a private [SerializeField] so tests follow the tuned scene values, not hard-coded numbers.</summary>
    protected static T Field<T>(object obj, string name)
    {
        var f = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(f, $"{obj.GetType().Name}.{name} not found");
        return (T)f.GetValue(obj);
    }

    /// <summary>Holds the player still (no gravity, no bounces) so it can be positioned precisely.</summary>
    protected static void FreezePlayer()
    {
        var rb = PlayerBody;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.linearVelocity = Vector2.zero;
    }

    protected static IEnumerator MovePlayerTo(Vector2 pos)
    {
        var rb = PlayerBody;
        rb.position = pos;
        Player.transform.position = pos;
        if (Time.timeScale > 0f) yield return new WaitForFixedUpdate();   // physics doesn't step while paused
        yield return null;
    }
}
