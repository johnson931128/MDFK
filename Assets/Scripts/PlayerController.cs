using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float groundAcceleration = 60f;
    [SerializeField] private float groundDeceleration = 70f;
    [SerializeField] private float airAcceleration = 40f;
    [SerializeField] private float airDeceleration = 30f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 12f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.12f;
    [SerializeField, Range(0f, 1f)] private float jumpCutMultiplier = 0.5f;

    [Header("Gravity")]
    [SerializeField] private float lowJumpGravityMultiplier = 1.8f;
    [SerializeField] private float fallGravityMultiplier = 1.55f;
    [SerializeField] private float maxFallSpeed = 18f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.16f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Facing")]
    [SerializeField] private Transform facingMarker;
    [SerializeField] private float facingMarkerDistance = 0.46f;

    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Transform visualRoot;
    private Animator animator;
    private InputAction moveAction;
    private InputAction jumpAction;
    private float lastGroundedTime = float.NegativeInfinity;
    private float lastJumpPressedTime = float.NegativeInfinity;

    public bool IsGrounded { get; private set; }
    public bool IsFacingLeft { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        visualRoot ??= transform.Find("VisualRoot") ?? transform.Find("CharacterVisual");
        facingMarker ??= transform.Find("FacingMarker");

        if (inputActions == null)
        {
            Debug.LogError("PlayerController requires an InputActionAsset.", this);
            return;
        }

        moveAction = inputActions.FindAction("Player/Move", true);
        jumpAction = inputActions.FindAction("Player/Jump", true);
    }

    private void OnEnable()
    {
        inputActions?.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void Update()
    {
        IsGrounded = CheckGrounded();
        if (IsGrounded)
        {
            lastGroundedTime = Time.time;
        }

        if (jumpAction == null)
        {
            return;
        }

        if (jumpAction.WasPressedThisFrame())
        {
            lastJumpPressedTime = Time.time;
        }

        if (jumpAction.WasReleasedThisFrame() && body.linearVelocity.y > 0f)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.y * jumpCutMultiplier);
        }

        if (CanJump())
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
            lastGroundedTime = float.NegativeInfinity;
            lastJumpPressedTime = float.NegativeInfinity;
        }
    }

    private void FixedUpdate()
    {
        if (moveAction == null)
        {
            return;
        }

        float horizontalInput = moveAction.ReadValue<Vector2>().x;
        bool isAccelerating = !Mathf.Approximately(horizontalInput, 0f);
        bool isGrounded = IsGrounded;
        float acceleration = isGrounded
            ? (isAccelerating ? groundAcceleration : groundDeceleration)
            : (isAccelerating ? airAcceleration : airDeceleration);
        float targetSpeed = horizontalInput * moveSpeed;
        float newHorizontalSpeed = Mathf.MoveTowards(body.linearVelocity.x, targetSpeed, acceleration * Time.fixedDeltaTime);
        body.linearVelocity = new Vector2(newHorizontalSpeed, body.linearVelocity.y);

        if (horizontalInput != 0f)
        {
            bool facingLeft = horizontalInput < 0f;
            IsFacingLeft = facingLeft;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = facingLeft;
            }

            if (visualRoot != null)
            {
                SpriteRenderer visualRenderer = visualRoot.GetComponent<SpriteRenderer>();
                if (visualRenderer != null)
                {
                    visualRenderer.flipX = facingLeft;
                }
                else
                {
                    Vector3 visualScale = visualRoot.localScale;
                    visualScale.x = facingLeft ? -Mathf.Abs(visualScale.x) : Mathf.Abs(visualScale.x);
                    visualRoot.localScale = visualScale;
                }
            }

            if (facingMarker != null)
            {
                Vector3 markerPosition = facingMarker.localPosition;
                markerPosition.x = (facingLeft ? -1f : 1f) * Mathf.Abs(facingMarkerDistance);
                facingMarker.localPosition = markerPosition;
            }
        }

        if (animator != null)
        {
            animator.SetBool("Grounded", IsGrounded);
            animator.SetFloat("Speed", Mathf.Abs(body.linearVelocity.x));
            animator.SetFloat("VerticalVelocity", body.linearVelocity.y);
        }

        ApplyBetterGravity();
    }

    private bool CanJump()
    {
        bool hasBufferedJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        bool hasCoyoteTime = Time.time - lastGroundedTime <= coyoteTime;
        return hasBufferedJump && hasCoyoteTime;
    }

    private void ApplyBetterGravity()
    {
        float gravityMultiplier = 1f;
        if (body.linearVelocity.y < 0f)
        {
            gravityMultiplier = fallGravityMultiplier;
        }
        else if (body.linearVelocity.y > 0f && jumpAction != null && !jumpAction.IsPressed())
        {
            gravityMultiplier = lowJumpGravityMultiplier;
        }

        if (gravityMultiplier > 1f)
        {
            Vector2 extraGravity = Physics2D.gravity * (body.gravityScale * body.mass * (gravityMultiplier - 1f));
            body.AddForce(extraGravity, ForceMode2D.Force);
        }

        if (body.linearVelocity.y < -maxFallSpeed)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, -maxFallSpeed);
        }
    }

    private bool CheckGrounded()
    {
        if (groundCheck == null)
        {
            return false;
        }

        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;
    }

    public void Configure(InputActionAsset actions, Transform check, LayerMask layer)
    {
        inputActions = actions;
        groundCheck = check;
        groundLayer = layer;
    }

    public void ConfigureFacingMarker(Transform marker)
    {
        facingMarker = marker;
    }

    public void ConfigureVisualRoot(Transform root)
    {
        visualRoot = root;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
        {
            return;
        }

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
