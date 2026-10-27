using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class CloudDomeMenu
{
    const string ShaderPath = "Assets/Scripts/Environment/CloudDome.shader";

    [MenuItem("GameObject/NNTI/Cloud Dome", false, 20)]
    static void CreateCloudDome(MenuCommand command)
    {
        CloudDome existing = Object.FindObjectOfType<CloudDome>();

        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            return;
        }

        GameObject go = new GameObject("Cloud Dome");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create Cloud Dome");

        go.AddComponent<MeshFilter>();

        MeshRenderer renderer = go.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        CloudDome dome = go.AddComponent<CloudDome>();
        dome.cloudShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        dome.sunLight = FindSunLight();

        Selection.activeGameObject = go;
        EditorSceneManager.MarkSceneDirty(go.scene);
    }

    internal static Light FindSunLight()
    {
        if (RenderSettings.sun != null)
            return RenderSettings.sun;

        Light[] lights = Object.FindObjectsOfType<Light>();

        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional && light.isActiveAndEnabled)
                return light;
        }

        return null;
    }
}
