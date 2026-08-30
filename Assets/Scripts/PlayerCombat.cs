using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class PlayerCombat : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Vector2 attackSize = new(0.8f, 0.65f);
    [SerializeField] private int attackDamage = 1;
    [SerializeField] private LayerMask enemyLayer = 1;
    [SerializeField] private float attackCooldown = 0.35f;
    [SerializeField] private float attackHitDelay = 0.18f;
    [SerializeField] private float attackDuration = 0.54f;

    [Header("References")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController playerController;

    private InputAction attackAction;
    private float attackStartedAt = float.NegativeInfinity;
    private float nextAttackTime = float.NegativeInfinity;
    private bool attackInProgress;
    private bool hitApplied;

    public bool IsAttacking => attackInProgress;

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        if (playerController == null)
        {
            playerController = GetComponent<PlayerController>();
        }
        attackAction = inputActions?.FindAction("Player/Attack", true);
        UpdateAttackPointPosition();
    }

    private void Update()
    {
        UpdateAttackPointPosition();

        if (attackInProgress)
        {
            float elapsed = Time.time - attackStartedAt;
            if (!hitApplied && elapsed >= attackHitDelay)
            {
                hitApplied = true;
                PerformHitDetection();
            }

            if (elapsed >= attackDuration)
            {
                attackInProgress = false;
            }
        }

        if (attackAction == null || !attackAction.WasPressedThisFrame())
        {
            return;
        }

        if (!attackInProgress && Time.time >= nextAttackTime)
        {
            BeginAttack();
        }
    }

    public void Configure(InputActionAsset actions, Transform point, LayerMask layer)
    {
        inputActions = actions;
        attackPoint = point;
        enemyLayer = layer;
        animator = GetComponent<Animator>();
        playerController = GetComponent<PlayerController>();
        attackAction = inputActions?.FindAction("Player/Attack", true);
    }

    private void BeginAttack()
    {
        attackInProgress = true;
        hitApplied = false;
        attackStartedAt = Time.time;
        nextAttackTime = Time.time + attackCooldown;
        animator?.SetTrigger("Attack");
    }

    private void PerformHitDetection()
    {
        if (attackPoint == null)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(attackPoint.position, attackSize, 0f, enemyLayer);
        HashSet<EnemyDummy> damagedEnemies = new();
        foreach (Collider2D hit in hits)
        {
            EnemyDummy enemy = hit.GetComponentInParent<EnemyDummy>();
            if (enemy != null && damagedEnemies.Add(enemy))
            {
                enemy.TakeDamage(attackDamage);
            }
        }
    }

    private void UpdateAttackPointPosition()
    {
        if (attackPoint == null)
        {
            return;
        }

        Vector3 localPosition = attackPoint.localPosition;
        float distance = Mathf.Abs(localPosition.x);
        localPosition.x = playerController != null && playerController.IsFacingLeft ? -distance : distance;
        attackPoint.localPosition = localPosition;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.9f);
        Gizmos.DrawWireCube(attackPoint.position, attackSize);
    }
}
