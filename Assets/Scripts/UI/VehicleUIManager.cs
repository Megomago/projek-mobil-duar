using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VehicleUIManager : MonoBehaviour
{
    [Header("=== VEHICLE REFERENCES ===")]
    [Tooltip("VehicleController — hanya untuk data penting (speed, gear, rpm, torsi)")]
    public VehicleController vehicle;
    [Tooltip("VehicleStatsManager — untuk data lainnya (bensin, baterai, armor, berat, dll)")]
    public VehicleStatsManager statsManager;

    [Header("=== DRIVING UI (dari VehicleController) ===")]
    public TextMeshProUGUI vehicleNameText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI gearText;
    public TextMeshProUGUI torqueText;
    public Image rpmBarFill;
    public Gradient rpmBarColor;

    [Header("=== FUEL UI (dari VehicleStatsManager) ===")]
    public TextMeshProUGUI fuelAmountText;
    public TextMeshProUGUI fuelCapacityText;
    public TextMeshProUGUI fuelConsumptionText;
    public Image fuelBarFill;

    [Header("=== ELECTRICAL UI (dari VehicleStatsManager) ===")]
    public TextMeshProUGUI batteryAmountText;
    public TextMeshProUGUI batteryCapacityText;
    public TextMeshProUGUI powerGenerationText;
    public TextMeshProUGUI powerConsumptionText;
    public TextMeshProUGUI maxPowerOutputText;
    public TextMeshProUGUI capacitorCapacityText;
    public TextMeshProUGUI capacitorChargeRateText;

    [Header("=== ARMOR & HEALTH UI (dari VehicleStatsManager) ===")]
    public TextMeshProUGUI bodyArmorText;
    public TextMeshProUGUI wheelHPText;
    public TextMeshProUGUI wheelArmorText;
    public TextMeshProUGUI engineArmorText;
    public TextMeshProUGUI batteryArmorText;

    [Header("=== WEIGHT UI (dari VehicleStatsManager) ===")]
    public TextMeshProUGUI totalWeightText;

    [Header("=== DEBUG OVERLAY (OnGUI — tanpa assign) ===")]
    [Tooltip("Tampilkan debug overlay OnGUI semua telemetri")]
    public bool showDebugOverlay = false;

    [Header("=== OPTIMIZATION ===")]
    [Tooltip("Berapa kali UI di-update per detik. 15 sudah sangat cukup dan hemat CPU. (0 = tiap frame)")]
    public float updateRate = 15f;

    private float _nextUpdateTime;
    private string _lastGearText;
    private VehicleCriticalPart[] _cachedCritParts;
    private VehicleController _cachedVc;

    public void Initialize(VehicleController controller, VehicleStatsManager stats, string vehicleName)
    {
        vehicle = controller;
        statsManager = stats;
        _cachedVc = controller;
        _cachedCritParts = null;
        _debugNextCritRefresh = 0f;
        SetVehicleName(vehicleName);

        if (rpmBarColor == null || rpmBarColor.colorKeys.Length == 0)
            SetupDefaultGradient();

        UpdateTelemetry();
    }

    private void Start()
    {
        if (vehicle == null)
        {
            vehicle = GetComponentInParent<VehicleController>();
            statsManager = GetComponentInParent<VehicleStatsManager>();
            if (vehicle != null)
            {
                Initialize(vehicle, statsManager, vehicle.gameObject.name.Replace("(Clone)", "").Trim());
            }
        }
    }

    public void SetVehicleName(string name)
    {
        if (vehicleNameText != null)
            vehicleNameText.text = name;
    }

    private void SetupDefaultGradient()
    {
        rpmBarColor = new Gradient();
        GradientColorKey[] colorKey = new GradientColorKey[3];
        colorKey[0] = new GradientColorKey(Color.green, 0.0f);
        colorKey[1] = new GradientColorKey(Color.yellow, 0.7f);
        colorKey[2] = new GradientColorKey(Color.red, 0.9f);

        GradientAlphaKey[] alphaKey = new GradientAlphaKey[2];
        alphaKey[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKey[1] = new GradientAlphaKey(1.0f, 1.0f);

        rpmBarColor.SetKeys(colorKey, alphaKey);
    }

    private void Update()
    {
        if (vehicle == null) return;

        if (updateRate > 0f)
        {
            if (Time.time < _nextUpdateTime) return;
            _nextUpdateTime = Time.time + (1f / updateRate);
        }

        UpdateTelemetry();
    }

    private void OnGUI()
    {
        if (!showDebugOverlay) return;

        // Debug overlay hanya tampil saat kendaraan ini SEDANG dikendarai player
        // (VehicleUIManager nempel di prefab mobil → tanpa guard ini overlay muncul
        // terus walau belum naik mobil / di preview garasi)
        if (VehicleEntry.ActiveVehicle == null) return;
        if (VehicleEntry.ActiveVehicle.GetComponent<VehicleUIManager>() != this) return;

        DrawDebugOverlay();
    }

    private GUIStyle _debugStyle;
    private readonly string[] _debugRows = new string[64];
    private int _debugRowCount;
    private int _debugBatteryRow = -1;
    private float _debugNextRefresh;
    private float _debugNextCritRefresh;
    private float _debugBatteryNorm = -1f;

    private void DrawDebugOverlay()
    {
        if (vehicle == null) return;

        if (Time.unscaledTime >= _debugNextRefresh)
        {
            _debugNextRefresh = Time.unscaledTime + 0.25f;
            RebuildDebugRows();
        }

        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            _debugStyle.normal.textColor = Color.white;
        }

        int x = Screen.width - 330, y = 10, w = 320, h = 20, pad = 2;
        GUI.Box(new Rect(x - 4, y - 8, w + 8, 780), "");

        for (int i = 0; i < _debugRowCount; i++)
        {
            if (i == _debugBatteryRow && _debugBatteryNorm >= 0f)
            {
                Rect barBG = new Rect(x, y, w, 14);
                Rect barFG = new Rect(x, y, w * _debugBatteryNorm, 14);
                GUI.DrawTexture(barBG, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(0.2f, 0.2f, 0.2f), 0, 0);
                Color battColor = _debugBatteryNorm > 0.5f ? Color.cyan : (_debugBatteryNorm > 0.2f ? Color.yellow : Color.red);
                GUI.DrawTexture(barFG, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, battColor, 0, 0);
                y += 18;
            }

            GUI.Label(new Rect(x, y, w, h), _debugRows[i], _debugStyle);
            y += h + pad;
        }
    }

    private void AddDebugRow(string text)
    {
        if (_debugRowCount < _debugRows.Length)
            _debugRows[_debugRowCount++] = text;
    }

    private void RebuildDebugRows()
    {
        _debugRowCount = 0;
        _debugBatteryRow = -1;
        _debugBatteryNorm = -1f;

        AddDebugRow("=== TELEMETRY ===");
        AddDebugRow("");
        AddDebugRow("--- DRIVING (VC) ---");
        AddDebugRow($"Speed     : {vehicle.speedKmh:F1} km/h");
        AddDebugRow($"Gear      : {vehicle.GetGearName()}");
        AddDebugRow($"RPM       : {vehicle.currentRPM:F0} / {vehicle.engine.maxRPM}");
        AddDebugRow($"Torque    : {vehicle.currentTorqueNm:F1} Nm");
        AddDebugRow($"Throttle  : {vehicle.throttleInput * 100f:F0}%");
        AddDebugRow($"Brake     : {vehicle.brakeInput * 100f:F0}%");
        AddDebugRow("");

        if (statsManager == null) return;

        AddDebugRow("--- FUEL (VSM) ---");
        AddDebugRow($"Fuel      : {statsManager.currentFuelAmount:F1} / {statsManager.currentFuelCapacity:F0} L");
        AddDebugRow($"Consump   : {vehicle.currentFuelConsumptionRate:F4} L/s");
        float fuelPct = statsManager.currentFuelCapacity > 0f ? statsManager.currentFuelAmount / statsManager.currentFuelCapacity * 100f : 0f;
        AddDebugRow($"Fuel %    : {fuelPct:F1}%");
        AddDebugRow("");

        AddDebugRow("--- ELECTRICAL (VSM) ---");
        float battPct = statsManager.currentBatteryCapacity > 0f ? statsManager.currentBatteryAmount / statsManager.currentBatteryCapacity * 100f : 0f;
        AddDebugRow($"Battery   : {statsManager.currentBatteryAmount:F1} / {statsManager.currentBatteryCapacity:F0} Wh ({battPct:F1}%)");
        float totalGen = statsManager.currentPowerGeneration;
        if (vehicle.engineRunning) totalGen += statsManager.enginePowerGeneration;
        AddDebugRow($"Power Gen : {totalGen:F0} W");
        AddDebugRow($"Power Cons: {statsManager.activePowerConsumption:F0} W");
        float netPower = totalGen - statsManager.activePowerConsumption;
        AddDebugRow($"Net Power : {netPower:F0} W {(netPower >= 0 ? "(charging)" : "(draining)")}");
        AddDebugRow($"Max Out   : {statsManager.currentMaxOutput:F0} W");
        AddDebugRow($"Capacitor : {statsManager.currentCapacitorCapacity:F0} Wh");
        AddDebugRow($"Chg Rate  : {statsManager.currentCapacitorChargeRate:F0} W");
        AddDebugRow("");

        AddDebugRow("--- ARMOR & HEALTH (VSM) ---");
        AddDebugRow($"Chassis DEF: {statsManager.currentChassisArmor:F0}");
        AddDebugRow($"Mass (kg)  : {statsManager.currentTotalMass:F0}");
        AddDebugRow("");
        AddDebugRow("--- CRITICAL PARTS ---");

        if (_cachedCritParts == null || Time.unscaledTime >= _debugNextCritRefresh)
        {
            _debugNextCritRefresh = Time.unscaledTime + 1f;
            _cachedCritParts = statsManager.GetComponentsInChildren<VehicleCriticalPart>(false);
        }

        for (int i = 0; i < _cachedCritParts.Length; i++)
        {
            VehicleCriticalPart cp = _cachedCritParts[i];
            if (cp == null || cp.hideFromModuleList) continue;
            AddDebugRow($"{cp.partName}  HP:{cp.currentHealth}/{cp.maxHealth}  DEF:{cp.armor}");
        }

        if (statsManager.currentBatteryCapacity > 0f)
        {
            _debugBatteryRow = _debugRowCount;
            _debugBatteryNorm = Mathf.Clamp01(statsManager.currentBatteryAmount / statsManager.currentBatteryCapacity);
        }

        AddDebugRow("--- WEIGHT (VSM) ---");
        if (statsManager.currentTotalMass >= 10000f)
            AddDebugRow($"Mass      : {statsManager.currentTotalMass / 1000f:F2} t");
        else
            AddDebugRow($"Mass      : {statsManager.currentTotalMass:F0} kg");
    }
    private void UpdateTelemetry()
    {
        // ── DARI VEHICLE CONTROLLER (speed, gear, rpm, torsi) ──
        // Semua pakai SetText(format, ...) = alloc-free (buffer internal TMP)
        if (speedText != null)
            speedText.SetText("{0} <size=50%>km/h</size>", Mathf.FloorToInt(vehicle.speedKmh));

        if (gearText != null)
        {
            string gear = vehicle.GetGearName();
            if (gear != _lastGearText)
            {
                _lastGearText = gear;
                gearText.text = gear;
            }
        }

        if (rpmBarFill != null && vehicle.engine.maxRPM > 0f)
        {
            float rpmNorm = Mathf.Clamp01(vehicle.currentRPM / vehicle.engine.maxRPM);
            rpmBarFill.fillAmount = rpmNorm;
            rpmBarFill.color = rpmBarColor.Evaluate(rpmNorm);
        }

        if (torqueText != null)
            torqueText.SetText("{0} <size=50%>Nm</size>", Mathf.FloorToInt(vehicle.currentTorqueNm));

        if (statsManager == null) return;

        // ── DARI VEHICLE STATS MANAGER (bensin, elektrik, armor, berat) ──
        VehicleController vc = _cachedVc != null ? _cachedVc : statsManager.GetComponent<VehicleController>();

        // Fuel
        if (fuelAmountText != null)
            fuelAmountText.SetText("{0:0.0} L", statsManager.currentFuelAmount);

        if (fuelCapacityText != null)
            fuelCapacityText.SetText("{0:0} L", statsManager.currentFuelCapacity);

        if (fuelConsumptionText != null)
        {
            if (vc != null)
                fuelConsumptionText.SetText("{0:0.00} L/s", vc.currentFuelConsumptionRate);
            else
                fuelConsumptionText.text = "0.00 L/s";
        }

        if (fuelBarFill != null && statsManager.currentFuelCapacity > 0f)
            fuelBarFill.fillAmount = Mathf.Clamp01(statsManager.currentFuelAmount / statsManager.currentFuelCapacity);

        // Electrical
        if (batteryAmountText != null)
            batteryAmountText.SetText("{0:0.0} Wh", statsManager.currentBatteryAmount);

        if (batteryCapacityText != null)
            batteryCapacityText.SetText("{0:0} Wh", statsManager.currentBatteryCapacity);

        if (powerGenerationText != null)
        {
            float totalGen = statsManager.currentPowerGeneration;
            if (vehicle != null && vehicle.engineRunning)
                totalGen += statsManager.enginePowerGeneration;
            powerGenerationText.SetText("{0:0} W", totalGen);
        }

        if (powerConsumptionText != null)
            powerConsumptionText.SetText("{0:0} W", statsManager.activePowerConsumption);

        if (maxPowerOutputText != null)
            maxPowerOutputText.SetText("{0:0} W", statsManager.currentMaxOutput);

        if (capacitorCapacityText != null)
            capacitorCapacityText.SetText("{0:0} Wh", statsManager.currentCapacitorCapacity);

        if (capacitorChargeRateText != null)
            capacitorChargeRateText.SetText("{0:0} W", statsManager.currentCapacitorChargeRate);

        // Armor & Health
        if (bodyArmorText != null)
            bodyArmorText.SetText("{0:0} DEF", statsManager.currentChassisArmor);

        if (wheelHPText != null || wheelArmorText != null || engineArmorText != null || batteryArmorText != null)
        {
            // Cache array — GetComponentsInChildren tiap frame = alloc
            if (_cachedCritParts == null)
                _cachedCritParts = statsManager.GetComponentsInChildren<VehicleCriticalPart>(false);

            foreach (var cp in _cachedCritParts)
            {
                if (cp.hideFromModuleList) continue;
                switch (cp.partType)
                {
                    case VehicleCriticalPart.CriticalPartType.Engine:
                        if (engineArmorText != null) engineArmorText.SetText("{0:0}", cp.armor);
                        break;
                    case VehicleCriticalPart.CriticalPartType.Battery:
                        if (batteryArmorText != null) batteryArmorText.SetText("{0:0}", cp.armor);
                        break;
                    default:
                        if (wheelHPText != null) wheelHPText.SetText("{0:0}", cp.currentHealth);
                        if (wheelArmorText != null) wheelArmorText.SetText("{0:0}", cp.armor);
                        break;
                }
            }
        }

        // Weight
        if (totalWeightText != null)
        {
            if (statsManager.currentTotalMass >= 10000f)
                totalWeightText.SetText("{0:0.0} t", statsManager.currentTotalMass / 1000f);
            else
                totalWeightText.SetText("{0:0} kg", statsManager.currentTotalMass);
        }
    }
}
