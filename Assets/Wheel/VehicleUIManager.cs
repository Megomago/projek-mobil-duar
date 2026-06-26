using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VehicleUIManager : MonoBehaviour
{
    [Header("=== VEHICLE REFERENCE ===")]
    [Tooltip("Kendaraan yang sedang aktif/dipantau")]
    public VehicleController vehicle;

    [Header("=== UI ELEMENTS ===")]
    [Tooltip("Teks untuk menampilkan nama kendaraan (Otomatis terisi jika di-spawn via Manager)")]
    public TextMeshProUGUI vehicleNameText;

    [Tooltip("Teks untuk menampilkan kecepatan")]
    public TextMeshProUGUI speedText;
    
    [Tooltip("Teks untuk menampilkan posisi Gear (1, 2, R, dll)")]
    public TextMeshProUGUI gearText;
    
    [Tooltip("Gunakan UI Image dengan Image Type: Filled (Horizontal)")]
    public Image rpmBarFill;
    
    [Tooltip("Warna Bar RPM dari hijau (rendah) ke merah (redline)")]
    public Gradient rpmBarColor;

    [Header("=== OPTIMIZATION ===")]
    [Tooltip("Berapa kali UI di-update per detik. 15 sudah sangat cukup dan hemat CPU. (0 = tiap frame)")]
    public float updateRate = 15f; 
    
    private float _nextUpdateTime;

    /// <summary>
    /// Dipanggil oleh spawner/manager untuk menyambungkan UI dengan mobil secara dinamis (sistem Prefab HUD).
    /// </summary>
    public void Initialize(VehicleController controller, string vehicleName)
    {
        vehicle = controller;
        SetVehicleName(vehicleName);

        // Setup warna default (Hijau -> Kuning -> Merah) jika belum di-set di Inspector
        if (rpmBarColor == null || rpmBarColor.colorKeys.Length == 0)
        {
            SetupDefaultGradient();
        }

        // Panggil sekali di awal agar UI tidak kosong pada frame pertama
        UpdateTelemetry();
    }

    private void Start()
    {
        // Fallback: Jika tidak di-spawn secara dinamis oleh Manager, dia akan mencari mobil tempat script ini nempel (cara lama)
        if (vehicle == null)
        {
            vehicle = GetComponentInParent<VehicleController>();
            if (vehicle != null)
            {
                Initialize(vehicle, vehicle.gameObject.name.Replace("(Clone)", "").Trim());
            }
        }
    }

    public void SetVehicleName(string name)
    {
        if (vehicleNameText != null)
        {
            vehicleNameText.text = name;
        }
    }

    private void SetupDefaultGradient()
    {
        rpmBarColor = new Gradient();
        GradientColorKey[] colorKey = new GradientColorKey[3];
        colorKey[0] = new GradientColorKey(Color.green, 0.0f);   // 0% RPM
        colorKey[1] = new GradientColorKey(Color.yellow, 0.7f);  // 70% RPM
        colorKey[2] = new GradientColorKey(Color.red, 0.9f);     // 90% RPM (Redline)

        GradientAlphaKey[] alphaKey = new GradientAlphaKey[2];
        alphaKey[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKey[1] = new GradientAlphaKey(1.0f, 1.0f);

        rpmBarColor.SetKeys(colorKey, alphaKey);
    }

    private void Update()
    {
        if (vehicle == null) return;

        // Optimasi: Update UI beberapa kali per detik saja agar tidak membebani CPU
        if (updateRate > 0f)
        {
            if (Time.time < _nextUpdateTime) return;
            _nextUpdateTime = Time.time + (1f / updateRate);
        }

        UpdateTelemetry();
    }

    private void UpdateTelemetry()
    {
        // 1. Update Speed 
        if (speedText != null)
        {
            speedText.text = $"{Mathf.FloorToInt(vehicle.speedKmh)} <size=50%>km/h</size>"; 
        }

        // 2. Update Gear
        if (gearText != null)
        {
            gearText.text = vehicle.GetGearName();
        }

        // 3. Update RPM Bar (Visual Jarum/Bar)
        if (rpmBarFill != null && vehicle.engine.maxRPM > 0f)
        {
            float rpmNormalized = Mathf.Clamp01(vehicle.currentRPM / vehicle.engine.maxRPM);
            rpmBarFill.fillAmount = rpmNormalized;
            rpmBarFill.color = rpmBarColor.Evaluate(rpmNormalized);
        }
    }
}
