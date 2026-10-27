using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TimeOfDayMenu
{
    [MenuItem("GameObject/NNTI/Time Of Day", false, 21)]
    static void CreateTimeOfDay(MenuCommand command)
    {
        TimeOfDayController existing = Object.FindObjectOfType<TimeOfDayController>();

        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        GameObject go = new GameObject("Time Of Day");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Time Of Day");

        TimeOfDayController controller = go.AddComponent<TimeOfDayController>();
        controller.sunLight = CloudDomeMenu.FindSunLight();

        CloudDome dome = Object.FindObjectOfType<CloudDome>();
        if (dome != null)
            controller.cloudDome = dome;

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }
}
