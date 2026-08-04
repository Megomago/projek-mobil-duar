using UnityEngine;

public class VehicleEntry : MonoBehaviour
{
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
        _isOccupied = true;

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
        _isOccupied = false;

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
            _vehicleController.SetMovementLocked(true);

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

    private void EnableTurrets(bool on)
    {
        var turrets = GetComponentsInChildren<Weapons.ManualTurretController>(true);
        foreach (var t in turrets)
            if (t != null) t.enabled = on;
    }
}
