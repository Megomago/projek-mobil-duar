using UnityEngine;

public class VehicleCamera : MonoBehaviour
{
    public static VehicleCamera Instance { get; private set; }

    public Transform target; // VehicleController transform
    public float distance = 6f;
    public float height = 2.5f;
    public float mouseSensitivity = 2f;

    private float currentX = 0f;
    private float currentY = 0f;

    private float shakeTimer = 0f;
    private float shakeIntensity = 0f;
    private float initialShakeDuration = 0f; // Menyimpan durasi awal untuk efek fade out

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
        // Tahan ALT untuk memunculkan kursor
        if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Kamera hanya bisa digerakkan saat ALT tidak ditahan (kursor terkunci)
            currentX += Input.GetAxis("Mouse X") * mouseSensitivity;
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            currentY = Mathf.Clamp(currentY, -35f, 60f);
        }
    }

    private void LateUpdate()
    {
        if (!target)
            return;

        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 position = target.position - (rotation * Vector3.forward * distance + new Vector3(0, -height, 0));

        transform.position = position;
        transform.rotation = rotation;
        transform.LookAt(target.position + new Vector3(0, height * 0.5f, 0));

        // Camera Shake effect applied after all other position calculations
        if (shakeTimer > 0)
        {
            // Menghitung rasio sisa waktu (dari 1 perlahan turun ke 0)
            float fadeProgress = shakeTimer / initialShakeDuration; 
            
            // Mengalikan intensitas dengan fadeProgress agar getarannya memudar perlahan
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
