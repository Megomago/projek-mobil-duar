using UnityEngine;
using UnityEditor;

public class ClearPlayerPrefs
{
    [MenuItem("Tools/Clear PlayerPrefs (Reset Data)")]
    public static void ClearPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("Semua data PlayerPrefs berhasil dihapus! Senjata lama yang nyangkut sudah hilang.");
    }
}
