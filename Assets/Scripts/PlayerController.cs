using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("=== MOVEMENT ===")]
    public float moveSpeed = 6f;
    public float sprintMultiplier = 1.5f;
    public float acceleration = 12f;

    [Header("=== JUMP ===")]
    public float jumpForce = 6f;

    [Header("=== FPP CAMERA ===")]
    public float mouseSensitivity = 2f;
    public float cameraHeight = 1.6f;

    private CharacterController _controller;
    private float _yaw;
    private float _pitch;
    private Vector3 _verticalVelocity;
    private Vector3 _moveVelocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        if (_controller == null)
            _controller = gameObject.AddComponent<CharacterController>();
    }

    private void OnEnable()
    {
        if (VehicleCamera.Instance != null)
            VehicleCamera.Instance.SetTarget(null);

        Camera cam = Camera.main;
        if (cam == null) return;
        cam.transform.position = transform.position + Vector3.up * cameraHeight;
        cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void Update()
    {
        _yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -90f, 90f);

        transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

        if (_controller.isGrounded)
        {
            Vector3 move = transform.forward * Input.GetAxisRaw("Vertical") + transform.right * Input.GetAxisRaw("Horizontal");
            if (move.magnitude > 1f) move.Normalize();

            float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
            Vector3 targetVel = move * speed;

            float t = 1f - Mathf.Exp(-acceleration * Time.deltaTime);
            _moveVelocity = Vector3.Lerp(_moveVelocity, targetVel, t);
        }

        Vector3 horizontalMove = _moveVelocity * Time.deltaTime;
        horizontalMove.y = 0;

        if (_controller.isGrounded && _verticalVelocity.y < 0)
            _verticalVelocity.y = -2f;

        if (Input.GetKeyDown(KeyCode.Space) && _controller.isGrounded)
            _verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);

        _verticalVelocity.y += Physics.gravity.y * Time.deltaTime;

        _controller.Move(horizontalMove + new Vector3(0, _verticalVelocity.y, 0) * Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (VehicleCamera.Instance != null)
            VehicleCamera.Instance.SetTarget(null);

        Camera cam = Camera.main;
        if (cam == null) return;
        cam.transform.position = transform.position + Vector3.up * cameraHeight;
        cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }
}
