using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VehicleController - Full vehicle physics system for Unity 2022
/// Supports: Custom WheelCollider, Gear System, RPM, Torque, Drivetrain
/// Author: Custom build
/// </summary>
public class VehicleController : MonoBehaviour
{
    #region --- STRUCTS & ENUMS ---

    public enum DrivetrainType { FWD, RWD, AWD }
    public enum TransmissionType { Manual, Automatic }

    [System.Serializable]
    public struct WheelSetup
    {
        public WheelCollider collider;
        public Transform meshTransform;
        [Tooltip("Apakah roda ini adalah roda penggerak (drive wheel)?")]
        public bool isDriveWheel;
        [Tooltip("Apakah roda ini bisa dibelokkan (steering)?")]
        public bool isSteerWheel;
    }

    [System.Serializable]
    public struct GearRatio
    {
        public string gearName;
        public float ratio;
    }

    [System.Serializable]
    public struct EngineParameters
    {
        [Tooltip("Torque maksimum engine dalam Nm")]
        public float maxTorqueNm;
        [Tooltip("RPM idle engine")]
        public float idleRPM;
        [Tooltip("RPM maksimum engine (redline)")]
        public float maxRPM;
        [Tooltip("RPM di mana torque puncak tercapai")]
        public float peakTorqueRPM;
        [Tooltip("Inersia flywheel, mempengaruhi respons RPM")]
        public float flywheelInertia;
        [Tooltip("Kurva torque engine (X=RPM normalized 0-1, Y=multiplier 0-1)")]
        public AnimationCurve torqueCurve;
        [Tooltip("Konsumsi bahan bakar (Liter/detik) saat RPM maksimum")]
        public float maxFuelConsumptionRate;
    }

    [System.Serializable]
    public struct WheelFrictionParameters
    {
        [Tooltip("Friction longitudinal (maju/mundur) - forward")]
        public float forwardStiffness;
        [Tooltip("Friction lateral (samping) - sideways")]
        public float sidewaysStiffness;
        [Tooltip("Extremum slip longitudinal")]
        public float forwardExtremumSlip;
        [Tooltip("Extremum slip lateral")]
        public float sidewaysExtremumSlip;
        [Tooltip("Asymptote slip longitudinal")]
        public float forwardAsymptoteSlip;
        [Tooltip("Asymptote slip lateral")]
        public float sidewaysAsymptoteSlip;
        [Tooltip("Extremum value friction")]
        public float extremumValue;
        [Tooltip("Asymptote value friction")]
        public float asymptoteValue;
    }

    [System.Serializable]
    public struct SuspensionParameters
    {
        [Tooltip("Jarak travel suspensi dalam meter")]
        public float suspensionDistance;
        [Tooltip("Spring rate suspensi dalam N/m")]
        public float springRate;
        [Tooltip("Damper suspensi dalam Ns/m")]
        public float damperRate;
        [Tooltip("Target posisi suspensi (0=atas, 1=bawah)")]
        [Range(0f, 1f)]
        public float targetPosition;
    }

    #endregion

    #region --- INSPECTOR FIELDS ---

    [Header("=== WHEEL SETUP ===")]
    public WheelSetup[] wheels;

    [Header("=== ENGINE PARAMETERS ===")]
    public EngineParameters engine = new EngineParameters
    {
        maxTorqueNm       = 280f,
        idleRPM           = 650f,
        maxRPM            = 7000f,
        peakTorqueRPM     = 3800f,
        flywheelInertia   = 0.35f,
        maxFuelConsumptionRate = 0.05f
    };

    [Header("=== STARTER ===")]
    [Tooltip("Daya listrik yg dibutuhkan starter utk menyalakan mesin (Watt). Mobil kecil ~1kW, diesel/V8 ~2-3kW")]
    public float starterPowerRequired = 1500f;
    [Tooltip("Minimal energi baterai (Wh) agar starter bisa memutar mesin. ~5-10% dari kapasitas baterai")]
    public float starterMinBatteryWh = 50f;

    [Header("=== ENGINE BRAKE ===")]
    [Tooltip("Kekuatan engine brake saat throttle lepas (semakin besar, semakin terasa hambatan mesin)")]
    public float engineBrakingTorque = 200f;
    [Tooltip("Kekuatan compression brake saat mesin mati (menahan laju kendaraan)")]
    public float compressionBrakeTorque = 80f;

    [Header("=== DRIVETRAIN ===")]
    public DrivetrainType drivetrainType = DrivetrainType.RWD;
    public TransmissionType transmissionType = TransmissionType.Automatic;

    [Header("=== GEAR RATIOS ===")]
    [Tooltip("Index 0 = Reverse, Index 1 = Gear 1, dst.")]
    public GearRatio[] gearRatios = new GearRatio[]
    {
        new GearRatio { gearName = "R",  ratio = -3.5f  },
        new GearRatio { gearName = "1",  ratio =  3.36f },
        new GearRatio { gearName = "2",  ratio =  2.10f },
        new GearRatio { gearName = "3",  ratio =  1.48f },
        new GearRatio { gearName = "4",  ratio =  1.11f },
        new GearRatio { gearName = "5",  ratio =  0.85f },
        new GearRatio { gearName = "6",  ratio =  0.67f }
    };

    [Tooltip("Rasio final drive (differential)")]
    public float finalDriveRatio = 3.7f;

    [Tooltip("Efisiensi transmisi (0.85 - 0.95 umumnya)")]
    [Range(0.5f, 1f)]
    public float transmissionEfficiency = 0.85f;

    [Header("=== AUTO TRANSMISSION SETTINGS ===")]
    [Tooltip("RPM saat transmisi otomatis upshift")]
    public float autoUpshiftRPM = 6200f;
    [Tooltip("RPM saat transmisi otomatis downshift")]
    public float autoDownshiftRPM = 1200f;

    [Header("=== SUSPENSION CUSTOM ===")]
    public SuspensionParameters suspension = new SuspensionParameters
    {
        suspensionDistance = 0.2f,
        springRate         = 32000f,
        damperRate         = 4000f,
        targetPosition     = 0.4f
    };

    [Header("=== FRICTION CUSTOM ===")]
    public WheelFrictionParameters wheelFriction = new WheelFrictionParameters
    {
        forwardStiffness       = 1.2f,
        sidewaysStiffness      = 1.5f,
        forwardExtremumSlip    = 0.5f,
        sidewaysExtremumSlip   = 0.3f,
        forwardAsymptoteSlip   = 1.0f,
        sidewaysAsymptoteSlip  = 0.7f,
        extremumValue          = 0.95f,
        asymptoteValue         = 0.75f
    };

    [Header("=== STEERING ===")]
    [Tooltip("Sudut maksimum belok dalam derajat")]
    public float maxSteerAngle = 40f;
    [Tooltip("Kurva steer angle vs kecepatan (X=speed km/h normalized, Y=steer multiplier)")]
    public AnimationCurve steerVsSpeedCurve;

    [Header("=== BRAKE ===")]
    [Tooltip("Torque rem dalam Nm")]
    public float brakeTorque = 2500f;
    [Tooltip("Porsi rem ke roda depan (0=belakang semua, 1=depan semua)")]
    [Range(0f, 1f)]
    public float brakeBias = 0.60f;
    [Tooltip("Torque handbrake")]
    public float handbrakeTorque = 4500f;

    [Header("=== RIGIDBODY ===")]
    [Tooltip("Massa kendaraan dalam kg")]
    public float vehicleMass = 1400f;
    [Tooltip("Tinggi center of mass dari posisi pivot")]
    public float centerOfMassHeight = -0.3f;

    [Header("=== ANTI ROLL BAR ===")]
    [Tooltip("Kekuatan anti-roll bar")]
    public float antiRollForce = 7000f;

    [Header("=== DOWNFORCE ===")]
    [Tooltip("Downforce multiplier berdasarkan kecepatan")]
    public float downforceMultiplier = 0.02f;

    [Tooltip("Gunakan RPM berdasarkan kecepatan nyata mobil (mencegah RPM fluktuatif karena roda slip / ngepot)")]
    public bool preventWheelSlipRPM = true;

    #endregion

    #region --- RUNTIME STATE ---

    // Engine state
    [HideInInspector] public float currentRPM;
    [HideInInspector] public float currentTorqueNm;
    [HideInInspector] public float engineLoad;       // 0-1
    [HideInInspector] public bool  engineRunning = false;
    [HideInInspector] public bool  lightsOn = false;
    [HideInInspector] public float currentFuelConsumptionRate; // L/sec

    // Transmission state
    [HideInInspector] public int   currentGearIndex = 1; // 1 = Gear 1
    [HideInInspector] public bool  isReverse = false;
    [HideInInspector] public float clutchValue = 1f;      // 0=disengaged, 1=fully engaged

    // Input state
    [HideInInspector] public float throttleInput;    // 0 - 1
    [HideInInspector] public float brakeInput;       // 0 - 1
    [HideInInspector] public float steerInput;       // -1 to 1
    [HideInInspector] public float handbrakeInput;   // 0 - 1
    [HideInInspector] public bool  clutchInput;      // manual clutch

    // Vehicle state
    [HideInInspector] public float speedKmh;
    [HideInInspector] public float speedMs;
    [HideInInspector] public float wheelSlipRatio;

    // Internal
    private Rigidbody   _rb;
    private float       _wheelRPM;
    private float       _driveWheelCount;
    private float       _shiftCooldown;
    private float       _brakeHoldTimer;
    private const float SHIFT_COOLDOWN_TIME = 0.8f;
    private const float RPM_TO_RADS         = Mathf.PI / 30f;
    private const float RADS_TO_RPM         = 30f / Mathf.PI;

    #endregion

    #region --- UNITY LIFECYCLE ---

    private void Awake()
    {
        engineRunning = false;
        currentRPM = 0f;

        _rb = GetComponent<Rigidbody>();
        InitRigidbody();
        InitWheels();
        InitDefaultCurves();
    }

    private void Update()
    {
        GatherInput();
        UpdateSpeedometer();

        if (transmissionType == TransmissionType.Automatic)
            HandleAutoTransmission();

        _shiftCooldown -= Time.deltaTime;
        
        UpdateWheelMeshes();
    }

    private void FixedUpdate()
    {
        UpdateEngineRPM();
        ApplyDriveTorque();
        ApplyBraking();
        ApplySteering();
        ApplyAntiRollBar();
        ApplyDownforce();
    }

    #endregion

    #region --- INITIALIZATION ---

    private void InitRigidbody()
    {
        _rb.mass = vehicleMass;
        _rb.centerOfMass = new Vector3(0f, centerOfMassHeight, 0f);
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void InitWheels()
    {
        _driveWheelCount = 0;
        foreach (var w in wheels)
        {
            if (w.isDriveWheel) _driveWheelCount++;
            ApplyWheelColliderSettings(w.collider);
        }
    }

    private void InitDefaultCurves()
    {
        // Torque curve default: naik sampai peakTorqueRPM, turun setelah itu
        if (engine.torqueCurve == null || engine.torqueCurve.keys.Length == 0)
        {
            engine.torqueCurve = new AnimationCurve(
                new Keyframe(0.0f, 0.30f),
                new Keyframe(0.2f, 0.65f),
                new Keyframe(0.4f, 0.85f),
                new Keyframe(0.57f, 1.00f),   // peak torque
                new Keyframe(0.75f, 0.90f),
                new Keyframe(0.90f, 0.75f),
                new Keyframe(1.00f, 0.50f)    // redline
            );
        }

        // Steer vs speed curve
        if (steerVsSpeedCurve == null || steerVsSpeedCurve.keys.Length == 0)
        {
            steerVsSpeedCurve = new AnimationCurve(
                new Keyframe(0f,   1.00f),
                new Keyframe(0.2f, 0.80f),
                new Keyframe(0.5f, 0.50f),
                new Keyframe(1.0f, 0.25f)
            );
        }
    }

    /// <summary>
    /// Terapkan parameter WheelCollider custom dari inspector ke collider
    /// </summary>
    private void ApplyWheelColliderSettings(WheelCollider wc)
    {
        if (wc == null) return;

        // Suspension
        JointSpring spring = wc.suspensionSpring;
        spring.spring        = suspension.springRate;
        spring.damper        = suspension.damperRate;
        spring.targetPosition = suspension.targetPosition;
        wc.suspensionSpring  = spring;
        wc.suspensionDistance = suspension.suspensionDistance;

        // Forward friction
        WheelFrictionCurve fwd = wc.forwardFriction;
        fwd.extremumSlip      = wheelFriction.forwardExtremumSlip;
        fwd.extremumValue     = wheelFriction.extremumValue;
        fwd.asymptoteSlip     = wheelFriction.forwardAsymptoteSlip;
        fwd.asymptoteValue    = wheelFriction.asymptoteValue;
        fwd.stiffness         = wheelFriction.forwardStiffness;
        wc.forwardFriction    = fwd;

        // Sideways friction
        WheelFrictionCurve side = wc.sidewaysFriction;
        side.extremumSlip     = wheelFriction.sidewaysExtremumSlip;
        side.extremumValue    = wheelFriction.extremumValue;
        side.asymptoteSlip    = wheelFriction.sidewaysAsymptoteSlip;
        side.asymptoteValue   = wheelFriction.asymptoteValue;
        side.stiffness        = wheelFriction.sidewaysStiffness;
        wc.sidewaysFriction   = side;
    }

    #endregion

    #region --- INPUT ---

    private void GatherInput()
    {
        // Ambil input horizontal (A/D atau Panah Kanan/Kiri)
        steerInput     = Input.GetAxis("Horizontal");
        handbrakeInput = Input.GetKey(KeyCode.Space) ? 1f : 0f;

        // Input W/S atau Panah Atas/Bawah (Positif = W, Negatif = S)
        float verticalRaw = Input.GetAxis("Vertical");

        // Batas kecepatan di bawah 1 km/h dianggap mobil berhenti total
        float stoppedThreshold = 1.0f;

        // =========================================================================
        // SISTEM TRANSMISI OTOMATIS ARCADE (AUTO REVERSE SHIFTER)
        // =========================================================================
        if (speedKmh <= stoppedThreshold)
        {
            // Jika mobil berhenti dan player nahan W (Maju)
            if (verticalRaw > 0.1f)
            {
                isReverse = false;
                currentGearIndex = 1; // Otomatis masuk Gigi 1
            }
            // Jika mobil berhenti dan player nahan S (Mundur)
            else if (verticalRaw < -0.1f)
            {
                isReverse = true;
                currentGearIndex = 0; // Otomatis masuk Gigi Mundur (R)
            }
        }

        // Tentukan fungsi gas (throttle) dan rem (brake) berdasarkan arah gigi
        if (isReverse)
        {
            // Saat di gigi mundur (R):
            // S (negatif) = Gas mundur, W (positif) = Rem/Deselerasi
            throttleInput = verticalRaw < 0f ? -verticalRaw : 0f;
            brakeInput    = verticalRaw > 0f ?  verticalRaw : 0f;
        }
        else
        {
            // Saat di gigi maju (D):
            // W (positif) = Gas maju, S (negatif) = Rem/Deselerasi
            throttleInput = verticalRaw > 0f ?  verticalRaw : 0f;
            brakeInput    = verticalRaw < 0f ? -verticalRaw : 0f;
        }
        // =========================================================================

        // I = Ignition (On/Off mesin seperti mobil asli)
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (engineRunning)
            {
                engineRunning = false;
            }
            else
            {
                VehicleStatsManager vsm = GetComponent<VehicleStatsManager>();
                bool hasFuel = vsm == null || vsm.currentFuelAmount > 0f || vsm.isPreviewMode;
                if (!hasFuel) return;

                // Cek apakah baterai cukup kuat utk starter
                // Starter mobil butuh daya besar (~1500W) dalam waktu singkat.
                // Minimal baterai harus > starterMinBatteryWh agar voltase tidak drop.
                bool hasStarterPower = vsm == null || vsm.currentBatteryAmount > starterMinBatteryWh || vsm.isPreviewMode;

                if (!hasStarterPower) return;

                engineRunning = true;
            }
        }

        // L = Lampu On/Off
        if (Input.GetKeyDown(KeyCode.L))
            lightsOn = !lightsOn;

        // Transmisi manual (tetap lu pertahankan jika ingin opsional)
        if (transmissionType == TransmissionType.Manual)
        {
            clutchInput = Input.GetKey(KeyCode.LeftShift);
            if (Input.GetKeyDown(KeyCode.E) && _shiftCooldown <= 0f) ShiftUp();
            if (Input.GetKeyDown(KeyCode.Q) && _shiftCooldown <= 0f) ShiftDown();
        }
    }

    #endregion

    #region --- ENGINE & RPM ---

    private void UpdateEngineRPM()
    {
        // Hitung wheel RPM dari drive wheels — selalu update walau mesin mati
        float totalWheelRPM = 0f;
        int   driveCount    = 0;
        foreach (var w in wheels)
        {
            if (!w.isDriveWheel) continue;
            
            if (preventWheelSlipRPM)
            {
                float sign = Vector3.Dot(_rb.velocity, transform.forward) >= 0 ? 1f : -1f;
                float theoreticalWheelRPM = (speedMs * 60f) / (2f * Mathf.PI * w.collider.radius) * sign;
                totalWheelRPM += theoreticalWheelRPM;
            }
            else
            {
                totalWheelRPM += w.collider.rpm;
            }
            driveCount++;
        }
        _wheelRPM = driveCount > 0 ? totalWheelRPM / driveCount : 0f;

        if (!engineRunning)
        {
            // Saat mesin baru dinyalakan, sync RPM ke putaran roda
            if (currentRPM > 1f)
            {
                currentRPM = Mathf.Lerp(currentRPM, 0f, Time.fixedDeltaTime * 3f);
                if (currentRPM < 1f) currentRPM = 0f;
            }
            return;
        }

        // Saat engine baru nyala & RPM masih 0, sync ke putaran roda biar gak loncat
        if (currentRPM < 1f)
        {
            float startGearR = Mathf.Abs(GetCurrentGearRatio());
            currentRPM = Mathf.Abs(_wheelRPM) * startGearR * finalDriveRatio;
            // Dihapus Mathf.Max ke idleRPM agar RPM bisa naik perlahan dari 0 saat distarter
        }

        // Konversi wheel RPM ke engine RPM via gear ratio
        // engineRPM = |wheelRPM| * gearRatio * finalDriveRatio
        float gearR = Mathf.Abs(GetCurrentGearRatio());
        float wheelBasedRPM = Mathf.Abs(_wheelRPM) * gearR * finalDriveRatio;
        wheelBasedRPM = Mathf.Max(wheelBasedRPM, engine.idleRPM);

        // Engine rev-up independent dari wheel saat throttle input (free revving)
        float throttleRevTarget = engine.idleRPM + (throttleInput * (engine.maxRPM - engine.idleRPM));

        // Tentukan apakah kopling terlepas (Free-rev / Launch Control)
        bool isClutchDisengaged = false;
        
        if (transmissionType == TransmissionType.Manual && clutchInput)
            isClutchDisengaged = true;
            
        // ARCADE LAUNCH CONTROL: Jika menekan Handbrake saat mobil berhenti/pelan, kopling dilepas agar bisa burnout.
        // Namun jika sedang mengebut (drifting), handbrake tidak akan memutus transmisi agar RPM tetap tinggi ngikutin roda.
        if (handbrakeInput > 0.5f && speedKmh < 10f)
            isClutchDisengaged = true;

        float targetRPM = wheelBasedRPM;
        
        if (isClutchDisengaged)
        {
            // Kopling lepas: mesin bebas menderu sesuai gas
            targetRPM = throttleRevTarget;
        }
        else
        {
            // TORQUE CONVERTER / AUTO CLUTCH SIMULATION
            // Tarikan awal butuh RPM tinggi (mendekati Peak Torque) agar torsi maksimal bisa keluar.
            // Batas RPM ini disebut "Stall Speed" pada transmisi otomatis.
            float stallSpeedRPM = Mathf.Min(engine.peakTorqueRPM, engine.maxRPM * 0.7f); 
            
            // Saat dari 0km/h dan digas penuh, RPM akan langsung melompat ke stallSpeedRPM
            float slipRPM = throttleInput * (stallSpeedRPM - engine.idleRPM); 
            
            // Efek slip ini akan berkurang secara perlahan dan "mengunci" (lock up) ke putaran roda 
            // ketika kecepatan putaran roda sudah menyamai stall speed.
            float lockUpFactor = Mathf.Clamp01(wheelBasedRPM / stallSpeedRPM); 
            
            targetRPM = wheelBasedRPM + Mathf.Lerp(slipRPM, 0f, lockUpFactor);
        }
        
        targetRPM = Mathf.Clamp(targetRPM, engine.idleRPM, engine.maxRPM);

        // Smooth RPM change dengan flywheel inertia
        float inertiaFactor = 1f / (engine.flywheelInertia + 0.01f);
        currentRPM = Mathf.Lerp(currentRPM, targetRPM, Time.fixedDeltaTime * inertiaFactor * 5f);

        // Hitung engine torque sesuai RPM dan throttle
        float rpmNormalized = Mathf.Clamp01((currentRPM - engine.idleRPM) / (engine.maxRPM - engine.idleRPM));
        float torqueMultiplier = engine.torqueCurve.Evaluate(rpmNormalized);
        engineLoad   = throttleInput;
        currentTorqueNm = engine.maxTorqueNm * torqueMultiplier * throttleInput;
        
        // Konsumsi bensin dinamis (hanya saat mesin nyala)
        // - Bergantung pada RPM
        // - Bergantung pada beban mesin (throttle)
        // - Bergantung pada rasio berat mobil (semakin berat modul/armor, makin boros)
        float loadFactor = Mathf.Lerp(0.1f, 1.0f, throttleInput); // 10% konsumsi saat idle/coasting, 100% saat gas penuh
        float massFactor = _rb.mass / vehicleMass; // vehicleMass = berat standar tanpa modul
        
        currentFuelConsumptionRate = (currentRPM / engine.maxRPM) * loadFactor * massFactor * engine.maxFuelConsumptionRate;
    }

    #endregion

    #region --- DRIVE TORQUE ---

    private void ApplyDriveTorque()
    {
        float gearRatio = GetCurrentGearRatio();
        float totalDriveTorque = 0f;

        // ── DRIVE TORQUE (hanya saat engine nyala) ──
        if (engineRunning)
        {
            totalDriveTorque = currentTorqueNm * gearRatio * finalDriveRatio * transmissionEfficiency;

            // ── REV LIMITER ──
            float gearR = Mathf.Abs(gearRatio);
            float realEngineRPM = Mathf.Abs(_wheelRPM) * gearR * finalDriveRatio;

            float revLimiterFactor = 1f;
            if (realEngineRPM >= engine.maxRPM)
                revLimiterFactor = 0f;
            else if (realEngineRPM > engine.maxRPM * 0.98f)
                revLimiterFactor = Mathf.InverseLerp(engine.maxRPM, engine.maxRPM * 0.98f, realEngineRPM);

            totalDriveTorque *= revLimiterFactor;

            // Clutch slip
            if (transmissionType == TransmissionType.Manual && clutchInput)
                totalDriveTorque *= 0.05f;
        }

        // ── ENGINE BRAKE / COMPRESSION BRAKE (jalan terus walau mesin mati) ──
        if (speedMs > 1f && (!engineRunning || throttleInput < 0.1f))
        {
            float brakeTorque;
            if (engineRunning)
            {
                // Engine brake: hambat kompresi mesin saat throttle lepas
                float fade = 1f - Mathf.Clamp01(throttleInput / 0.1f);
                brakeTorque = (currentRPM / engine.maxRPM) * engineBrakingTorque * fade;
            }
            else
            {
                // Compression brake ringan saat mesin mati (menahan laju kendaraan)
                brakeTorque = compressionBrakeTorque;
            }

            float sign = (_wheelRPM >= 0f) ? -1f : 1f;
            totalDriveTorque = sign * brakeTorque;
        }

        // Terapkan ke roda
        float torquePerWheel = _driveWheelCount > 0 ? totalDriveTorque / _driveWheelCount : 0f;

        foreach (var w in wheels)
        {
            if (w.collider == null) continue;

            if (IsWheelDriven(w))
                w.collider.motorTorque = torquePerWheel;
            else
                w.collider.motorTorque = 0f;
        }
    }

    private bool IsWheelDriven(WheelSetup w)
    {
        if (!w.isDriveWheel) return false;
        // Bisa dikembangkan untuk FWD/RWD/AWD filter per axle
        return true;
    }

    #endregion

    #region --- BRAKING ---

    private void ApplyBraking()
    {
        foreach (var w in wheels)
        {
            if (w.collider == null) continue;

            bool isFront = w.isSteerWheel; // asumsi roda belok = roda depan

            float brakeTorqueApplied = 0f;

            if (brakeInput > 0f)
            {
                float bias = isFront ? brakeBias : (1f - brakeBias);
                brakeTorqueApplied = brakeTorque * brakeInput * bias;
            }

            // Handbrake hanya ke roda belakang
            if (handbrakeInput > 0f && !isFront)
                brakeTorqueApplied = Mathf.Max(brakeTorqueApplied, handbrakeTorque * handbrakeInput);

            w.collider.brakeTorque = brakeTorqueApplied;
        }
    }

    #endregion

    #region --- STEERING ---

    private void ApplySteering()
    {
        float speedNorm  = Mathf.Clamp01(speedKmh / 200f);
        float steerMult  = steerVsSpeedCurve.Evaluate(speedNorm);
        float targetAngle = steerInput * maxSteerAngle * steerMult;

        foreach (var w in wheels)
        {
            if (w.collider == null || !w.isSteerWheel) continue;
            w.collider.steerAngle = Mathf.Lerp(w.collider.steerAngle, targetAngle, Time.fixedDeltaTime * 8f);
        }
    }

    #endregion

    #region --- GEAR SYSTEM ---

    public float GetCurrentGearRatio()
    {
        if (isReverse) return gearRatios[0].ratio;
        int idx = Mathf.Clamp(currentGearIndex, 1, gearRatios.Length - 1);
        return gearRatios[idx].ratio;
    }

    public string GetGearName()
    {
        if (isReverse) return "R";
        if (currentGearIndex == 0) return "N";
        int idx = Mathf.Clamp(currentGearIndex, 1, gearRatios.Length - 1);
        return gearRatios[idx].gearName;
    }

    public void ShiftUp()
    {
        if (isReverse) { isReverse = false; currentGearIndex = 1; return; }
        if (currentGearIndex < gearRatios.Length - 1)
        {
            currentGearIndex++;
            _shiftCooldown = SHIFT_COOLDOWN_TIME;
        }
    }

    public void ShiftDown()
    {
        if (currentGearIndex > 1)
        {
            currentGearIndex--;
            _shiftCooldown = SHIFT_COOLDOWN_TIME;
        }
    }

    public void ToggleReverse()
    {
        if (speedKmh < 3f)
        {
            isReverse = !isReverse;
            if (isReverse) currentGearIndex = 0;
            else currentGearIndex = 1;
        }
    }

    private void HandleAutoTransmission()
    {
        // Update brake hold timer
        if (brakeInput > 0.5f) _brakeHoldTimer += Time.deltaTime;
        else _brakeHoldTimer = 0f;

        if (_shiftCooldown > 0f) return;
        if (isReverse) return;

        // Upshift: Cegah upshift jika handbrake sedang ditarik (agar bisa free-rev / burnout di gigi 1)
        if (currentRPM >= autoUpshiftRPM && currentGearIndex < gearRatios.Length - 1 && handbrakeInput < 0.5f)
        {
            if (throttleInput > 0f) // cuma perlu ada throttle, tidak 0.3f
                ShiftUp();
        }
        // Aggressive Downshift (Rev-Matching / Engine Braking): saat ngerem keras > 1 detik
        else if (_brakeHoldTimer > 1.0f && currentGearIndex > 1)
        {
            float nextGearRatio = Mathf.Abs(gearRatios[currentGearIndex - 1].ratio);
            float projectedRPM = Mathf.Abs(_wheelRPM) * nextGearRatio * finalDriveRatio;
            
            // Downshift lebih agresif (limit sampai 95% redline)
            if (projectedRPM < engine.maxRPM * 0.95f) 
            {
                ShiftDown();
                _shiftCooldown = 0.4f; // Cooldown lebih cepat buat aggressive downshift
            }
        }
        // Kickdown / Stalling Prevention (saat nabrak atau nanjak berat)
        // Jika gas ditahan penuh tapi RPM/kecepatan ngedrop, kita paksa downshift agar dapat torsi!
        else if (throttleInput > 0.8f && currentGearIndex > 1)
        {
            float nextGearRatio = Mathf.Abs(gearRatios[currentGearIndex - 1].ratio);
            float projectedRPM = Mathf.Abs(_wheelRPM) * nextGearRatio * finalDriveRatio;
            
            // Paksa turun gigi jika kita berada di bawah Peak Torque RPM, DAN turun gigi tidak akan bikin mesin meleduk (over-rev)
            if (currentRPM < engine.peakTorqueRPM && projectedRPM < engine.maxRPM * 0.9f)
            {
                ShiftDown();
                _shiftCooldown = 0.3f; // Cooldown dicepatkan agar bisa turun gigi berkali-kali dengan cepat
            }
        }
        // Downshift Normal: lebih smooth saat melambat / lepas gas
        else if (currentRPM <= autoDownshiftRPM && currentGearIndex > 1)
        {
            // Cegah downshift kalau bakal spike RPM ke redline
            float nextGearRatio = Mathf.Abs(gearRatios[currentGearIndex - 1].ratio);
            float projectedRPM = Mathf.Abs(_wheelRPM) * nextGearRatio * finalDriveRatio;
            
            if (projectedRPM < engine.maxRPM * 0.85f) // buffer 85% redline
                ShiftDown();
        }
    }

    #endregion

    #region --- ANTI ROLL BAR ---

    private void ApplyAntiRollBar()
    {
        // Pasangkan per-axle (asumsi 4 roda: 0,1 = depan; 2,3 = belakang)
        if (wheels.Length >= 2) ApplyAntiRollAxle(wheels[0], wheels[1]);
        if (wheels.Length >= 4) ApplyAntiRollAxle(wheels[2], wheels[3]);
    }

    private void ApplyAntiRollAxle(WheelSetup wL, WheelSetup wR)
    {
        WheelHit hitL, hitR;
        bool groundL = wL.collider.GetGroundHit(out hitL);
        bool groundR = wR.collider.GetGroundHit(out hitR);

        float travelL = groundL
            ? (-wL.collider.transform.InverseTransformPoint(hitL.point).y - wL.collider.radius) / wL.collider.suspensionDistance
            : 1f;
        float travelR = groundR
            ? (-wR.collider.transform.InverseTransformPoint(hitR.point).y - wR.collider.radius) / wR.collider.suspensionDistance
            : 1f;

        float antiRollTorque = (travelL - travelR) * antiRollForce;

        if (groundL) _rb.AddForceAtPosition(wL.collider.transform.up * -antiRollTorque, wL.collider.transform.position);
        if (groundR) _rb.AddForceAtPosition(wR.collider.transform.up *  antiRollTorque, wR.collider.transform.position);
    }

    #endregion

    #region --- DOWNFORCE ---

    private void ApplyDownforce()
    {
        if (downforceMultiplier <= 0f) return;
        float force = speedMs * speedMs * downforceMultiplier;
        _rb.AddForce(-transform.up * force);
    }

    #endregion

    #region --- WHEEL MESH ---

    private void UpdateWheelMeshes()
    {
        foreach (var w in wheels)
        {
            if (w.collider == null || w.meshTransform == null) continue;
            Vector3 pos; Quaternion rot;
            w.collider.GetWorldPose(out pos, out rot);
            w.meshTransform.position   = pos;
            w.meshTransform.rotation   = rot;
        }
    }

    #endregion

    #region --- WHEEL PARAMETER LIVE UPDATE ---

    /// <summary>
    /// Terapkan ulang seluruh parameter ke WheelCollider di runtime (bisa dipanggil dari inspector/event)
    /// </summary>
    public void RefreshAllWheelParameters()
    {
        foreach (var w in wheels)
            ApplyWheelColliderSettings(w.collider);
    }

    #endregion

    #region --- APPLY WHEEL SETUP ---

    private void ApplyWheelSetup()
    {
        foreach (var w in wheels)
        {
            if (w.collider == null) continue;
            ApplyWheelColliderSettings(w.collider);
        }
    }

    #endregion

    #region --- SPEEDOMETER ---

    private void UpdateSpeedometer()
    {
        speedMs  = _rb.velocity.magnitude;
        speedKmh = speedMs * 3.6f;
    }

    #endregion

    #region --- GIZMOS ---

    private void OnDrawGizmosSelected()
    {
        // CoM gizmo
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        if (_rb != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_rb.worldCenterOfMass, 0.08f);
        }
    }

    #endregion
}
