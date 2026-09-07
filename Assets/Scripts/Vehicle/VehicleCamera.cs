using UnityEngine;

public class VehicleCamera : MonoBehaviour
{
    public static VehicleCamera Instance { get; private set; }

    [Header("=== TARGET & CHASE SETTINGS ===")]
    public Transform target;
    public float distance = 6f;
    public float height = 2.5f;
    public float mouseSensitivity = 2f;

    private float currentX = 0f;
    private float currentY = 0f;

    [Header("=== CAMERA SHAKE (SIMON EDITION) ===")]
    [Tooltip("Maksimal pergeseran posisi kamera dalam meter (0.25 - 0.4 udah berasa mantep banget)")]
    public float maxPosShake = 3f;

    [Tooltip("Maksimal getaran rotasi dalam derajat (ini kunci tembakan berasa berbobot!)")]
    public float maxRotShake = 1.8f;

    [Tooltip("Frekuensi getaran per detik (25 - 35 Hz pas buat senjata berat)")]
    public float shakeFrequency = 28f;

    [Tooltip("Kecepatan getaran mereda (semakin gede, semakin cepet stabil)")]
    public float shakeDecay = 3.5f;

    // Internal state: Trauma 0 s/d 1
    private float _trauma = 0f;
    private float _noiseSeed;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        Instance = this;
        _noiseSeed = Random.value * 100f; // Seed acak biar polanya gak ngebosenin
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

        // 1. Rotasi dasar dari pergerakan mouse
        Quaternion baseRotation = Quaternion.Euler(currentY, currentX, 0f);

        // 2. Posisi dasar di belakang kendaraan
        Vector3 basePosition = target.position - (baseRotation * Vector3.forward * distance) + new Vector3(0f, height, 0f);

        // 3. Kalkulasi Camera Shake (Perlin Noise)
        Vector3 shakePosOffset = Vector3.zero;
        Quaternion shakeRotOffset = Quaternion.identity;

        if (_trauma > 0f)
        {
            // Pangkat dua (_trauma^2): guncangan kecil berasa halus, guncangan gede berasa galak
            float shakeFactor = _trauma * _trauma;
            float time = Time.time * shakeFrequency;

            // Perlin Noise kontinu (-1 s/d 1)
            float noiseX = (Mathf.PerlinNoise(_noiseSeed + 1f, time) * 2f - 1f);
            float noiseY = (Mathf.PerlinNoise(_noiseSeed + 2f, time) * 2f - 1f);
            float noiseZ = (Mathf.PerlinNoise(_noiseSeed + 3f, time) * 2f - 1f);

            // Posisi getar relatif terhadap arah hadap kamera (bukan sumbu dunia kaku)
            shakePosOffset = baseRotation * new Vector3(noiseX, noiseY, noiseZ * 0.4f) * (maxPosShake * shakeFactor);

            // Rotasi getar (Pitch, Yaw, Roll)
            float rotPitch = (Mathf.PerlinNoise(_noiseSeed + 4f, time) * 2f - 1f) * maxRotShake * shakeFactor;
            float rotYaw   = (Mathf.PerlinNoise(_noiseSeed + 5f, time) * 2f - 1f) * maxRotShake * shakeFactor;
            float rotRoll  = (Mathf.PerlinNoise(_noiseSeed + 6f, time) * 2f - 1f) * (maxRotShake * 0.5f) * shakeFactor;

            shakeRotOffset = Quaternion.Euler(rotPitch, rotYaw, rotRoll);

            // Redam trauma secara konsisten tanpa double-decay
            _trauma = Mathf.MoveTowards(_trauma, 0f, shakeDecay * Time.deltaTime);
        }

        // 4. Set posisi dan rotasi akhir
        transform.position = basePosition + shakePosOffset;
        transform.rotation = baseRotation * shakeRotOffset;
    }

    /// <summary>
    /// Panggil saat senjata nembak atau kena ledakan.
    /// Rekomendasi input intensity:
    /// - Senjata ringan/machinegun : 0.08 - 0.15
    /// - Oerlikon 20mm/Autocannon : 0.18 - 0.25
    /// - Meriam RARDEN 30mm/HE    : 0.45 - 0.60
    /// </summary>
    public void Shake(float intensity, float duration = 0f)
    {
        // Trauma terakumulasi dan auto-clamp di 1.0 (anti gempa kiamat)
        // Parameter duration sengaja dibiarin ada biar gak ngerusak panggilan dari script senjata lu.
        _trauma = Mathf.Clamp01(_trauma + intensity);
    }
}