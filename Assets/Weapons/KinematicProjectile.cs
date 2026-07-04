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
        private VehicleStatsManager _ownerStatsManager;

        // Explosive State (di-set oleh ModularWeapon via SetExplosive)
        private bool _isExplosive;
        private float _explosiveRadius;
        private float _explosiveDamage;
        private float _explosiveForce;
        private GameObject _explosionVFXPrefab;
        private AudioClip _explosionSFX;

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
        public void Initialize(Vector3 startPos, Vector3 startDir, float muzzleVelocity, float atk, float pen, VehicleStatsManager owner = null)
        {
            _currentPosition = startPos;
            transform.position = startPos;
            transform.forward = startDir;

            _currentVelocity = startDir.normalized * muzzleVelocity;
            _aliveTime = 0f;
            _atk = atk;
            _pen = pen;
            _ownerStatsManager = owner;

            // FIX: Bersihkan jejak TrailRenderer kalau peluru ini hasil daur ulang dari Object Pool
            // Biar gak ngebentuk "laser" dari posisi matinya peluru balik ke ujung laras
            if (_trailRenderer != null)
            {
                _trailRenderer.Clear();
            }

            _piercedColliders.Clear();

            // Reset explosive state tiap kali dipake ulang dari pool
            _isExplosive = false;
            _explosiveRadius = 0f;
            _explosiveDamage = 0f;
            _explosiveForce = 0f;
            _explosionVFXPrefab = null;
            _explosionSFX = null;
        }

        public void SetExplosive(bool isExplosive, float radius, float damage, float force, GameObject vfx, AudioClip sfx)
        {
            _isExplosive = isExplosive;
            _explosiveRadius = radius;
            _explosiveDamage = damage;
            _explosiveForce = force;
            _explosionVFXPrefab = vfx;
            _explosionSFX = sfx;
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
            // Skip hits on the shooter's own vehicle — prevent self-damage
            var statsMgr = hit.collider.GetComponentInParent<VehicleStatsManager>();
            if (statsMgr != null && _ownerStatsManager != null && statsMgr == _ownerStatsManager)
                return;

            // Explosive ammo: bypass kinetic damage pipeline, langsung detonate
            if (_isExplosive)
            {
                ExplosionManager.Detonate(hit.point, _explosiveRadius, _explosiveDamage, _explosiveForce, statsMgr, hitMask, _ownerStatsManager);

                if (_explosionVFXPrefab != null && ObjectPool.Instance != null)
                {
                    GameObject vfx = ObjectPool.Instance.Spawn(_explosionVFXPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                    if (vfx != null)
                    {
                        float vfxScale = _explosiveRadius * 0.35f;
                        vfx.transform.localScale = Vector3.one * Mathf.Max(vfxScale, 0.5f);
                    }
                }

                if (_explosionSFX != null)
                    AudioSource.PlayClipAtPoint(_explosionSFX, hit.point);

                #if UNITY_EDITOR
                Debug.DrawLine(hit.point + Vector3.left * 0.3f, hit.point + Vector3.right * 0.3f, Color.red, 3f);
                Debug.DrawLine(hit.point + Vector3.up * 0.3f, hit.point + Vector3.down * 0.3f, Color.red, 3f);
                Debug.DrawLine(hit.point + Vector3.forward * 0.3f, hit.point + Vector3.back * 0.3f, Color.red, 3f);
                Debug.Log($"[EXPLOSIVE] {_explosiveDamage} dmg radius {_explosiveRadius}m at {hit.point}");
                #endif

                DestroyProjectile();
                return;
            }

            OptResult? result = null;
            string dbgType = "unknown";

            // ── Priority-based damage detection ──
            // Semua GetComponentInParent unconditional, urutan prioritas:
            //   1. Module (dictionary > GetComponentInParent)
            //   2. Wheel
            //   3. Critical Part
            //   4. SimpleTarget
            //   5. Chassis body (hanya jika statsMgr ada)

            // Cari semua komponen target sekali di awal
            var modComp = hit.collider.GetComponentInParent<VehicleModuleComponent>();
            var wheelHealth = hit.collider.GetComponentInParent<WheelHealth>();
            var critPart = hit.collider.GetComponentInParent<VehicleCriticalPart>();
            var simpleTarget = hit.collider.GetComponentInParent<SimpleTarget>();

            // Debug: kirim log nama target + hierarchy depth
            #if UNITY_EDITOR
            Transform depthCheck = hit.collider.transform;
            int depth = 0;
            string chain = hit.collider.gameObject.name;
            while (depthCheck.parent != null && depth < 20)
            {
                depthCheck = depthCheck.parent;
                chain += " > " + depthCheck.gameObject.name;
                depth++;
            }
            Debug.Log($"[HIT] '{hit.collider.gameObject.name}' depth={depth} chain={chain} | modComp={(modComp != null ? modComp.gameObject.name : "null")} wheel={(wheelHealth != null)} crit={(critPart != null)} target={(simpleTarget != null)} statsMgr={(statsMgr != null ? statsMgr.gameObject.name : "null")}");
            #endif

            // 1. Module — dictionary lookup dulu, fallback GetComponentInParent
            if (statsMgr != null && statsMgr.moduleColliderMap.TryGetValue(hit.collider, out var hitModule))
            {
                dbgType = "module";
                result = ApplyModuleDamage(hitModule, statsMgr);
                goto AFTER_DAMAGE;
            }

            if (modComp != null)
            {
                float def = (modComp.placedModuleData != null && modComp.placedModuleData.moduleTemplate != null)
                    ? modComp.placedModuleData.moduleTemplate.armor : 10f;
                result = OptFormula.Calculate(_atk, _pen, def, _currentVelocity.magnitude);
                modComp.TakeDamage(result.Value.damage);

                if (statsMgr != null && modComp.placedModuleData != null)
                {
                    dbgType = "module";
                    result = ApplyModuleDamage(modComp.placedModuleData, statsMgr);
                }
                else
                {
                    dbgType = "module";
                }
                goto AFTER_DAMAGE;
            }

            // 2. Wheel
            if (wheelHealth != null)
            {
                dbgType = "wheel";
                result = OptFormula.Calculate(_atk, _pen, wheelHealth.armor, _currentVelocity.magnitude);
                wheelHealth.TakeDamage(result.Value.damage);
                #if UNITY_EDITOR
                Debug.Log($"[WHEEL] {wheelHealth.gameObject.name} ATK:{_atk} PEN:{_pen} DEF:{wheelHealth.armor} → DMG:{result.Value.damage} PIERCE:{result.Value.pierce} EXIT:{result.Value.exitVel} | HP:{wheelHealth.currentHealth}/{wheelHealth.maxHealth}");
                #endif
                goto AFTER_DAMAGE;
            }

            // 3. Critical Part
            if (critPart != null)
            {
                dbgType = "crit";
                float def = critPart.armor;
                result = OptFormula.Calculate(_atk, _pen, def, _currentVelocity.magnitude);
                #if UNITY_EDITOR
                Debug.Log($"[CRIT] {critPart.partName} ATK:{_atk} PEN:{_pen} DEF:{def} → DMG:{result.Value.damage} PIERCE:{result.Value.pierce} EXIT:{result.Value.exitVel}");
                #endif
                critPart.TakeDamage(result.Value.damage);
                goto AFTER_DAMAGE;
            }

            // 4. SimpleTarget
            if (simpleTarget != null)
            {
                dbgType = "target";
                result = simpleTarget.TakeDamage(_atk, _pen, _currentVelocity.magnitude);
                goto AFTER_DAMAGE;
            }

            // 5. Chassis body (hanya jika statsMgr valid)
            if (statsMgr != null && statsMgr.baseData != null)
            {
                dbgType = "body";
                float def = statsMgr.currentChassisArmor;
                result = OptFormula.Calculate(_atk, _pen, def, _currentVelocity.magnitude);
                #if UNITY_EDITOR
                Debug.Log($"[BODY] Chassis ATK:{_atk} PEN:{_pen} DEF:{def} → DMG:{result.Value.damage} PIERCE:{result.Value.pierce} EXIT:{result.Value.exitVel}");
                #endif
            }

        AFTER_DAMAGE:
            // ——— DEBUG VISUALIZATION: warna beda tiap tipe hit ———
            #if UNITY_EDITOR
            Color dbgColor;
            switch (dbgType)
            {
                case "module": dbgColor = Color.green; break;
                case "wheel":  dbgColor = Color.red; break;
                case "crit":   dbgColor = Color.yellow; break;
                case "body":   dbgColor = Color.blue; break;
                case "target": dbgColor = Color.magenta; break;
                default:       dbgColor = Color.white; break;
            }
            Debug.DrawLine(hit.point + Vector3.left * 0.2f, hit.point + Vector3.right * 0.2f, dbgColor, 3f);
            Debug.DrawLine(hit.point + Vector3.up * 0.2f, hit.point + Vector3.down * 0.2f, dbgColor, 3f);
            Debug.DrawLine(hit.point + Vector3.forward * 0.2f, hit.point + Vector3.back * 0.2f, dbgColor, 3f);
            #endif

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

            // Tambahkan dorongan fisik (Push) — pake Rigidbody cached dari statsMgr
            Rigidbody hitRb = statsMgr != null ? statsMgr.VehicleRigidbody : hit.collider.GetComponentInParent<Rigidbody>();
            if (hitRb != null)
            {
                Vector3 force = _currentVelocity.normalized * (_currentVelocity.magnitude * mass);
                hitRb.AddForceAtPosition(force, hit.point, ForceMode.Impulse);
            }

            // Pierce: kalau tembus, lanjut terbang dengan exit velocity
            if (result.HasValue && result.Value.pierce && result.Value.exitVel > 0f)
            {
                _piercedColliders.Add(hit.collider);
                _currentPosition = hit.point + _currentVelocity.normalized * 0.01f;
                _currentVelocity = _currentVelocity.normalized * result.Value.exitVel;
                _pen = result.Value.remainingPen;
                _atk = result.Value.remainingAtk;
                transform.position = _currentPosition;
                transform.forward = _currentVelocity.normalized;
                return;
            }

            DestroyProjectile();
        }

        private OptResult ApplyModuleDamage(PlacedModule mod, VehicleStatsManager mgr)
        {
            float def = mod.moduleTemplate.armor;
            var r = OptFormula.Calculate(_atk, _pen, def, _currentVelocity.magnitude);

            VehicleModuleComponent modComp = mod.spawnedPrefab != null
                ? mod.spawnedPrefab.GetComponent<VehicleModuleComponent>()
                : null;

            if (modComp != null)
            {
                modComp.TakeDamage(r.damage);
                if (modComp.isDestroyed && mod.moduleTemplate.volatileExplosive)
                    TriggerChainExplosion(mod, mgr);
            }
            else
            {
                mod.currentHealth -= r.damage;
                if (mod.currentHealth <= 0f)
                {
                    bool wasVolatile = mod.moduleTemplate.volatileExplosive;
                    mgr.UninstallModule(mod);
                    if (wasVolatile) TriggerChainExplosion(mod, mgr);
                }
            }

            #if UNITY_EDITOR
            Debug.Log($"[MODULE] {mod.moduleTemplate.moduleName} ATK:{_atk} PEN:{_pen} DEF:{def} → DMG:{r.damage} PIERCE:{r.pierce} EXIT:{r.exitVel} | HP:{(modComp != null ? modComp.currentHealth : mod.currentHealth)}/{mod.moduleTemplate.maxHealth}");
            #endif

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

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            if (_explosiveRadius > 0f)
            {
                Gizmos.color = new Color(1f, 0.4f, 0f, 0.15f);
                Gizmos.DrawSphere(transform.position, _explosiveRadius);
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
                Gizmos.DrawWireSphere(transform.position, _explosiveRadius);
            }
        }
#endif
    }
}
