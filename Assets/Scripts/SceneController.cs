using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    [Header("Scene Names")]
    public string battlefieldSceneName = "Battlefield";
    public string garasiSceneName = "Garasi";

    void Update()
    {
        // Jika tombol Escape (ESC) ditekan, langsung kembali ke Garasi
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoToGarasi();
        }
    }

    // Fungsi ini dipanggil dari Button 1 di scene Garasi
    public void GoToBattlefield()
    {
        Debug.Log("Pindah ke scene: " + battlefieldSceneName);
        SceneManager.LoadScene(battlefieldSceneName);
    }

    // Fungsi ini dipanggil untuk kembali ke Garasi (bisa dipanggil via UI Button custom)
    public void GoToGarasi()
    {
        Debug.Log("Kembali ke scene: " + garasiSceneName);
        
        // Buka kembali kursor agar bisa klik UI di Garasi
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Bisa juga menggunakan SceneManager.LoadScene(0) jika Garasi ada di index 0 pada Build Settings
        SceneManager.LoadScene(garasiSceneName);
    }
}
