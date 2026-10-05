using UnityEngine;

/// <summary>
/// Infinite vertical-scrolling layer with parallax. Attach to a GameObject with
/// a SpriteRenderer. The script clones the sprite into 3 tiles and scrolls them
/// at a fraction of the camera's speed so they loop seamlessly.
///
/// parallaxFactor 0 = locked to camera (no relative motion — static backdrop).
/// parallaxFactor 1 = stays in world space (scrolls past fast — max parallax).
/// Background: ~0.05.  Mid-ground neon signs: ~0.4.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    [Tooltip("0 = moves with camera. 1 = static in world (max parallax).")]
    [SerializeField] [Range(0f, 1f)] float parallaxFactor = 0.5f;

    const int TileCount = 3;

    Transform cam;
    float tileHeight;
    Transform[] tiles;

    void Start()
    {
        cam = Camera.main.transform;
        var sr = GetComponent<SpriteRenderer>();

        // Scale to fill screen width so edges aren't cut off on narrow phones
        var mainCam = Camera.main;
        float screenWidth = mainCam.orthographicSize * 2f * mainCam.aspect;
        float spriteWidth = sr.sprite.bounds.size.x;
        if (spriteWidth > 0f)
        {
            float fitScale = screenWidth / spriteWidth * 1.05f; // slight oversize to avoid edge gaps
            transform.localScale = new Vector3(fitScale, fitScale, 1f); // uniform scale preserves aspect ratio
        }

        tileHeight = sr.bounds.size.y; // use scaled bounds for correct tiling

        tiles = new Transform[TileCount];
        tiles[0] = transform;

        // Create extra tiles for seamless vertical tiling
        for (int i = 1; i < TileCount; i++)
        {
            var go = new GameObject($"{name}_tile{i}");
            go.transform.SetParent(transform.parent);
            go.transform.localScale = transform.localScale;

            var tileSR = go.AddComponent<SpriteRenderer>();
            tileSR.sprite           = sr.sprite;
            tileSR.sortingOrder     = sr.sortingOrder;
            tileSR.sortingLayerName = sr.sortingLayerName;
            tileSR.color            = sr.color;

            tiles[i] = go.transform;
        }
    }

    void LateUpdate()
    {
        if (cam == null) return;

        float camY = cam.position.y;
        float x    = cam.position.x;
        float z    = transform.position.z;

        // How far the layer "falls behind" the camera
        float scroll = camY * parallaxFactor;

        // Wrap so tiles repeat endlessly
        float offset = Mathf.Repeat(scroll, tileHeight);

        // Bottom tile sits just below the camera; stack the others above it
        float baseY = camY - offset - tileHeight;

        for (int i = 0; i < TileCount; i++)
            tiles[i].position = new Vector3(x, baseY + i * tileHeight, z);
    }
}
