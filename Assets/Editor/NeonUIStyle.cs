using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Neon look for the menus: rounded neon buttons (Unity's built-in rounded sprite,
/// tinted, with a lighter rim) and Monoton titles with a pink glow.
/// Used by SceneBuilder for new scenes and by SceneUpdater for the existing one.
/// </summary>
public static class NeonUIStyle
{
    const string FontPath     = "Assets/Art/Fonts/Monoton-Regular.ttf";
    const string FontAssetPath = "Assets/Art/Fonts/Monoton SDF.asset";
    const string GlowMatPath  = "Assets/Art/Fonts/Monoton SDF Neon Glow.mat";
    const string TitleChars   = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 !?.,:'-&";

    public static readonly Color Primary   = new Color(1.00f, 0.18f, 0.62f);   // hot pink
    public static readonly Color Secondary = new Color(0.45f, 0.20f, 0.95f);   // electric violet
    public static readonly Color TitleGlow = new Color(1.00f, 0.25f, 0.75f);

    // Buttons that start or continue play get the primary colour
    static readonly string[] PrimaryButtons = { "PlayButton", "RestartButton", "ResumeButton" };

    /// <summary>Restyles every button, title and slider under the given canvas.</summary>
    public static void Apply(GameObject canvas)
    {
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        var (font, glow) = GetOrCreateTitleFont();

        foreach (var button in canvas.GetComponentsInChildren<Button>(true))
            StyleButton(button, sprite, System.Array.IndexOf(PrimaryButtons, button.name) >= 0 ? Primary : Secondary);

        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true))
            if (t.name == "TitleText") StyleTitle(t, font, glow);

        foreach (var slider in canvas.GetComponentsInChildren<Slider>(true))
            if (slider.fillRect && slider.fillRect.TryGetComponent(out Image fill))
            {
                Undo.RecordObject(fill, "Neon UI");
                fill.color = Primary;
            }
    }

    static void StyleButton(Button button, Sprite sprite, Color color)
    {
        var img = button.targetGraphic as Image;
        if (img == null) return;

        Undo.RecordObject(img, "Neon UI");
        img.sprite = sprite;
        img.type   = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.3f;   // enlarges the built-in sprite's small corners into a rounded pill

        // On-screen move buttons sit over the play area, so keep them see-through
        bool overGameplay = button.GetComponent<HoldButton>() != null;
        float alpha = overGameplay ? 0.4f : 1f;
        img.color = new Color(color.r, color.g, color.b, alpha);

        // Lighter rim around the button for the neon-tube edge
        if (!button.TryGetComponent(out Outline rim))
            rim = Undo.AddComponent<Outline>(button.gameObject);
        Undo.RecordObject(rim, "Neon UI");
        var rimColor = Color.Lerp(color, Color.white, 0.55f);
        rimColor.a = overGameplay ? 0.6f : 1f;
        rim.effectColor    = rimColor;
        rim.effectDistance = new Vector2(4f, -4f);

        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            Undo.RecordObject(label, "Neon UI");
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
        }
        EditorUtility.SetDirty(button.gameObject);
    }

    static void StyleTitle(TMP_Text t, TMP_FontAsset font, Material glow)
    {
        Undo.RecordObject(t, "Neon UI");
        Undo.RecordObject(t.rectTransform, "Neon UI");

        float size = t.fontSize;
        t.font = font;
        t.fontSharedMaterial = glow;
        t.color = Color.white;
        t.fontStyle = FontStyles.UpperCase;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true;          // Monoton is wide; shrink rather than wrap or clip
        t.fontSizeMax = size;
        t.fontSizeMin = 30f;
        t.extraPadding = true;              // room for the glow
        t.rectTransform.sizeDelta = new Vector2(1000f, t.rectTransform.sizeDelta.y);
        EditorUtility.SetDirty(t);
    }

    /// <summary>Builds (once) a static TMP font asset for Monoton plus a glow material preset.</summary>
    static (TMP_FontAsset, Material) GetOrCreateTitleFont()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (fontAsset == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (source == null)
            {
                Debug.LogError($"[NeonUIStyle] Font not found at {FontPath}");
                return (null, null);
            }

            fontAsset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                                                      AtlasPopulationMode.Dynamic, false);
            fontAsset.name = "Monoton SDF";
            if (!fontAsset.TryAddCharacters(TitleChars, out string missing))
                Debug.LogWarning($"[NeonUIStyle] Monoton is missing glyphs: {missing}");
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;   // baked: no font file needed at runtime

            AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
            fontAsset.atlasTexture.name = "Monoton SDF Atlas";
            fontAsset.material.name     = "Monoton SDF Material";
            AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            AssetDatabase.SaveAssets();
        }

        var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
        if (glow == null)
        {
            glow = new Material(fontAsset.material) { name = "Monoton SDF Neon Glow" };
            glow.shader = Shader.Find("TextMeshPro/Distance Field");   // the mobile shader has no glow
            glow.SetTexture("_MainTex", fontAsset.atlasTexture);
            glow.EnableKeyword("GLOW_ON");
            glow.SetColor("_GlowColor", TitleGlow);
            glow.SetFloat("_GlowOffset", 0f);
            glow.SetFloat("_GlowInner", 0.1f);
            glow.SetFloat("_GlowOuter", 0.6f);
            glow.SetFloat("_GlowPower", 0.85f);
            AssetDatabase.CreateAsset(glow, GlowMatPath);
            AssetDatabase.SaveAssets();
        }
        return (fontAsset, glow);
    }
}
