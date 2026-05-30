using UnityEngine;

public class VehicleCamera : MonoBehaviour
{
    public Transform target; // VehicleController transform
    public float distance = 6f;
    public float height = 2.5f;
    public float mouseSensitivity = 2f;

    private float currentX = 0f;
    private float currentY = 0f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        currentX += Input.GetAxis("Mouse X") * mouseSensitivity;
        currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        currentY = Mathf.Clamp(currentY, -35f, 60f);

        // ESC unlock cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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
    }
}
