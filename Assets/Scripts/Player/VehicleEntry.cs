using UnityEngine;

public class VehicleEntry : MonoBehaviour
{
    /// <summary>
    /// Kendaraan yang sedang dikendarai player GLOBAL. Mencegah masuk 2 kendaraan
    /// sekaligus (2 VehicleEntry overlap → 2 set HUD numpuk).
    /// </summary>
    public static VehicleEntry ActiveVehicle;

    [Header("=== EXIT POINT ===")]
    [Tooltip("Posisi player saat teleport keluar mobil")]
    public Transform exitPoint;

    [Header("=== PROMPT ===")]
    [Tooltip("GameObject prompt UI (misal: 'Press E')")]
    public GameObject interactionPrompt;

    [Header("=== SETTINGS ===")]
    public KeyCode enterExitKey = KeyCode.E;

    private PlayerController _player;
    private bool _playerInRange;
    private bool _isOccupied;

    private VehicleController _vehicleController;
    private VehicleGridWeaponTrigger _weaponTrigger;
    private Coroutine _exitSettleCoroutine;

    private void Awake()
    {
        _vehicleController = GetComponent<VehicleController>();
        _weaponTrigger = GetComponent<VehicleGridWeaponTrigger>();

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isOccupied) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null && pc.gameObject.activeInHierarchy)
        {
            _player = pc;
            _playerInRange = true;
            if (interactionPrompt != null)
                interactionPrompt.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_isOccupied) return;

        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null && pc == _player)
        {
            _playerInRange = false;
            _player = null;
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(enterExitKey))
        {
            if (_isOccupied)
                ExitVehicle();
            else if (_playerInRange && _player != null)
                EnterVehicle();
        }
    }

    public void EnterVehicle()
    {
        if (_player == null) return;

        // Guard global: kalau player masih di kendaraan lain, keluarkan dulu
        if (ActiveVehicle != null && ActiveVehicle != this)
        {
            ActiveVehicle.ExitVehicle();
        }

        _isOccupied = true;
        ActiveVehicle = this;

        // Player masuk kembali sebelum mobil settle → batalkan rencana freeze,
        // biarkan mobil dinamis supaya bisa dikendarai.
        if (_exitSettleCoroutine != null)
        {
            StopCoroutine(_exitSettleCoroutine);
            _exitSettleCoroutine = null;
        }

        _player.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (_vehicleController != null)
            _vehicleController.SetMovementLocked(false);

        EnableTurrets(true);

        if (_weaponTrigger != null)
        {
            _weaponTrigger.usePlayerInput = true;
            _weaponTrigger.InitializeWeapons();
        }

        var hudSpawner = GetComponent<VehicleHUDSpawner>();
        if (hudSpawner != null)
            hudSpawner.ReinitializeHUD();

        if (VehicleCamera.Instance != null)
            VehicleCamera.Instance.SetTarget(transform);

        if (interactionPrompt != null)
        {
            interactionPrompt.SetActive(true);
            var tmp = interactionPrompt.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.text = "Press E to exit";
        }

        #if UNITY_EDITOR
        Debug.Log("[VehicleEntry] Entered vehicle: " + gameObject.name);
        #endif
    }

    public void ExitVehicle()
    {
        if (!_isOccupied) return;
        if (ActiveVehicle != null && ActiveVehicle != this) return;

        _isOccupied = false;
        ActiveVehicle = null;

        if (_player == null)
        {
            Debug.LogWarning("[VehicleEntry] Player reference hilang, spawn ulang...");
            _player = FindObjectOfType<PlayerController>();
            if (_player == null) return;
        }

        Vector3 exitPos = exitPoint ? exitPoint.position : transform.position + transform.right * 2f;
        _player.transform.position = exitPos;
        _player.transform.rotation = Quaternion.identity;

        _player.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_weaponTrigger != null)
            _weaponTrigger.ClearHUDs();

        var hudSpawner = GetComponent<VehicleHUDSpawner>();
        if (hudSpawner != null)
            hudSpawner.ClearHUD();

        if (_vehicleController != null)
        {
            // Kunci INPUT langsung: kalau ditunda, WASD jalan kaki player ikut
            // menyetir mobil (GatherInput masih jalan) dan wheel torque tersisa
            // bikin mobil nggas sendiri. freezeRigidbody:false → kinematic ditunda,
            // biar mobil tetap bisa jatuh/settle dulu (bukan melayang beku).
            _vehicleController.SetMovementLocked(true, freezeRigidbody: false);

            // Inersia TETAP (mobil tetap meluncur dari kecepatan saat player turun —
            // berhenti alami oleh gesekan). Yang dibuang adalah torsi setir sendiri
            // (ReleaseWheelControls di SetMovementLocked), jadi mobil tidak nggas/
            // muter sendiri setelah ditinggal.

            // Freeze kinematic setelah mobil benar-benar berhenti menyentuh tanah.
            if (_exitSettleCoroutine != null) StopCoroutine(_exitSettleCoroutine);
            _exitSettleCoroutine = StartCoroutine(SettleThenLock());
        }

        EnableTurrets(false);

        if (_weaponTrigger != null)
            _weaponTrigger.usePlayerInput = false;

        if (VehicleCamera.Instance != null)
            VehicleCamera.Instance.SetTarget(null);

        if (interactionPrompt != null)
        {
            var tmp = interactionPrompt.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null) tmp.text = "Press E to enter";
            interactionPrompt.SetActive(false);
        }

        _playerInRange = false;
        _player = null;

        #if UNITY_EDITOR
        Debug.Log("[VehicleEntry] Exited vehicle: " + gameObject.name);
        #endif
    }

    private void OnDestroy()
    {
        // Kendaraan hancur/despawn saat player masih di dalam → lepaskan player biar tidak terkunci
        if (ActiveVehicle == this)
        {
            ActiveVehicle = null;
            if (_player != null)
            {
                _player.gameObject.SetActive(true);
                _player.transform.position = exitPoint != null ? exitPoint.position : transform.position + transform.right * 2f;
            }
        }
    }

    /// <summary>
    /// Tunggu rigidbody berhenti (IsSleeping) setelah mobil meluncur sampai berhenti
    /// alami, lalu freeze kinematic (parkir). Kalau player masuk lagi, coroutine
    /// dibatalkan di EnterVehicle. Timeout → biarkan dinamis (jangan freeze di udara).
    /// </summary>
    private System.Collections.IEnumerator SettleThenLock()
    {
        float timeout = 4f;
        float t = 0f;
        Rigidbody rb = _vehicleController != null ? _vehicleController.GetComponent<Rigidbody>() : null;

        while (t < timeout)
        {
            if (_isOccupied) yield break; // player sudah naik lagi — jangan lock
            if (rb == null) yield break;

            if (rb.IsSleeping())
            {
                if (_vehicleController != null)
                    _vehicleController.SetMovementLocked(true);
                _exitSettleCoroutine = null;
                yield break;
            }

            t += Time.deltaTime;
            yield return null;
        }

        // Timeout: mobil belum settle (misal limbung di bebatuan) → biarkan dinamis,
        // nanti berhenti sendiri oleh gravitasi/fisika.
        _exitSettleCoroutine = null;
    }

    private void EnableTurrets(bool on)
    {
        var turrets = GetComponentsInChildren<Weapons.ManualTurretController>(true);
        foreach (var t in turrets)
            if (t != null) t.enabled = on;
    }
}
