using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Jump")]
    [SerializeField] float jumpForce = 16f;
    [SerializeField] float boostJumpForce = 23f;

    [Header("Horizontal Movement")]
    [SerializeField] float tiltMultiplier = 22f;
    [SerializeField] float maxSpeed = 9f;

    [Header("Screen Wrap")]
    [SerializeField] bool enableWrap = true;

    Rigidbody2D rb;
    PlayerAnimator anim;
    float halfScreenWidth;
    bool alive = true;
    int platformLayer;

    public float CurrentHeight => transform.position.y;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<PlayerAnimator>();
        rb.gravityScale = 2.5f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        var cam = Camera.main;
        halfScreenWidth = cam.orthographicSize * cam.aspect;
        platformLayer = LayerMask.NameToLayer("Platform");
    }

    void FixedUpdate()
    {
        if (!alive || GameManager.Instance?.State != GameManager.GameState.Playing) return;

        MoveHorizontal();

        if (platformLayer >= 0)
            Physics2D.IgnoreLayerCollision(gameObject.layer, platformLayer, rb.linearVelocity.y > 0.1f);

        if (enableWrap) WrapScreen();
    }

    void MoveHorizontal()
    {
        float tilt;

#if UNITY_EDITOR || UNITY_STANDALONE
        // Keyboard fallback so the game is testable in the editor
        tilt = Input.GetAxis("Horizontal");
#else
        tilt = Mathf.Clamp(Input.acceleration.x, -1f, 1f);
#endif

        float sens = SettingsManager.Instance != null ? SettingsManager.Instance.TiltSensitivity : tiltMultiplier;
        float vx = Mathf.Clamp(tilt * sens, -maxSpeed, maxSpeed);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
    }

    void WrapScreen()
    {
        Vector3 p = transform.position;
        float edge = halfScreenWidth + 0.5f;

        if (p.x > edge)
            transform.position = new Vector3(-halfScreenWidth + 0.1f, p.y, p.z);
        else if (p.x < -edge)
            transform.position = new Vector3(halfScreenWidth - 0.1f, p.y, p.z);
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (!alive) return;
        // Ignore collisions while moving upward
        if (rb.linearVelocity.y > 0.1f) return;

        foreach (ContactPoint2D contact in col.contacts)
        {
            // Only react when landing on top of a surface
            if (contact.normal.y > 0.5f)
            {
                if (col.gameObject.CompareTag("HazardPlatform"))
                {
                    AudioManager.Instance?.Play(AudioManager.SFX.Hazard);
                    Die();
                    return;
                }

                bool isBounce = col.gameObject.CompareTag("BouncePlatform");
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, isBounce ? boostJumpForce : jumpForce);

                AudioManager.Instance?.Play(isBounce ? AudioManager.SFX.BounceBoost : AudioManager.SFX.Jump);
                col.gameObject.GetComponent<PlatformBase>()?.OnPlayerLanded();
                anim?.OnLand();
                return;
            }
        }
    }

    public void Die()
    {
        if (!alive) return;
        alive = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        anim?.OnDeath();
        GameManager.Instance?.TriggerGameOver();
    }
}
