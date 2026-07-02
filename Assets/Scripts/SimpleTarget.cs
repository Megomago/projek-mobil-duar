using UnityEngine;

public class SimpleTarget : MonoBehaviour
{
    [Header("Target Stats")]
    [Tooltip("Defense value (armor)")]
    public float defense = 100f;
    
    [Tooltip("Max health")]
    public float maxHealth = 500f;
    
    [Tooltip("Prefab to spawn when destroyed")]
    public GameObject destroyedPrefab;
    
    [Tooltip("Current health (Visible for debugging)")]
    public float currentHealth;

    void Awake() => currentHealth = maxHealth;

    public OptResult TakeDamage(float atk, float pen, float muzzleVel)
    {
        var r = OptFormula.Calculate(atk, pen, defense, muzzleVel);
        currentHealth -= r.damage;
        Debug.Log($"[CUBE] {name} | ATK:{atk} PEN:{pen} DEF:{defense} → DMG:{r.damage:F1} PIERCE:{r.pierce} EXIT:{r.exitVel:F0} | HP:{currentHealth:F1}/{maxHealth}");
        
        if (currentHealth <= 0f) Die();
        
        return r;
    }

    void Die()
    {
        Debug.Log($"[CUBE] {name} DESTROYED!");
        var r = GetComponent<Renderer>();
        if (r != null) r.material.color = Color.red;

        if (destroyedPrefab != null)
        {
            Instantiate(destroyedPrefab, transform.position, transform.rotation);
        }
        
        // Optional: Destroy this object after spawning prefab
        // Destroy(gameObject);
    }

    void OnValidate()
    {
        if (maxHealth < 0) maxHealth = 0;
        if (defense < 0) defense = 0;
    }
}