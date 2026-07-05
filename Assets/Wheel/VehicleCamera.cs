using UnityEngine;

public class VehicleCamera : MonoBehaviour
{
    public static VehicleCamera Instance { get; private set; }

    public Transform target;
    public float distance = 6f;
    public float height = 2.5f;
    public float mouseSensitivity = 2f;

    private float currentX = 0f;
    private float currentY = 0f;

    private float shakeTimer = 0f;
    private float shakeIntensity = 0f;
    private float initialShakeDuration = 0f;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            currentX += Input.GetAxis("Mouse X") * mouseSensitivity;
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            currentY = Mathf.Clamp(currentY, -35f, 60f);
        }
    }

    private void LateUpdate()
{
    if (!target) return;

    // 1. Dapet rotasi dari mouse
    Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
    
    // 2. Dapet posisi di belakang mobil
    Vector3 position = target.position - (rotation * Vector3.forward * distance) + new Vector3(0, height, 0);

    // 3. Set posisi dan rotasi. STOP. GAUSAH PAKE LOOKAT!
    transform.position = position;
    transform.rotation = rotation;

    // Camera Shake (udah bener lu taruh di bawah)
    if (shakeTimer > 0)
    {
        float fadeProgress = shakeTimer / initialShakeDuration; 
        transform.position += Random.insideUnitSphere * (shakeIntensity * fadeProgress);
        shakeTimer -= Time.deltaTime;
    }
}

    public void Shake(float intensity, float duration)
    {
        shakeIntensity = intensity;
        shakeTimer = duration;
        initialShakeDuration = duration; // Simpan durasi saat ini untuk patokan fade
    }
}
