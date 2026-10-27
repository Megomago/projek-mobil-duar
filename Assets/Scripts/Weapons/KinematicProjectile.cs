using System.Collections.Generic;
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
        private float _explosionVFXDespawnTime;
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
        private static readonly List<PlacedModule> _chainVictims = new List<PlacedModule>();

        // Material cross debug hit — ZTest Always biar tembus bodi. Cross crit (orange)
        // sering spawn di DALAM mesin yang ketutup panel bodi; material depth-tested
        // bikin dia invisible. Cache static: cuma bikin Material 1x, bukan per hit.
        private static Material _debugCrossMaterial;

        private static Material DebugCrossMaterial
        {
            get
            {
                if (_debugCrossMaterial == null)
                {
                    Shader shader = Shader.Find("Custom/XRayLine");
                    if (shader == null) shader = Shader.Find("Custom/GridOverlay");
                    if (shader == null) shader = Shader.Find("Sprites/Default");
                    _debugCrossMaterial = new Material(shader);
                    _debugCrossMaterial.hideFlags = HideFlags.DontSave;
                }
                return _debugCrossMaterial;
            }
        }

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
            _explosionVFXDespawnTime = 0f;
            _explosionSFX = null;
        }

        public void SetExplosive(bool isExplosive, float radius, float damage, float force, GameObject vfx, float vfxDespawnTime, AudioClip sfx)
        {
            _isExplosive = isExplosive;
            _explosiveRadius = radius;
            _explosiveDamage = damage;
            _explosiveForce = force;
            _explosionVFXPrefab = vfx;
            _explosionVFXDespawnTime = vfxDespawnTime;
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

                // Tembus collider kendaraan PENEMBAH sendiri (modul/bodi/roda sendiri).
                // Kalau cuma di-skip di HandleHit, peluru diam di tempat ("mematung")
                // karena posisinya tidak pernah maju — misal peluru kena modul yang
                // terpasang di kendaraan yang sama, persis di depan laras.
                if (_ownerStatsManager != null)
                {
                    var ownerProxy = h.collider.GetComponent<HitboxProxy>();
                    VehicleStatsManager hitStats = ownerProxy != null ? ownerProxy.statsManager
                        : h.collider.GetComponentInParent<VehicleStatsManager>();
                    if (hitStats == _ownerStatsManager)
                    {
                        _piercedColliders.Add(h.collider);
                        continue;
                    }
                }

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

            // Skip hits on the shooter's own vehicle — prevent self-damage.
            // Tandai pierced juga: kalau tidak, raycast frame berikutnya memilih
            // collider yang sama lagi dan peluru nyangkut mematung di udara.
            var statsMgr = proxy != null ? proxy.statsManager : hit.collider.GetComponentInParent<VehicleStatsManager>();
            if (statsMgr != null && _ownerStatsManager != null && statsMgr == _ownerStatsManager)
            {
                _piercedColliders.Add(hit.collider);
                return;
            }

            // Explosive ammo: bypass kinetic damage pipeline, langsung detonate
            if (_isExplosive)
            {
                ExplosionManager.Detonate(hit.point, _explosiveRadius, _explosiveDamage, _explosiveForce, statsMgr, hitMask, _ownerStatsManager, _pen);

                // Feedback HUD untuk ammo explosive
                if (HitFeedbackHUD.IsPlayerShooter(_ownerStatsManager))
                {
                    string targetName = statsMgr != null ? "Target" : "HE Impact";
                    HitFeedbackHUD.ReportHit(targetName, _explosiveDamage, false);
                }

                // Visualisasi Gizmo area ledakan (wire sphere 3D tahan 3 detik)
                SpawnDebugExplosionGizmo(hit.point, _explosiveRadius);

                // Fallback VFX: gunakan explosionVFXPrefab jika ada, atau fallback ke hitImpactPrefab
                GameObject vfxPrefab = _explosionVFXPrefab != null ? _explosionVFXPrefab : hitImpactPrefab;
                if (vfxPrefab != null && ObjectPool.Instance != null)
                {
                    Quaternion rot = hit.normal != Vector3.zero ? Quaternion.LookRotation(hit.normal) : Quaternion.identity;
                    GameObject vfx = ObjectPool.Instance.Spawn(vfxPrefab, hit.point, rot);
                    if (vfx != null)
                    {
                        if (_explosionVFXPrefab != null)
                        {
                            float vfxScale = _explosiveRadius * 0.35f;
                            vfx.transform.localScale = Vector3.one * Mathf.Max(vfxScale, 0.5f);
                        }
                        // VFX ledakan: durasi diatur dari WeaponData. 0 = biarkan prefab yang atur.
                        // Fallback impact tetap dibersihkan 4 detik agar tidak bocor di scene.
                        if (_explosionVFXPrefab != null)
                        {
                            if (_explosionVFXDespawnTime > 0f)
                                ObjectPool.Instance.Despawn(vfx, _explosionVFXDespawnTime);
                        }
                        else
                        {
                            ObjectPool.Instance.Despawn(vfx, 4f);
                        }
                    }
                }

                if (_explosionSFX != null)
                    AudioPool.PlayClipAtPoint(_explosionSFX, hit.point, 1f, Random.Range(0.92f, 1.08f), 1f, 150f);

                #if UNITY_EDITOR
                Debug.Log($"[EXPLOSIVE] {_explosiveDamage} dmg radius {_explosiveRadius}m at {hit.point}");
                #endif

                DestroyProjectile();
                return;
            }

            OptResult? result = null;
            string dbgType = "unknown";

            // Feedback HUD (War Thunder style) — diisi oleh branch yang kena
            string fbName = null;
            bool fbDestroyed = false;
            bool fbKilled = false;

            // Bukan kendaraan — cek SimpleTarget dulu sebelum skip
            if (proxy == null && statsMgr == null)
            {
                var st = hit.collider.GetComponentInParent<SimpleTarget>();
                if (st != null)
                {
                    dbgType = "target";
                    result = st.TakeDamage(_atk, EffectivePen, _currentVelocity.magnitude);
                    fbName = "Target";
                    fbKilled = st.currentHealth <= 0f;
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

            // Debug: kirim log nama target + hierarchy depth (hanya kalau ShowHitDebug aktif)
            #if UNITY_EDITOR
            if (ShowHitDebug)
            {
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
            }
            #endif

            // 1. Module — dictionary lookup dulu, fallback GetComponentInParent
            if (statsMgr != null && statsMgr.moduleColliderMap.TryGetValue(hit.collider, out var hitModule))
            {
                dbgType = "module";
                ModuleTemplate fbTemplate = hitModule.moduleTemplate;
                result = ApplyModuleDamage(hitModule, statsMgr);
                fbName = fbTemplate != null ? fbTemplate.moduleName : "Module";
                fbDestroyed = hitModule.currentHealth <= 0f;
                goto AFTER_DAMAGE;
            }

            // Modul terpasang di grid → lewat ApplyModuleDamage saja (hitung + apply damage SEKALI)
            if (modComp != null && statsMgr != null && modComp.placedModuleData != null)
            {
                dbgType = "module";
                PlacedModule fbPlaced = modComp.placedModuleData;
                ModuleTemplate fbTemplate = fbPlaced.moduleTemplate;
                result = ApplyModuleDamage(fbPlaced, statsMgr);
                fbName = fbTemplate != null ? fbTemplate.moduleName : "Module";
                fbDestroyed = fbPlaced.currentHealth <= 0f;
                goto AFTER_DAMAGE;
            }

            // Modul standalone (tidak terpasang di kendaraan) → damage langsung sekali
            if (modComp != null)
            {
                ModuleTemplate fbTemplate = null;
                if (modComp.placedModuleData != null && modComp.placedModuleData.moduleTemplate != null)
                    fbTemplate = modComp.placedModuleData.moduleTemplate;
                else if (modComp.moduleTemplate != null)
                    fbTemplate = modComp.moduleTemplate;

                float def = fbTemplate != null ? fbTemplate.armor : 10f;
                result = OptFormula.Calculate(_atk, EffectivePen, def, _currentVelocity.magnitude);
                modComp.TakeDamage(result.Value.damage);
                fbName = fbTemplate != null ? fbTemplate.moduleName : "Module";
                fbDestroyed = modComp.isDestroyed;
                dbgType = "module";
                goto AFTER_DAMAGE;
            }

            // 2. Wheel
            if (wheelHealth != null)
            {
                dbgType = "wheel";
                result = OptFormula.Calculate(_atk, EffectivePen, wheelHealth.armor, _currentVelocity.magnitude);
                wheelHealth.TakeDamage(result.Value.damage);
                fbName = "Wheel";
                fbDestroyed = wheelHealth.isDestroyed;
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
                fbName = critPart.partName;
                fbDestroyed = critPart.currentHealth <= 0f;
                goto AFTER_DAMAGE;
            }

            // 4. SimpleTarget
            if (simpleTarget != null)
            {
                dbgType = "target";
                result = simpleTarget.TakeDamage(_atk, EffectivePen, _currentVelocity.magnitude);
                fbName = "Target";
                fbKilled = simpleTarget.currentHealth <= 0f;
                goto AFTER_DAMAGE;
            }

            // 5. Chassis body (hanya jika statsMgr valid)
            if (statsMgr != null && statsMgr.baseData != null)
            {
                dbgType = "body";
                float def = statsMgr.currentChassisArmor;
                result = OptFormula.Calculate(_atk, EffectivePen, def, _currentVelocity.magnitude);
                fbName = "Hull";
                #if UNITY_EDITOR
                Debug.Log($"[BODY] Chassis ATK:{_atk} PEN:{_pen} DEF:{def} → DMG:{result.Value.damage} PIERCE:{result.Value.pierce} EXIT:{result.Value.exitVel}");
                #endif
            }

        AFTER_DAMAGE:
            // ——— HIT FEEDBACK HUD (War Thunder style) — hanya saat penembak = kendaraan player ———
            if (fbName != null && HitFeedbackHUD.IsPlayerShooter(_ownerStatsManager))
            {
                if (fbKilled)
                    HitFeedbackHUD.ReportKill("Target");
                else
                    HitFeedbackHUD.ReportHit(fbName, result.HasValue ? result.Value.damage : 0f, fbDestroyed);
            }

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

            // Snapshot korban dulu — UninstallModule di tengah iterasi memodifikasi
            // installedModules dan bisa melempar InvalidOperationException.
            _chainVictims.Clear();
            foreach (var mod in mgr.installedModules)
            {
                if (mod == sourceMod || mod.moduleTemplate == null || mod.zoneName != srcZone) continue;
                int dx = Mathf.Abs(mod.gridPosition.x - srcPos.x);
                int dy = Mathf.Abs(mod.gridPosition.y - srcPos.y);
                if (dx <= radius && dy <= radius)
                    _chainVictims.Add(mod);
            }

            foreach (var victim in _chainVictims)
            {
                if (victim == null || victim.moduleTemplate == null) continue;

                // Damage lewat komponen agar PlacedModule & VehicleModuleComponent tetap sinkron
                VehicleModuleComponent comp = victim.spawnedPrefab != null
                    ? victim.spawnedPrefab.GetComponent<VehicleModuleComponent>()
                    : null;

                if (comp != null)
                {
                    comp.TakeDamage(dmg);
                }
                else
                {
                    victim.currentHealth -= dmg;
                    #if UNITY_EDITOR
                    Debug.Log($"[CHAIN] {victim.moduleTemplate.moduleName} kena ledakan {dmg} → HP:{victim.currentHealth}");
                    #endif
                    if (victim.currentHealth <= 0f) mgr.UninstallModule(victim);
                }
            }
        }

        private void SpawnDebugHitCross(Vector3 point, string hitType)
        {
            if (!ShowHitDebug) return;

            Color color = Color.white;
            bool isCritical = false;
            switch (hitType)
            {
                case "module": color = Color.green; break;
                case "wheel":  color = Color.red; break;
                case "crit":   color = new Color(1f, 0.5f, 0f); isCritical = true; break; // orange — sengaja beda jauh dari hull (biru)
                case "body":   color = Color.blue; break;
                case "target": color = Color.magenta; break;
            }

            // Critical part: cross lebih besar + tongkat vertikal, biar tidak "menyatu"
            // dengan cross hull saat posisinya bertumpuk (peluru nembus bodi → kena part)
            float arm = isCritical ? 0.35f : 0.2f;

            Debug.DrawLine(point + Vector3.left * arm, point + Vector3.right * arm, color, 3f);
            Debug.DrawLine(point + Vector3.up * arm, point + Vector3.down * arm, color, 3f);
            Debug.DrawLine(point + Vector3.forward * arm, point + Vector3.back * arm, color, 3f);

            GameObject go = new GameObject("___HitDebug");
            go.transform.position = point;
            var lr = go.AddComponent<LineRenderer>();
            // sharedMaterial XRayLine (ZTest Always): cross tetap kelihatan walau
            // spawn di dalam bodi (kasus crit part mesin) — anti ketutup panel.
            lr.sharedMaterial = DebugCrossMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = isCritical ? 0.035f : 0.02f;
            lr.endWidth = isCritical ? 0.035f : 0.02f;

            if (isCritical)
            {
                lr.positionCount = 8;
                lr.SetPositions(new Vector3[]
                {
                    point + Vector3.left * arm, point + Vector3.right * arm,
                    point + Vector3.up * arm, point + Vector3.down * arm,
                    point + Vector3.forward * arm, point + Vector3.back * arm,
                    point, point + Vector3.up * (arm + 0.3f)
                });
            }
            else
            {
                lr.positionCount = 6;
                lr.SetPositions(new Vector3[]
                {
                    point + Vector3.left * arm, point + Vector3.right * arm,
                    point + Vector3.up * arm, point + Vector3.down * arm,
                    point + Vector3.forward * arm, point + Vector3.back * arm
                });
            }
            Object.Destroy(go, 3f);
        }

        private void SpawnDebugExplosionGizmo(Vector3 point, float radius)
        {
            #if UNITY_EDITOR
            DrawDebugExplosionSphere(point, radius, new Color(1f, 0.45f, 0f, 0.9f), 3f);
            #endif

            if (!ShowHitDebug) return;

            GameObject go = new GameObject("___ExplosionDebug");
            go.transform.position = point;
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = DebugCrossMaterial;
            lr.startColor = new Color(1f, 0.45f, 0f, 0.85f);
            lr.endColor = new Color(1f, 0.45f, 0f, 0.85f);
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.useWorldSpace = true;

            int segments = 32;
            lr.positionCount = segments + 1;
            float step = 360f / segments;
            for (int i = 0; i <= segments; i++)
            {
                float rad = Mathf.Deg2Rad * (i * step);
                Vector3 pos = point + new Vector3(Mathf.Cos(rad) * radius, 0.05f, Mathf.Sin(rad) * radius);
                lr.SetPosition(i, pos);
            }
            Object.Destroy(go, 3f);
        }

        public static void DrawDebugExplosionSphere(Vector3 center, float radius, Color color, float duration = 3f, int segments = 24)
        {
            float angleStep = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float a1 = Mathf.Deg2Rad * (i * angleStep);
                float a2 = Mathf.Deg2Rad * ((i + 1) * angleStep);

                float c1 = Mathf.Cos(a1) * radius;
                float s1 = Mathf.Sin(a1) * radius;
                float c2 = Mathf.Cos(a2) * radius;
                float s2 = Mathf.Sin(a2) * radius;

                // Lingkaran bidang XZ (horizontal ground)
                Debug.DrawLine(center + new Vector3(c1, 0f, s1), center + new Vector3(c2, 0f, s2), color, duration);
                // Lingkaran bidang XY (vertikal melintang)
                Debug.DrawLine(center + new Vector3(c1, s1, 0f), center + new Vector3(c2, s2, 0f), color, duration);
                // Lingkaran bidang YZ (vertikal membujur)
                Debug.DrawLine(center + new Vector3(0f, s1, c1), center + new Vector3(0f, s2, c2), color, duration);
            }

            // Cross di pusat ledakan
            Debug.DrawLine(center + Vector3.left * 0.5f, center + Vector3.right * 0.5f, Color.red, duration);
            Debug.DrawLine(center + Vector3.up * 0.5f, center + Vector3.down * 0.5f, Color.red, duration);
            Debug.DrawLine(center + Vector3.forward * 0.5f, center + Vector3.back * 0.5f, Color.red, duration);
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
                GameObject impact = ObjectPool.Instance.Spawn(hitImpactPrefab, point, Quaternion.LookRotation(normal));
                if (impact != null)
                {
                    ObjectPool.Instance.Despawn(impact, 2f);
                }
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
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;
            if (_isExplosive && _explosiveRadius > 0f)
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
