using UnityEngine;

public class WheelHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 50f;
    public float currentHealth;
    public float armor = 10f; // Pertahanan roda

    [Header("Wheel Components")]
    [Tooltip("Visual mesh dari roda yang akan dihilangkan saat hancur")]
    public GameObject wheelMesh;
    
    [Tooltip("WheelCollider dari roda yang akan dimatikan saat hancur")]
    public WheelCollider wheelCollider;

    public bool isDestroyed { get; private set; }

    void Awake()
    {
        currentHealth = maxHealth;
        isDestroyed = false;
    }

    public void TakeDamage(float damage)
    {
        if (isDestroyed) return;

        currentHealth -= damage;
        
        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            DestroyWheel();
        }
    }

    private void DestroyWheel()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Hilangkan Mesh (Visual)
        if (wheelMesh != null)
        {
            wheelMesh.SetActive(false);
        }

        // Matikan WheelCollider supaya mobil pincang / nyeret tanah
        if (wheelCollider != null)
        {
            wheelCollider.enabled = false;
        }

        #if UNITY_EDITOR
        Debug.Log($"[WHEEL] {gameObject.name} hancur! Mobil jadi pincang.");
        #endif
    }
}
