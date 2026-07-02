using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Kinematic Projectile - Sub-step raycast projectile logic.
    /// Sangat ringan, menggunakan Raycast alih-alih Rigidbody collision untuk mencegah projectile menembus dinding.
    /// </summary>
    public class KinematicProjectile : MonoBehaviour
    {
        [Header("Projectile Stats")]
        [Tooltip("Lifetime peluru dalam detik sebelum hancur otomatis")]
        public float maxLifetime = 5f;
        
        [Tooltip("Massa peluru (kg), berpengaruh pada push force ke target jika target punya Rigidbody")]
        public float mass = 0.05f;

        [Tooltip("Layer mask untuk mendeteksi apa yang bisa ditembak")]
        public LayerMask hitMask;

        [Header("Visuals (Opsional)")]
        [Tooltip("Efek partikel saat peluru mengenai sesuatu")]
        public GameObject hitImpactPrefab;

        // Internal State
        private System.Collections.Generic.HashSet<Collider> _piercedColliders = new System.Collections.Generic.HashSet<Collider>();
        private Vector3 _currentVelocity;
        private Vector3 _currentPosition;
        private float _aliveTime;
        private TrailRenderer _trailRenderer;
        private float _atk;
        private float _pen;
        // Pembatas spawn impact VFX per frame (ANTI LAG SPIKE)
        private static float _lastImpactFrameTime;
        private static int _impactsThisFrame;
        private const int MAX_IMPACTS_PER_FRAME = 10; // Batasi maks 3 efek ledakan per frame

        // Gravitasi bumi (-9.81), bisa diubah jika butuh balistik spesifik
        private readonly Vector3 _gravity = new Vector3(0f, -9.81f, 0f);

        private void Awake()
        {
            _trailRenderer = GetComponent<TrailRenderer>();
        }

        /// <summary>
        /// Dipanggil oleh senjata saat menembakkan peluru ini.
        /// </summary>
        /// <param name="startPos">Posisi keluar laras</param>
        /// <param name="startDir">Arah tembakan</param>
        /// <param name="muzzleVelocity">Kecepatan awal (m/s)</param>
        /// <param name="atk">Attack power</param>
        /// <param name="pen">Penetration power</param>
        public void Initialize(Vector3 startPos, Vector3 startDir, float muzzleVelocity, float atk, float pen)
        {
            _currentPosition = startPos;
            transform.position = startPos;
            transform.forward = startDir;

            _currentVelocity = startDir.normalized * muzzleVelocity;
            _aliveTime = 0f;
            _atk = atk;
            _pen = pen;

            // FIX: Bersihkan jejak TrailRenderer kalau peluru ini hasil daur ulang dari Object Pool
            // Biar gak ngebentuk "laser" dari posisi matinya peluru balik ke ujung laras
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }

            _piercedColliders.Clear();
        }

        private void Update()
        {
            _aliveTime += Time.deltaTime;

            // 1. Cek umur peluru (lifetime)
            if (_aliveTime >= maxLifetime)
            {
                DestroyProjectile();
                return;
            }

            // 2. Terapkan gaya gravitasi ke kecepatan saat ini
            _currentVelocity += _gravity * Time.deltaTime;

            // 3. Prediksi posisi berikutnya
            Vector3 nextPosition = _currentPosition + (_currentVelocity * Time.deltaTime);
            Vector3 directionToNext = nextPosition - _currentPosition;
            float distanceToNext = directionToNext.magnitude;

            // 4. Lakukan Raycast dari posisi saat ini ke posisi berikutnya menggunakan RaycastAll agar bisa ignore collider yg sudah ditembus
            RaycastHit[] hits = Physics.RaycastAll(_currentPosition, directionToNext.normalized, distanceToNext, hitMask);
            
            RaycastHit closestHit = default;
            bool foundHit = false;
            float minDistance = float.MaxValue;

            foreach (var h in hits)
            {
                // Ignore collider yang sudah pernah kita tembus di frame sebelumnya
                if (_piercedColliders.Contains(h.collider)) 
                    continue;

                // Jangan abaikan distance == 0 secara umum, karena bisa jadi itu adalah armor plate 
                // yang letaknya menempel erat (adjacent) dan kita berada tepat di perbatasannya.

                if (h.distance < minDistance)
                {
                    minDistance = h.distance;
                    closestHit = h;
                    foundHit = true;
                }
            }

            if (foundHit)
            {
                HandleHit(closestHit);
                return; // Berhenti memproses posisi, karena peluru menabrak sesuatu
            }

            // 5. Jika tidak menabrak, pindahkan peluru ke posisi baru
            _currentPosition = nextPosition;
            transform.position = _currentPosition;
            transform.forward = _currentVelocity.normalized; // Putar visual peluru menghadap arah jatuhnya
        }

        private void HandleHit(RaycastHit hit)
        {
            OptResult? result = null;
            var statsMgr = hit.collider.GetComponentInParent<VehicleStatsManager>();

            // 1. Coba damage ke modul grid (VehicleStatsManager -> PlacedModule)
            if (statsMgr != null)
            {
                foreach (var mod in statsMgr.installedModules)
                {
                    if (mod.spawnedPrefab != null && IsChildOrSelf(hit.collider.transform, mod.spawnedPrefab.transform))
                    {
                        result = ApplyModuleDamage(mod, statsMgr);
                        goto AFTER_DAMAGE;
                    }
                }
            }

            // 1.5 Coba damage ke individual wheel (sistem baru)
            WheelHealth wheelHealth = hit.collider.GetComponentInParent<WheelHealth>();
            if (wheelHealth != null)
            {
                result = OptFormula.Calculate(_atk, _pen, wheelHealth.armor, _currentVelocity.magnitude);
                wheelHealth.TakeDamage(result.Value.damage);
                Debug.Log($"[WHEEL] {wheelHealth.gameObject.name} ATK:{_atk} PEN:{_pen} DEF:{wheelHealth.armor} → DMG:{result.Value.damage} PIERCE:{result.Value.pierce} EXIT:{result.Value.exitVel} | HP:{wheelHealth.currentHealth}/{wheelHealth.maxHealth}");
                goto AFTER_DAMAGE;
            }

            // 2. Coba damage ke vehicle body parts
            // Gunakan nama collider: "BodyHitbox", "EngineHitbox", "WheelHitbox", "BatteryHitbox"
            string cname = hit.collider.name;
            if (statsMgr != null && statsMgr.baseData != null)
            {
                float def = 0f;
                string partName = "";

                if (cname.Contains("BodyHitbox")) { def = statsMgr.baseData.bodyArmor; partName = "Body"; }
                else if (cname.Contains("EngineHitbox")) { def = statsMgr.baseData.engineArmor; partName = "Engine"; }
                else if (cname.Contains("WheelHitbox")) { def = statsMgr.baseData.wheelArmor; partName = "Wheel"; }
                else if (cname.Contains("BatteryHitbox"))
                {
                    bool separateBattery = statsMgr.baseData != null && !statsMgr.baseData.isBatteryJoinEngine;
                    if (separateBattery)
                    {
                        def = statsMgr.baseData.batteryArmor;
                        partName = "Battery";
                    }
                }

                if (def > 0f)
                {
                    result = OptFormula.Calculate(_atk, _pen, def, _currentVelocity.magnitude);
                    Debug.Log($"[BODY] {partName} ATK:{_atk} PEN:{_pen} DEF:{def} → DMG:{result.Value.damage} PIERCE:{result.Value.pierce} EXIT:{result.Value.exitVel}");

                    switch (partName)
                    {
                        case "Body": statsMgr.currentBodyHealth -= result.Value.damage; break;
                        case "Engine": statsMgr.currentEngineHealth -= result.Value.damage; break;
                        case "Wheel": statsMgr.currentWheelHealth -= result.Value.damage; break;
                        case "Battery": statsMgr.currentBatteryHealth -= result.Value.damage; break;
                    }
                }
            }
            else
            {
                // 3. Coba target lain (misal SimpleTarget)
                var simpleTarget = hit.collider.GetComponentInParent<SimpleTarget>();
                if (simpleTarget != null)
                {
                    result = simpleTarget.TakeDamage(_atk, _pen, _currentVelocity.magnitude);
                }
            }

        AFTER_DAMAGE:
            // Spawn efek ledakan/percikan api dengan pembatas maksimal per frame
            if (hitImpactPrefab != null)
            {
                if (Time.time != _lastImpactFrameTime)
                {
                    _lastImpactFrameTime = Time.time;
                    _impactsThisFrame = 0;
                }

                if (_impactsThisFrame < MAX_IMPACTS_PER_FRAME)
                {
                    ObjectPool.Instance.Spawn(hitImpactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    _impactsThisFrame++;
                }
            }

            // Tambahkan dorongan fisik (Push) jika target adalah Rigidbody
            Rigidbody hitRb = hit.collider.GetComponentInParent<Rigidbody>();
            if (hitRb != null)
            {
                Vector3 force = _currentVelocity.normalized * (_currentVelocity.magnitude * mass);
                hitRb.AddForceAtPosition(force, hit.point, ForceMode.Impulse);
            }

            // Pierce: kalau tembus, lanjut terbang dengan exit velocity
            if (result.HasValue && result.Value.pierce && result.Value.exitVel > 0f)
            {
                _piercedColliders.Add(hit.collider); // Masukkan ke daftar tembus agar tidak di-hit lagi selamanya oleh peluru ini
                
                // Pindahkan posisi peluru sedikit ke depan (searah velocity) untuk keluar dari permukaan collider
                _currentPosition = hit.point + _currentVelocity.normalized * 0.01f;
                _currentVelocity = _currentVelocity.normalized * result.Value.exitVel;
                
                // PENETRATION DROP: Peluru kehilangan daya tembusnya
                _pen = result.Value.remainingPen;
                _atk = result.Value.remainingAtk;
                
                transform.position = _currentPosition;
                transform.forward = _currentVelocity.normalized;
                return;
            }

            // Hancurkan peluru (Kembalikan ke pool)
            DestroyProjectile();
        }

        private OptResult ApplyModuleDamage(PlacedModule mod, VehicleStatsManager mgr)
        {
            float def = mod.moduleTemplate.armor;
            var r = OptFormula.Calculate(_atk, _pen, def, _currentVelocity.magnitude);
            mod.currentHealth -= r.damage;
            Debug.Log($"[MODULE] {mod.moduleTemplate.moduleName} ATK:{_atk} PEN:{_pen} DEF:{def} → DMG:{r.damage} PIERCE:{r.pierce} EXIT:{r.exitVel} | HP:{mod.currentHealth}/{mod.moduleTemplate.maxHealth}");

            if (mod.currentHealth <= 0f)
            {
                bool wasVolatile = mod.moduleTemplate.volatileExplosive;
                mgr.UninstallModule(mod);
                if (wasVolatile) TriggerChainExplosion(mod, mgr);
            }

            return r;
        }

        private void TriggerChainExplosion(PlacedModule sourceMod, VehicleStatsManager mgr)
        {
            if (sourceMod.moduleTemplate == null) return;
            int radius = sourceMod.moduleTemplate.explosionRadius;
            float dmg = sourceMod.moduleTemplate.explosionDamage;
            Vector2Int srcPos = sourceMod.gridPosition;
            string srcZone = sourceMod.zoneName;

            foreach (var mod in mgr.installedModules)
            {
                if (mod == sourceMod || mod.moduleTemplate == null || mod.zoneName != srcZone) continue;
                int dx = Mathf.Abs(mod.gridPosition.x - srcPos.x);
                int dy = Mathf.Abs(mod.gridPosition.y - srcPos.y);
                if (dx <= radius && dy <= radius)
                {
                    mod.currentHealth -= dmg;
                    Debug.Log($"[CHAIN] {mod.moduleTemplate.moduleName} kena ledakan {dmg} → HP:{mod.currentHealth}");
                    if (mod.currentHealth <= 0f) mgr.UninstallModule(mod);
                }
            }
        }

        private bool IsChildOrSelf(Transform child, Transform parent)
        {
            while (child != null)
            {
                if (child == parent) return true;
                child = child.parent;
            }
            return false;
        }
        private void DestroyProjectile()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
