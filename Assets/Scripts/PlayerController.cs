using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 12f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.16f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Input System")]
    [SerializeField] private InputActionAsset inputActions;

    private Rigidbody2D body;
    private InputAction moveAction;
    private InputAction jumpAction;

    public bool IsGrounded { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();

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

        if (jumpAction != null && jumpAction.WasPressedThisFrame() && IsGrounded)
        {
            body.linearVelocity = new Vector2(body.linearVelocity.x, jumpForce);
        }
    }

    private void FixedUpdate()
    {
        if (moveAction == null)
        {
            return;
        }

        float horizontalInput = moveAction.ReadValue<Vector2>().x;
        body.linearVelocity = new Vector2(horizontalInput * moveSpeed, body.linearVelocity.y);
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
