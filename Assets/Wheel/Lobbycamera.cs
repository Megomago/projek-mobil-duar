using UnityEngine;
using Cinemachine;

public class KlikKananKamera : MonoBehaviour
{
    private CinemachineFreeLook kameraGue;
    
    // Variabel buat ngitung waktu
    private float waktuNganggur = 0f;
    
    // GUE SET 5 DETIK DULU BIAR LU BISA NGETES! 
    // Kalo lu set 30 detik sekarang, ntar lu bengong depan laptop nungguinnya kayak orang bego.
    public float batasWaktuNganggur = 5f; 
    
    // Kecepatan puteran cinematic (bisa lu ganti-ganti di Inspector nanti)
    public float kecepatanCinematic = 10f; 

    void Start()
    {
        kameraGue = GetComponent<CinemachineFreeLook>();
    }

    void Update()
    {
        // Kalo lagi klik kanan...
        if (Input.GetMouseButton(1)) 
        {
            waktuNganggur = 0f; // Reset stopwatch ke 0

            kameraGue.m_XAxis.m_InputAxisName = "Mouse X";
            kameraGue.m_YAxis.m_InputAxisName = "Mouse Y";
        }
        else 
        {
            kameraGue.m_XAxis.m_InputAxisName = "";
            kameraGue.m_YAxis.m_InputAxisName = "";
            kameraGue.m_XAxis.m_InputAxisValue = 0;
            kameraGue.m_YAxis.m_InputAxisValue = 0;

            // Kalo klik kanan dilepas, stopwatch mulai jalan
            // Time.deltaTime itu waktu antar frame, biar hitungan detiknya akurat
            waktuNganggur += Time.deltaTime;

            // Kalo waktu nganggur udah ngelewatin batas (misal 5 detik)...
            if (waktuNganggur >= batasWaktuNganggur)
            {
                // Paksa X-Axis (puteran kiri-kanan) jalan sendiri pelan-pelan
                kameraGue.m_XAxis.Value += kecepatanCinematic * Time.deltaTime;
                
                // Opsional: Kalo lu mau kameranya juga otomatis turun/naik dikit pas cinematic,
                // lu bisa mainin m_YAxis.Value juga di sini, tapi ntar lu pusing. Gini aja dulu.
            }
        }
    }
}