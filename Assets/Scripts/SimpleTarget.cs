using UnityEngine;

public class SimpleTarget : MonoBehaviour
{
    [Header("Target Stats")]
    [Tooltip("Defense value (armor)")]
    public float defense = 100f;
    
    [Tooltip("Max health")]
    public float maxHealth = 500f;
    
    [Header("Options")]
    [Tooltip("ONESHOT: langsung hancur kena peluru apapun")]
    public bool isOneHitPart = false;
    [Tooltip("Hilang setelah hancur (false = cuma ganti warna + spawn prefab)")]
    public bool isDisappearAfterDie = true;
    
    [Header("Effects")]
    [Tooltip("Prefab to spawn when destroyed")]
    public GameObject destroyedPrefab;
    
    [Tooltip("Current health (Visible for debugging)")]
    public float currentHealth;
    private bool _isDead;

    void Awake()
    {
        currentHealth = maxHealth;
        _isDead = false;
    }

    public OptResult TakeDamage(float atk, float pen, float muzzleVel)
    {
        if (_isDead) return default;

        if (isOneHitPart)
        {
            currentHealth = 0f;
            Die();
            var r = OptFormula.Calculate(atk, pen, defense, muzzleVel);
            return r;
        }

        var r2 = OptFormula.Calculate(atk, pen, defense, muzzleVel);
        currentHealth -= r2.damage;
        Debug.Log($"[CUBE] {name} | ATK:{atk} PEN:{pen} DEF:{defense} → DMG:{r2.damage:F1} PIERCE:{r2.pierce} EXIT:{r2.exitVel:F0} | HP:{currentHealth:F1}/{maxHealth}");
        
        if (currentHealth <= 0f) Die();
        
        return r2;
    }

    void Die()
    {
        _isDead = true;
        Debug.Log($"[CUBE] {name} DESTROYED!");

        if (destroyedPrefab != null)
            Instantiate(destroyedPrefab, transform.position, transform.rotation);

        if (isDisappearAfterDie)
            Destroy(gameObject);
        else
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.material.color = Color.red;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
        }
    }

    void OnValidate()
    {
        if (maxHealth < 0) maxHealth = 0;
        if (defense < 0) defense = 0;
    }
}