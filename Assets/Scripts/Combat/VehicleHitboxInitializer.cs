using UnityEngine;

public class VehicleHitboxInitializer : MonoBehaviour
{
    private void Awake()
    {
        VehicleStatsManager vsm = GetComponent<VehicleStatsManager>();
        if (vsm == null) return;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            GameObject go = col.gameObject;
            HitboxProxy proxy = go.GetComponent<HitboxProxy>();
            if (proxy == null)
                proxy = go.AddComponent<HitboxProxy>();

            proxy.moduleComponent = go.GetComponentInParent<VehicleModuleComponent>();
            proxy.wheelHealth = go.GetComponentInParent<WheelHealth>();
            proxy.criticalPart = go.GetComponentInParent<VehicleCriticalPart>();
            proxy.simpleTarget = go.GetComponentInParent<SimpleTarget>();
            proxy.statsManager = vsm;
        }
    }
}
