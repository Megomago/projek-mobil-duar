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
        private float _initialVelocity;
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
        private const int MAX_IMPACTS_PER_FRAME = 10;

        [Header("Debug")]
        [Tooltip("Toggle hit indicator cross visible di build/exe (F2 toggle)")]
        public static bool ShowHitDebug = false;

        // Gravitasi bumi (-9.81), bisa diubah jika butuh balistik spesifik
        private readonly Vector3 _gravity = new Vector3(0f, -9.81f, 0f);

        // PEN di-scale oleh velocity ratio: peluru lambat = penetrasi rendah
        private float EffectivePen => _pen;

        private static readonly RaycastHit[] _raycastHitsBuffer = new RaycastHit[16];

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

            _initialVelocity = muzzleVelocity;
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

            // 4. Lakukan Raycast dari posisi saat ini ke posisi berikutnya menggunakan RaycastNonAlloc agar zero GC allocation
            int hitCount = Physics.RaycastNonAlloc(_currentPosition, directionToNext.normalized, _raycastHitsBuffer, distanceToNext, hitMask);
            
            RaycastHit closestHit = default;
            bool foundHit = false;
            float minDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                var h = _raycastHitsBuffer[i];
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
            // HitboxProxy: satu GetComponent flat, zero hierarchy traversal
            var proxy = hit.collider.GetComponent<HitboxProxy>();

            // Skip hits on the shooter's own vehicle — prevent self-damage
            var statsMgr = proxy != null ? proxy.statsManager : hit.collider.GetComponentInParent<VehicleStatsManager>();
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

            // Bukan kendaraan — cek SimpleTarget dulu sebelum skip
            if (proxy == null && statsMgr == null)
            {
                var st = hit.collider.GetComponentInParent<SimpleTarget>();
                if (st != null)
                {
                    dbgType = "target";
                    result = st.TakeDamage(_atk, EffectivePen, _currentVelocity.magnitude);
                    goto AFTER_DAMAGE;
                }

                if (hitImpactPrefab != null)
                    SpawnImpactAt(hit.point, hit.normal);
                DestroyProjectile();
                return;
            }

            // ── Priority-based damage detection ──
            // Semua referensi dari HitboxProxy (zero GC, zero hierarchy traversal):
            //   1. Module (dictionary > proxy.moduleComponent)
            //   2. Wheel
            //   3. Critical Part
            //   4. SimpleTarget
            //   5. Chassis body (hanya jika statsMgr ada)

            // Cari semua komponen target dari proxy (cache, tanpa GetComponentInParent)
            // Pasti proxy != null atau statsMgr != null di titik ini karena early return di atas
            var modComp = proxy != null ? proxy.moduleComponent : null;
            var wheelHealth = proxy != null ? proxy.wheelHealth : null;
            var critPart = proxy != null ? proxy.criticalPart : null;
            var simpleTarget = proxy != null ? proxy.simpleTarget : null;

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
                float def = 10f;
                if (modComp.placedModuleData != null && modComp.placedModuleData.moduleTemplate != null)
                    def = modComp.placedModuleData.moduleTemplate.armor;
                else if (modComp.moduleTemplate != null)
                    def = modComp.moduleTemplate.armor;
                result = OptFormula.Calculate(_atk, EffectivePen, def, _currentVelocity.magnitude);
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
                result = OptFormula.Calculate(_atk, EffectivePen, wheelHealth.armor, _currentVelocity.magnitude);
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
                result = OptFormula.Calculate(_atk, EffectivePen, def, _currentVelocity.magnitude);
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
                result = simpleTarget.TakeDamage(_atk, EffectivePen, _currentVelocity.magnitude);
                goto AFTER_DAMAGE;
            }

            // 5. Chassis body (hanya jika statsMgr valid)
            if (statsMgr != null && statsMgr.baseData != null)
            {
                dbgType = "body";
                float def = statsMgr.currentChassisArmor;
                result = OptFormula.Calculate(_atk, EffectivePen, def, _currentVelocity.magnitude);
                #if UNITY_EDITOR
                Debug.Log($"[BODY] Chassis ATK:{_atk} PEN:{_pen} DEF:{def} → DMG:{result.Value.damage} PIERCE:{result.Value.pierce} EXIT:{result.Value.exitVel}");
                #endif
            }

        AFTER_DAMAGE:
            // ——— DEBUG HIT INDICATOR (Build Visible) ———
            SpawnDebugHitCross(hit.point, dbgType);

            // Spawn efek ledakan/percikan api dengan pembatas maksimal per frame
            if (hitImpactPrefab != null)
                SpawnImpactAt(hit.point, hit.normal);

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
            var r = OptFormula.Calculate(_atk, EffectivePen, def, _currentVelocity.magnitude);

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
                    #if UNITY_EDITOR
                    Debug.Log($"[CHAIN] {mod.moduleTemplate.moduleName} kena ledakan {dmg} → HP:{mod.currentHealth}");
                    #endif
                    if (mod.currentHealth <= 0f) mgr.UninstallModule(mod);
                }
            }
        }

        private void SpawnDebugHitCross(Vector3 point, string hitType)
        {
            Color color = Color.white;
            switch (hitType)
            {
                case "module": color = Color.green; break;
                case "wheel":  color = Color.red; break;
                case "crit":   color = Color.yellow; break;
                case "body":   color = Color.blue; break;
                case "target": color = Color.magenta; break;
            }

            // Debug.DrawLine — selalu jalan di Editor (tidak di-gate ShowHitDebug)
            Debug.DrawLine(point + Vector3.left * 0.2f, point + Vector3.right * 0.2f, color, 3f);
            Debug.DrawLine(point + Vector3.up * 0.2f, point + Vector3.down * 0.2f, color, 3f);
            Debug.DrawLine(point + Vector3.forward * 0.2f, point + Vector3.back * 0.2f, color, 3f);

            // Build-visible LineRenderer cross — hanya muncul kalau ShowHitDebug ON (F2 toggle)
            if (!ShowHitDebug) return;

            GameObject go = new GameObject("___HitDebug");
            go.transform.position = point;
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = 0.02f;
            lr.endWidth = 0.02f;
            lr.positionCount = 6;
            lr.SetPositions(new Vector3[]
            {
                point + Vector3.left * 0.2f, point + Vector3.right * 0.2f,
                point + Vector3.up * 0.2f, point + Vector3.down * 0.2f,
                point + Vector3.forward * 0.2f, point + Vector3.back * 0.2f
            });
            Object.Destroy(go, 3f);
        }

        private void SpawnImpactAt(Vector3 point, Vector3 normal)
        {
            if (Time.time != _lastImpactFrameTime)
            {
                _lastImpactFrameTime = Time.time;
                _impactsThisFrame = 0;
            }

            if (_impactsThisFrame < MAX_IMPACTS_PER_FRAME)
            {
                ObjectPool.Instance.Spawn(hitImpactPrefab, point, Quaternion.LookRotation(normal));
                _impactsThisFrame++;
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
