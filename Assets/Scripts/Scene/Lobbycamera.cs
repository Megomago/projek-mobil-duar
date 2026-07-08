using UnityEngine;
using Cinemachine;

public class KlikKananKamera : MonoBehaviour
{
    private CinemachineFreeLook kameraGue;
    
    // Variabel buat ngitung waktu
    private float waktuNganggur = 0f;

    [Header("=== SETTING KAMERA UTAMA ===")]
    public float batasWaktuNganggur = 5f; 
    public float kecepatanCinematic = 10f; 

    [Header("=== INVENTORY SHIFT SETTINGS ===")]
    [Tooltip("Centang ini lewat script UI lu pas masuk mode inventory")]
    public bool isInventoryMode = false;
    
    [Tooltip("0.5f = Tengah. Di atas 0.5f (misal 0.7f) bakal geser mobil ke kiri.")]
    [Range(0.1f, 0.9f)]
    public float screenXInventory = 0.72f;
    
    [Tooltip("Seberapa cepat transisi geser kameranya")]
    public float kecepatanTransisi = 5f;

    // Cache komponen Composer dari 3 Rig Cinemachine
    private CinemachineComposer[] rigComposers = new CinemachineComposer[3];

    void Start()
    {
        kameraGue = GetComponent<CinemachineFreeLook>();

        for (int i = 0; i < 3; i++)
        {
            var rig = kameraGue.GetRig(i);
            if (rig != null)
            {
                rigComposers[i] = rig.GetCinemachineComponent<CinemachineComposer>();
            }
        }
    }

    void Update()
    {
        // 1. LOGIKA INPUT & CINEMATIC ROTATION (KODE LAMA LU)
        if (Input.anyKey || Input.GetAxis("Mouse X") != 0 || Input.GetAxis("Mouse Y") != 0 || Input.GetAxis("Mouse ScrollWheel") != 0)
        {
            waktuNganggur = 0f;
        }

        if (Input.GetMouseButton(1)) 
        {
            kameraGue.m_XAxis.m_InputAxisName = "Mouse X";
            kameraGue.m_YAxis.m_InputAxisName = "Mouse Y";
        }
        else 
        {
            kameraGue.m_XAxis.m_InputAxisName = "";
            kameraGue.m_YAxis.m_InputAxisName = "";
            kameraGue.m_XAxis.m_InputAxisValue = 0;
            kameraGue.m_YAxis.m_InputAxisValue = 0;

            waktuNganggur += Time.deltaTime;

            if (waktuNganggur >= batasWaktuNganggur)
            {
                kameraGue.m_XAxis.Value += kecepatanCinematic * Time.deltaTime;
            }
        }

        float targetScreenX = isInventoryMode ? screenXInventory : 0.5f;

        // Terapkan transisi halus (Lerp) ke semua Rig Composer
        for (int i = 0; i < 3; i++)
        {
            if (rigComposers[i] != null)
            {
                rigComposers[i].m_ScreenX = Mathf.Lerp(
                    rigComposers[i].m_ScreenX, 
                    targetScreenX, 
                    Time.deltaTime * kecepatanTransisi
                );
            }
        }

    }
    
    public void ToggleInventoryMode(bool aktif)
    {
        isInventoryMode = aktif;
    }
}