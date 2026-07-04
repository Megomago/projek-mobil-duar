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

    public void Initialize(VehicleController controller, VehicleStatsManager stats, string vehicleName)
    {
        vehicle = controller;
        statsManager = stats;
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
        DrawDebugOverlay();
    }

    private void DrawDebugOverlay()
    {
        int x = Screen.width - 330, y = 10, w = 320, h = 20, pad = 2;
        GUIStyle lbl = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = Color.white } };
        GUI.Box(new Rect(x - 4, y - 8, w + 8, 780), "");

        void Row(string label)
        {
            GUI.Label(new Rect(x, y, w, h), label, lbl);
            y += h + pad;
        }

        Row($"<b>=== TELEMETRY ===</b>");
        Row($"");

        // ── DARI VEHICLE CONTROLLER ──
        Row($"<b>── DRIVING (VC) ──</b>");
        Row($"Speed     : {vehicle.speedKmh:F1} km/h");
        Row($"Gear      : {vehicle.GetGearName()}");
        Row($"RPM       : {vehicle.currentRPM:F0} / {vehicle.engine.maxRPM}");
        Row($"Torque    : {vehicle.currentTorqueNm:F1} Nm");
        Row($"Throttle  : {vehicle.throttleInput * 100f:F0}%");
        Row($"Brake     : {vehicle.brakeInput * 100f:F0}%");
        Row($"");

        if (statsManager == null) return;

        // ── DARI VEHICLE STATS MANAGER ──
        Row($"<b>── FUEL (VSM) ──</b>");
        Row($"Fuel      : {statsManager.currentFuelAmount:F1} / {statsManager.currentFuelCapacity:F0} L");
        if (vehicle != null)
            Row($"Consump   : {vehicle.currentFuelConsumptionRate:F4} L/s");
        float fuelPct = statsManager.currentFuelCapacity > 0f ? statsManager.currentFuelAmount / statsManager.currentFuelCapacity * 100f : 0f;
        Row($"Fuel %%    : {fuelPct:F1}%");
        Row($"");

        Row($"<b>── ELECTRICAL (VSM) ──</b>");
        float battPct = statsManager.currentBatteryCapacity > 0f ? statsManager.currentBatteryAmount / statsManager.currentBatteryCapacity * 100f : 0f;
        Row($"Battery   : {statsManager.currentBatteryAmount:F1} / {statsManager.currentBatteryCapacity:F0} Wh ({battPct:F1}%)");
        float totalGen = statsManager.currentPowerGeneration;
        if (vehicle != null && vehicle.engineRunning)
            totalGen += statsManager.enginePowerGeneration;
        Row($"Power Gen : {totalGen:F0} W");
        Row($"Power Cons: {statsManager.activePowerConsumption:F0} W");
        float netPower = totalGen - statsManager.activePowerConsumption;
        Row($"Net Power : {netPower:F0} W {(netPower >= 0 ? "(charging)" : "(draining)")}");
        Row($"Max Out   : {statsManager.currentMaxOutput:F0} W");
        Row($"Capacitor : {statsManager.currentCapacitorCapacity:F0} Wh");
        Row($"Chg Rate  : {statsManager.currentCapacitorChargeRate:F0} W");
        Row($"");

        Row($"<b>── ARMOR & HEALTH (VSM) ──</b>");
        Row($"Chassis DEF: {statsManager.currentChassisArmor:F0}");
        Row($"Mass (kg)  : {statsManager.currentTotalMass:F0}");
        Row($"");
        Row($"<b>── CRITICAL PARTS ──</b>");
        foreach (var cp in statsManager.GetComponentsInChildren<VehicleCriticalPart>(false))
        {
            if (cp.hideFromModuleList) continue;
            Row($"{cp.partName}  HP:{cp.currentHealth}/{cp.maxHealth}  DEF:{cp.armor}");
        }

        // Battery bar visual
        if (statsManager.currentBatteryCapacity > 0f)
        {
            float battNorm = Mathf.Clamp01(statsManager.currentBatteryAmount / statsManager.currentBatteryCapacity);
            Rect barBG = new Rect(x, y, w, 14);
            Rect barFG = new Rect(x, y, w * battNorm, 14);
            GUI.DrawTexture(barBG, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, new Color(0.2f, 0.2f, 0.2f), 0, 0);
            Color battColor = battNorm > 0.5f ? Color.cyan : (battNorm > 0.2f ? Color.yellow : Color.red);
            GUI.DrawTexture(barFG, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0, battColor, 0, 0);
            y += 18;
        }

        Row($"<b>── WEIGHT (VSM) ──</b>");
        if (statsManager.currentTotalMass >= 10000f)
            Row($"Mass      : {statsManager.currentTotalMass / 1000f:F2} t");
        else
            Row($"Mass      : {statsManager.currentTotalMass:F0} kg");
    }

    private void UpdateTelemetry()
    {
        // ── DARI VEHICLE CONTROLLER (speed, gear, rpm, torsi) ──
        if (speedText != null)
            speedText.text = $"{Mathf.FloorToInt(vehicle.speedKmh)} <size=50%>km/h</size>";

        if (gearText != null)
            gearText.text = vehicle.GetGearName();

        if (rpmBarFill != null && vehicle.engine.maxRPM > 0f)
        {
            float rpmNorm = Mathf.Clamp01(vehicle.currentRPM / vehicle.engine.maxRPM);
            rpmBarFill.fillAmount = rpmNorm;
            rpmBarFill.color = rpmBarColor.Evaluate(rpmNorm);
        }

        if (torqueText != null)
            torqueText.text = $"{vehicle.currentTorqueNm:F0} <size=50%>Nm</size>";

        if (statsManager == null) return;

        // ── DARI VEHICLE STATS MANAGER (bensin, elektrik, armor, berat) ──
        VehicleController vc = statsManager.GetComponent<VehicleController>();

        // Fuel
        if (fuelAmountText != null)
            fuelAmountText.text = statsManager.currentFuelAmount.ToString("0.0") + " L";

        if (fuelCapacityText != null)
            fuelCapacityText.text = statsManager.currentFuelCapacity.ToString("0") + " L";

        if (fuelConsumptionText != null)
        {
            if (vc != null)
                fuelConsumptionText.text = vc.currentFuelConsumptionRate.ToString("0.00") + " L/s";
            else
                fuelConsumptionText.text = "0.00 L/s";
        }

        if (fuelBarFill != null && statsManager.currentFuelCapacity > 0f)
            fuelBarFill.fillAmount = Mathf.Clamp01(statsManager.currentFuelAmount / statsManager.currentFuelCapacity);

        // Electrical
        if (batteryAmountText != null)
            batteryAmountText.text = statsManager.currentBatteryAmount.ToString("0.0") + " Wh";

        if (batteryCapacityText != null)
            batteryCapacityText.text = statsManager.currentBatteryCapacity.ToString("0") + " Wh";

        if (powerGenerationText != null)
        {
            float totalGen = statsManager.currentPowerGeneration;
            if (vehicle != null && vehicle.engineRunning)
                totalGen += statsManager.enginePowerGeneration;
            powerGenerationText.text = totalGen.ToString("0") + " W";
        }

        if (powerConsumptionText != null)
            powerConsumptionText.text = statsManager.activePowerConsumption.ToString("0") + " W";

        if (maxPowerOutputText != null)
            maxPowerOutputText.text = statsManager.currentMaxOutput.ToString("0") + " W";

        if (capacitorCapacityText != null)
            capacitorCapacityText.text = statsManager.currentCapacitorCapacity.ToString("0") + " Wh";

        if (capacitorChargeRateText != null)
            capacitorChargeRateText.text = statsManager.currentCapacitorChargeRate.ToString("0") + " W";

        // Armor & Health
        if (bodyArmorText != null)
            bodyArmorText.text = statsManager.currentChassisArmor.ToString("0") + " DEF";

        if (wheelHPText != null || wheelArmorText != null || engineArmorText != null || batteryArmorText != null)
        {
            foreach (var cp in statsManager.GetComponentsInChildren<VehicleCriticalPart>(false))
            {
                if (cp.hideFromModuleList) continue;
                switch (cp.partType)
                {
                    case VehicleCriticalPart.CriticalPartType.Engine:
                        if (engineArmorText != null) engineArmorText.text = cp.armor.ToString("0");
                        break;
                    case VehicleCriticalPart.CriticalPartType.Battery:
                        if (batteryArmorText != null) batteryArmorText.text = cp.armor.ToString("0");
                        break;
                    default:
                        if (wheelHPText != null) wheelHPText.text = cp.currentHealth.ToString("0");
                        if (wheelArmorText != null) wheelArmorText.text = cp.armor.ToString("0");
                        break;
                }
            }
        }

        // Weight
        if (totalWeightText != null)
        {
            if (statsManager.currentTotalMass >= 10000f)
                totalWeightText.text = (statsManager.currentTotalMass / 1000f).ToString("0.0") + " t";
            else
                totalWeightText.text = statsManager.currentTotalMass.ToString("0") + " kg";
        }
    }
}
