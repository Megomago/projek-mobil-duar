using System.Collections.Generic;
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

    [Tooltip("Maksimal tumpukan shake dari senjata yang sama (2 = dua kali nilai shake per tembakan)")]
    public int maxSameWeaponStacks = 2;

    private class ShakeSource
    {
        public float contribution;
    }

    private struct PendingShake
    {
        public int sourceId;
        public float intensity;
    }

    // Internal state: Trauma 0 s/d 1
    private float _trauma = 0f;
    private float _noiseSeed;

    private readonly Dictionary<int, ShakeSource> _shakeSources = new Dictionary<int, ShakeSource>();
    private readonly List<PendingShake> _pendingShakes = new List<PendingShake>();
    private readonly Dictionary<int, float> _frameIntensity = new Dictionary<int, float>();
    private readonly Dictionary<int, int> _frameShots = new Dictionary<int, int>();
    private readonly List<int> _expiredSources = new List<int>();

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
        UpdateTrauma();

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
        }

        // 4. Set posisi dan rotasi akhir
        transform.position = basePosition + shakePosOffset;
        transform.rotation = baseRotation * shakeRotOffset;
    }

    private void UpdateTrauma()
    {
        float decay = shakeDecay * Time.deltaTime;

        _expiredSources.Clear();
        foreach (var kvp in _shakeSources)
        {
            kvp.Value.contribution = Mathf.MoveTowards(kvp.Value.contribution, 0f, decay);
            if (kvp.Value.contribution <= 0f)
                _expiredSources.Add(kvp.Key);
        }

        for (int i = 0; i < _expiredSources.Count; i++)
            _shakeSources.Remove(_expiredSources[i]);

        if (_pendingShakes.Count > 0)
        {
            _frameIntensity.Clear();
            _frameShots.Clear();

            for (int i = 0; i < _pendingShakes.Count; i++)
            {
                PendingShake pending = _pendingShakes[i];

                _frameShots.TryGetValue(pending.sourceId, out int shots);
                _frameShots[pending.sourceId] = shots + 1;

                _frameIntensity.TryGetValue(pending.sourceId, out float intensity);
                if (pending.intensity > intensity)
                    _frameIntensity[pending.sourceId] = pending.intensity;
            }

            float highestIntensity = 0f;
            foreach (var kvp in _frameIntensity)
            {
                if (kvp.Value > highestIntensity)
                    highestIntensity = kvp.Value;
            }

            bool multipleWeapons = _frameIntensity.Count > 1;

            foreach (var kvp in _frameIntensity)
            {
                int sourceId = kvp.Key;
                float intensity = kvp.Value;
                int shots = _frameShots[sourceId];

                // Shake dari senjata yang sama maksimal 2x nilai per tembakan.
                float cap = Mathf.Min(intensity * Mathf.Max(1, maxSameWeaponStacks), 1f);

                // Kalau beda senjata nembak di frame yang sama, yang lebih kecil cuma 1x.
                if (multipleWeapons && intensity < highestIntensity)
                    cap = Mathf.Min(intensity, 1f);

                if (!_shakeSources.TryGetValue(sourceId, out ShakeSource source))
                {
                    source = new ShakeSource();
                    _shakeSources.Add(sourceId, source);
                }

                source.contribution = Mathf.Min(source.contribution + intensity * shots, cap);
            }

            _pendingShakes.Clear();
        }

        _trauma = 0f;
        foreach (var kvp in _shakeSources)
            _trauma += kvp.Value.contribution;

        _trauma = Mathf.Clamp01(_trauma);
    }

    /// <summary>
    /// Panggil saat senjata nembak atau kena ledakan.
    /// sourceId: identitas sumber shake (gunakan WeaponData.GetInstanceID()).
    /// Rekomendasi input intensity:
    /// - Senjata ringan/machinegun : 0.08 - 0.15
    /// - Oerlikon 20mm/Autocannon : 0.18 - 0.25
    /// - Meriam RARDEN 30mm/HE    : 0.45 - 0.60
    /// </summary>
    public void Shake(int sourceId, float intensity, float duration = 0f)
    {
        if (intensity <= 0f) return;
        _pendingShakes.Add(new PendingShake { sourceId = sourceId, intensity = intensity });
    }
}