using System.Collections;
using UnityEngine;

public sealed class EnemyDummy : MonoBehaviour
{
    [SerializeField] private int maxHealth = 3;
    [SerializeField] private int currentHealth = 3;
    [SerializeField] private float flashDuration = 0.08f;

    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private Coroutine flashRoutine;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            baseColor = spriteRenderer.color;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    public void Configure(int health)
    {
        maxHealth = Mathf.Max(1, health);
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (!isActiveAndEnabled || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - Mathf.Max(0, damage));
        Debug.Log($"Hit EnemyDummy, HP: {currentHealth}/{maxHealth}", this);

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }
        flashRoutine = StartCoroutine(Flash());

        if (currentHealth <= 0)
        {
            gameObject.SetActive(false);
        }
    }

    private IEnumerator Flash()
    {
        if (spriteRenderer == null)
        {
            yield break;
        }

        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(flashDuration);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseColor;
        }
        flashRoutine = null;
    }
}
