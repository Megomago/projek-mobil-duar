using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ParticleTintMenu
{
    const string SelectedMenuPath = "GameObject/NNTI/Add Day Night Tint To Selected Particle";
    const string TerrainDustMenuPath = "GameObject/NNTI/Add Day Night Tint To Terrain Dust";
    const string MaterialFolder = "Assets/Scripts/Environment/Materials";
    const string MaterialPath = MaterialFolder + "/DustParticle.mat";
    const string ShaderName = "Custom/DustParticleAlpha";

    [MenuItem(SelectedMenuPath, false, 22)]
    static void AddTintToSelected(MenuCommand command)
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
            return;

        if (AddTint(selected))
            EditorSceneManager.MarkSceneDirty(selected.scene);
    }

    [MenuItem(SelectedMenuPath, true)]
    static bool ValidateAddTintToSelected()
    {
        GameObject selected = Selection.activeGameObject;
        return selected != null && selected.GetComponent<ParticleSystem>() != null;
    }

    [MenuItem(TerrainDustMenuPath, false, 23)]
    static void AddTintToTerrainDust(MenuCommand command)
    {
        ParticleSystem[] systems = Object.FindObjectsOfType<ParticleSystem>();
        int found = 0;
        int added = 0;

        foreach (ParticleSystem system in systems)
        {
            Transform parent = system.transform.parent;

            if (parent == null || !parent.name.StartsWith("Terrain_"))
                continue;

            found++;

            if (AddTint(system.gameObject))
            {
                added++;
                EditorSceneManager.MarkSceneDirty(system.gameObject.scene);
            }
        }

        if (found == 0)
        {
            EditorUtility.DisplayDialog(
                "Day Night Tint",
                "Tidak ada Particle System di bawah object Terrain_.\n" +
                "Pilih object Particle System-nya lalu pakai menu " +
                "'Add Day Night Tint To Selected Particle'.",
                "OK");
            return;
        }

        if (added == 0)
        {
            EditorUtility.DisplayDialog(
                "Day Night Tint",
                "Semua Particle System di bawah Terrain_ sudah punya Particle Day Night Tint.",
                "OK");
            return;
        }

        Debug.Log("Day Night Tint disiapkan untuk " + added + " particle system di bawah Terrain_.");
    }

    [MenuItem(TerrainDustMenuPath, true)]
    static bool ValidateAddTintToTerrainDust()
    {
        foreach (ParticleSystem system in Object.FindObjectsOfType<ParticleSystem>())
        {
            Transform parent = system.transform.parent;

            if (parent != null && parent.name.StartsWith("Terrain_"))
                return true;
        }

        return false;
    }

    static bool AddTint(GameObject target)
    {
        if (target == null)
            return false;

        ParticleSystemRenderer renderer = target.GetComponent<ParticleSystemRenderer>();

        if (renderer == null)
            return false;

        Material dustMaterial = GetOrCreateDustMaterial(renderer.sharedMaterial);

        if (dustMaterial == null)
        {
            EditorUtility.DisplayDialog(
                "Day Night Tint",
                "Shader " + ShaderName + " belum ke-import. Tunggu Unity selesai compile lalu coba lagi.",
                "OK");
            return false;
        }

        ParticleDayNightTint tint = target.GetComponent<ParticleDayNightTint>();

        if (tint == null)
            tint = Undo.AddComponent<ParticleDayNightTint>(target);
        else
            Undo.RecordObject(tint, "Update Day Night Tint");

        tint.timeOfDay = Object.FindObjectOfType<TimeOfDayController>();
        tint.dustMaterial = dustMaterial;

        Undo.RecordObject(renderer, "Assign Dust Material");
        renderer.sharedMaterial = dustMaterial;

        EditorUtility.SetDirty(tint);
        EditorUtility.SetDirty(renderer);

        Selection.activeGameObject = target;
        EditorGUIUtility.PingObject(target);

        return true;
    }

    static Material GetOrCreateDustMaterial(Material sourceMaterial)
    {
        Shader shader = Shader.Find(ShaderName);

        if (shader == null)
            return null;

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                string parent = MaterialFolder.Substring(0, MaterialFolder.LastIndexOf('/'));

                if (!AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder("Assets/Scripts", "Environment");

                AssetDatabase.CreateFolder(parent, "Materials");
            }

            material = new Material(shader);
            material.name = "DustParticle";
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (sourceMaterial != null && sourceMaterial != material
            && sourceMaterial.HasProperty("_MainTex") && material.HasProperty("_MainTex"))
        {
            Texture texture = sourceMaterial.GetTexture("_MainTex");

            if (texture != null)
                material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", new Color(0.80f, 0.72f, 0.58f, 1f));

        if (material.HasProperty("_Alpha"))
            material.SetFloat("_Alpha", 0.35f);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return material;
    }
}

