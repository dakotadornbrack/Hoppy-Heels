using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hoppy Heels > Update Existing Scene
///
/// Brings an already-built scene up to date with changes that SceneBuilder
/// makes for new scenes, without rebuilding (and duplicating) everything.
/// Safe to run more than once.
/// </summary>
public static class SceneUpdater
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Hoppy Heels/Update Existing Scene")]
    static void UpdateOpenScene()
    {
        if (Apply())
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    /// <summary>Command-line entry: -executeMethod SceneUpdater.UpdateSampleSceneBatch</summary>
    public static void UpdateSampleSceneBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (!Apply()) { EditorApplication.Exit(1); return; }
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneUpdater] SampleScene updated and saved.");
    }

    static bool Apply()
    {
        var gm = Object.FindFirstObjectByType<GameManager>();
        var canvas = GameObject.Find("UI Canvas");
        if (gm == null || canvas == null)
        {
            Debug.LogError("[SceneUpdater] GameManager or UI Canvas not found. Is this a Hoppy Heels scene?");
            return false;
        }

        // Android back button
        if (!gm.TryGetComponent(out BackButtonHandler back))
            back = Undo.AddComponent<BackButtonHandler>(gm.gameObject);
        var settings = canvas.GetComponentInChildren<SettingsPanel>(true);
        var so = new SerializedObject(back);
        so.FindProperty("settingsPanel").objectReferenceValue = settings;
        so.ApplyModifiedProperties();

        // Control modes (Tilt / Touch / Buttons) — before styling so new buttons get the neon look
        ControlsSetup.Apply(canvas, GameObject.FindGameObjectWithTag("Player"));

        NeonUIStyle.Apply(canvas);
        Debug.Log("[SceneUpdater] Back button handler, control modes + neon UI style applied.");
        return true;
    }
}
