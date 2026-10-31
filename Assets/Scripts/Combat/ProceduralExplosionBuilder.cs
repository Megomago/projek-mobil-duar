using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Combat
{
    /// <summary>
    /// Generator Ledakan HE (High Explosive) kelas sinematik — 100% prosedural.
    /// Api dan asap pakai shader noise prosedural (Custom/ExplosionFire &amp; Custom/ExplosionSmoke),
    /// bukan tekstur Default-Particle Unity. Flash/shockwave masih memakai particle standar.
    ///
    /// FASE LEDAKAN (urut waktu, realistis & brutal):
    ///   0_Shockwave  : gelombang tekanan mengembang SUPER cepat, tipis & redup
    ///   1_Flash      : kilatan putih HDR instan (memicu Bloom URP & Auto-Exposure drop)
    ///   2_FireballCore : inti terpanas, putih-kuning buta
    ///   3_FireballBody : bola api utama: kuning -> oranye -> merah bara -> JELAGA ITEM
    ///   4_FireballOuter: selubung luar lebih dingin (merah tua), fireball mengempis
    ///   5_SmokeColumn: asap jelaga MENYUSUL dari balik api lalu bertahan paling lama
    ///   6_Shrapnel   : serpihan logam berpijar dengan trail (nembak kayak peluru)
    ///   7_Sparks     : percikan api kecil berkelip (flicker)
    ///   8_FlashLight : point light HARD SHADOW berkedip, menerangi bodi mobil di sekitar
    /// </summary>
    public class ProceduralExplosionBuilder : MonoBehaviour
    {
        [Header("=== SETTING UKURAN LEDAKAN ===")]
        public float explosionScale = 1.0f;

        [ContextMenu("GENERATE LEDAKAN TEMPUR SEKARANG")]
        public void GenerateExplosion()
        {
            #if UNITY_EDITOR
            BuildCinematicExplosion();
            #endif
        }

        #if UNITY_EDITOR
        [MenuItem("Tools/NNTI/Explosion/Upgrade Material ke Procedural")]
        private static void UpgradeMaterialsToProcedural()
        {
            Material fire = LoadOrCreateProceduralMaterial(FireMatPath, FireShaderName);
            Material smoke = LoadOrCreateProceduralMaterial(SmokeMatPath, SmokeShaderName);

            if (fire == null || smoke == null)
            {
                EditorUtility.DisplayDialog("Explosion Material", "Gagal upgrade. Cek console: shader Custom/ExplosionFire / Custom/ExplosionSmoke nggak ketemu.", "OK");
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[ExplosionBuilder] Material api & asap sudah pakai shader prosedural (Custom/ExplosionFire & Custom/ExplosionSmoke).");
        }
        #endif

        #if UNITY_EDITOR
        private const string MatFolder = "Assets/Scripts/Combat/GeneratedExplosion";
        private const string FlashMatPath = MatFolder + "/Explosion_Flash_Add.mat";
        private const string FireMatPath = MatFolder + "/Explosion_Fire_Add.mat";
        private const string SmokeMatPath = MatFolder + "/Explosion_Smoke_Alpha.mat";
        private const string FireShaderName = "Custom/ExplosionFire";
        private const string SmokeShaderName = "Custom/ExplosionSmoke";

        private void BuildCinematicExplosion()
        {
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            // Flash sengaja over-bright (HDR) supaya kena Bloom; fire sedikit di atas 1
            // biar bara tetap "menyala" di tonemapping; smoke alpha-blend warna netral.
            Material flashMat = LoadOrCreateMaterial(FlashMatPath, true, new Color(3.5f, 3.0f, 2.4f, 1f)); // Lebih nge-jreng
            Material fireMat = LoadOrCreateProceduralMaterial(FireMatPath, FireShaderName);
            Material smokeMat = LoadOrCreateProceduralMaterial(SmokeMatPath, SmokeShaderName);

            if (flashMat == null || fireMat == null || smokeMat == null)
            {
                Debug.LogError("[ExplosionBuilder] Gagal menyiapkan material ledakan URP!");
                return;
            }

            float s = Mathf.Max(0.05f, explosionScale);

            CreateShockwave(smokeMat, s);
            CreateFlash(flashMat, s);
            CreateFireballCore(fireMat, s);
            CreateFireballBody(fireMat, s);
            CreateFireballOuter(fireMat, s);
            CreateSmokeColumn(smokeMat, s);
            CreateShrapnel(fireMat, s);
            CreateSparks(fireMat, s);
            CreateFlashLight(s);

            Debug.Log($"[ExplosionBuilder] Ledakan HE sinematik skala {explosionScale}x selesai dirakit — API dominan 0.5 detik pertama, ASAP bertahan setelahnya.");
        }

        // =====================================================================
        // 0. SHOCKWAVE — gelombang tekanan, bukan api (Lebih cepet dari api)
        // =====================================================================
        private void CreateShockwave(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("0_Shockwave", mat, out ParticleSystemRenderer r);
            r.sortingFudge = 25f;

            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.18f;
            main.startSpeed = 0f;
            main.startSize = 2.5f * s;
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.8f);
            sizeCurve.AddKey(0.5f, 3.5f);
            sizeCurve.AddKey(1f, 7.0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.9f, 0.92f, 0.95f), 0f),
                    new GradientColorKey(new Color(0.4f, 0.4f, 0.42f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.45f, 0f),
                    new GradientAlphaKey(0.15f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // =====================================================================
        // 1. FLASH — kilatan putih instan (HDR -> bloom & exposure drop)
        // =====================================================================
        private void CreateFlash(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("1_Flash", mat, out ParticleSystemRenderer r);
            r.sortingFudge = -30f;

            var main = ps.main;
            main.duration = 0.15f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(8.0f * s, 11.0f * s);
            main.startColor = new Color(1f, 0.97f, 0.9f, 1f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 1),
                new ParticleSystem.Burst(0.04f, 1) 
            });

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.8f);
            sizeCurve.AddKey(1f, 1.5f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.5f, 1.4f, 1.2f), 0f), // HDR
                    new GradientColorKey(new Color(1f, 0.8f, 0.5f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // =====================================================================
        // 2. FIREBALL CORE — inti terpanas (putih-kuning buta)
        // =====================================================================
        private void CreateFireballCore(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("2_FireballCore", mat, out ParticleSystemRenderer r);
            r.sortingFudge = -20f;

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f * s, 8f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(1.8f * s, 3f * s);
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 15) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f * s;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.3f, 1.8f);
            sizeCurve.AddKey(1f, 1.5f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.5f, 1.4f, 1.2f), 0f),
                    new GradientColorKey(new Color(1.2f, 0.9f, 0.5f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.1f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(1.5f * s, 3.0f * s);
            noise.frequency = 1.5f;
            noise.scrollSpeed = 3f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.High;
        }

        // =====================================================================
        // 3. FIREBALL BODY — bola api utama yang bergulung (API PRIORITAS)
        // =====================================================================
        private void CreateFireballBody(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("3_FireballBody", mat, out ParticleSystemRenderer r);
            r.sortingFudge = -10f;

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f * s, 14f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(2.8f * s, 4.5f * s);
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 35),
                new ParticleSystem.Burst(0.06f, 20) 
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f * s;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            AnimationCurve rise = new AnimationCurve();
            rise.AddKey(0f, 0f);
            rise.AddKey(0.25f, 3.5f * s);
            rise.AddKey(1f, 1.5f * s);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, rise);

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.space = ParticleSystemSimulationSpace.World;
            limit.limit = new ParticleSystem.MinMaxCurve(8f * s);
            limit.dampen = 0.4f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.4f);
            sizeCurve.AddKey(0.3f, 1.5f);
            sizeCurve.AddKey(0.7f, 1.8f);
            sizeCurve.AddKey(1f, 1.6f); 
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Transisi warna api: KUNING TERANG -> ORANYE -> MERAH BARA -> JELAGA ITEM
            // Alpha ditahan solid sampe bener-bener jadi item, baru fade.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.2f, 0.95f, 0.5f), 0f),
                    new GradientColorKey(new Color(1.2f, 0.45f, 0.05f), 0.35f),
                    new GradientColorKey(new Color(0.7f, 0.1f, 0.02f), 0.65f),
                    new GradientColorKey(new Color(0.15f, 0.08f, 0.05f), 0.85f), // Soot
                    new GradientColorKey(new Color(0.05f, 0.05f, 0.05f), 1f) // Black
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.75f), // Tahan solid sampe akhir
                    new GradientAlphaKey(0.8f, 0.9f),
                    new GradientAlphaKey(0f, 1f) // Baru ilang pas udah jadi item
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(2.5f * s, 5.0f * s);
            noise.frequency = 0.65f;
            noise.scrollSpeed = 1.5f;
            noise.damping = true;
            noise.octaveCount = 3;
            noise.quality = ParticleSystemNoiseQuality.High;
        }

        // =====================================================================
        // 4. FIREBALL OUTER — selubung luar yang lebih dingin
        // =====================================================================
        private void CreateFireballOuter(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("4_FireballOuter", mat, out ParticleSystemRenderer r);
            r.sortingFudge = 0f;

            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * s, 6f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(3.5f * s, 5.5f * s);
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0.04f, 20),
                new ParticleSystem.Burst(0.15f, 12)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.9f * s;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            AnimationCurve rise = new AnimationCurve();
            rise.AddKey(0f, 0f);
            rise.AddKey(0.35f, 2.2f * s);
            rise.AddKey(1f, 1.2f * s);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, rise);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.6f);
            sizeCurve.AddKey(0.4f, 1.3f);
            sizeCurve.AddKey(1f, 1.6f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.4f, 0.05f), 0f),
                    new GradientColorKey(new Color(0.5f, 0.08f, 0.02f), 0.45f),
                    new GradientColorKey(new Color(0.12f, 0.05f, 0.03f), 0.8f),
                    new GradientColorKey(new Color(0.05f, 0.05f, 0.05f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(3f * s, 5.5f * s);
            noise.frequency = 0.5f;
            noise.scrollSpeed = 1.0f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.High;
        }

        // =====================================================================
        // 5. SMOKE — asap jelaga yang menyusul & bertahan paling lama
        // =====================================================================
        private void CreateSmokeColumn(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("5_SmokeColumn", mat, out ParticleSystemRenderer r);
            r.sortingFudge = 20f;

            var main = ps.main;
            main.duration = 1.2f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * s, 2.5f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(4.5f * s, 6.5f * s);
            main.startColor = Color.white;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI); 
            main.gravityModifier = -0.15f; 

            var emission = ps.emission;
            emission.rateOverTime = 12f * s; 
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0.12f, 22), 
                new ParticleSystem.Burst(0.35f, 18)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.2f * s;
            shape.radiusThickness = 0.4f; 
            shape.randomDirectionAmount = 0.65f; 

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            AnimationCurve drift = new AnimationCurve();
            drift.AddKey(0f, 0f);
            drift.AddKey(0.3f, 1.8f * s);
            drift.AddKey(1f, 0.6f * s); 
            velocity.y = new ParticleSystem.MinMaxCurve(1f, drift);

            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.space = ParticleSystemSimulationSpace.World;
            limit.limit = new ParticleSystem.MinMaxCurve(2.5f * s);
            limit.dampen = 0.5f;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.6f);
            sizeCurve.AddKey(0.25f, 1.2f);
            sizeCurve.AddKey(1f, 1.8f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Asap kudu SOLID nge-blok background, bukan transparan letoy
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.1f, 0.09f, 0.08f), 0f),
                    new GradientColorKey(new Color(0.18f, 0.17f, 0.16f), 0.4f),
                    new GradientColorKey(new Color(0.28f, 0.28f, 0.29f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.15f), // Langsung solid nge-blok
                    new GradientAlphaKey(0.75f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(1.5f * s, 3.0f * s);
            noise.frequency = 0.35f; 
            noise.scrollSpeed = 0.6f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.High;
        }

        // =====================================================================
        // 6. SHRAPNEL — serpihan logam berpijar (nembak brutal)
        // =====================================================================
        private void CreateShrapnel(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("6_Shrapnel", mat, out ParticleSystemRenderer r);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.15f;
            r.lengthScale = 2.0f;
            r.sortingFudge = -15f;
            r.trailMaterial = mat;

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(25f * s, 50f * s); // Nembak kayak peluru
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f * s, 0.2f * s);
            main.startColor = Color.white;
            main.gravityModifier = 4.5f; // Besi jatuh melengkung tajam & nancep

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.02f, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.25f * s;

            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 0.4f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            trails.minVertexDistance = 0.05f * s;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            AnimationCurve trailWidth = new AnimationCurve();
            trailWidth.AddKey(0f, 1f);
            trailWidth.AddKey(1f, 0.1f);
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, trailWidth);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.2f, 0.8f, 0.3f), 0f),
                    new GradientColorKey(new Color(0.8f, 0.2f, 0.05f), 0.4f),
                    new GradientColorKey(new Color(0.1f, 0.08f, 0.08f), 0.85f),
                    new GradientColorKey(new Color(0.05f, 0.05f, 0.05f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // =====================================================================
        // 7. SPARKS — percikan kecil yang berkelip
        // =====================================================================
        private void CreateSparks(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("7_Sparks", mat, out ParticleSystemRenderer r);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.08f;
            r.lengthScale = 2.5f;
            r.sortingFudge = -25f;

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(12f * s, 28f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f * s, 0.09f * s);
            main.startColor = Color.white;
            main.gravityModifier = 1.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f * s;
            shape.randomDirectionAmount = 0.5f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.6f * s, 1.5f * s);
            noise.frequency = 2.5f;
            noise.scrollSpeed = 4f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // Alpha "zigzag" = efek berkelip khas percikan logam panas
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1.2f, 1.0f, 0.7f), 0f),
                    new GradientColorKey(new Color(1.2f, 0.6f, 0.15f), 0.35f),
                    new GradientColorKey(new Color(0.7f, 0.15f, 0.02f), 0.75f),
                    new GradientColorKey(new Color(0.2f, 0.05f, 0.01f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.4f, 0.15f),
                    new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(0.3f, 0.45f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // =====================================================================
        // 8. FLASH LIGHT — menerangi lingkungan & bodi mobil sekitar (HARD SHADOW)
        // =====================================================================
        private void CreateFlashLight(float s)
        {
            GameObject go = new GameObject("8_FlashLight");
            go.transform.SetParent(transform, false);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f); // Lebih ke putih-oranye panas
            light.range = 18f * s; // Range lebih gede buat nyapu environment
            light.intensity = 0f;
            light.shadows = LightShadows.Hard; // WAJIB HARD SHADOW BUAT CINEMATIC IMPACT!

            ExplosionFlashLight pulse = go.AddComponent<ExplosionFlashLight>();
            pulse.peakIntensity = 65f * s; // BANTER BANGET, BIAR BLOOM & EXPOSURE NGE-JERIT
            pulse.duration = 0.25f; // Flash asli tuh cepet ilang
            pulse.decayPower = 4.5f; // Drop-off ekstrem
            pulse.flickerAmount = 0.2f;
        }

        // =====================================================================
        // UTILITAS
        // =====================================================================
        private ParticleSystem CreateSystem(string name, Material mat, out ParticleSystemRenderer renderer)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = mat;
            return ps;
        }

        private static Gradient MakeGradient(GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        private static Material LoadOrCreateProceduralMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[ExplosionBuilder] Shader '{shaderName}' nggak ketemu. Pastikan file shader ada di project dan sudah diimport.");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;

            if (isNew)
                mat = new Material(shader);
            else if (mat.shader != shader)
                mat.shader = shader;

            mat.name = System.IO.Path.GetFileNameWithoutExtension(path);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            bool isFire = shaderName == FireShaderName;

            if (isFire)
            {
                mat.SetColor("_Tint", Color.white);
                mat.SetColor("_CoreColor", new Color(2.4f, 2.0f, 1.3f, 1f));
                mat.SetFloat("_CoreStrength", 0.75f);
                mat.SetFloat("_CorePower", 2.5f);
                mat.SetFloat("_Intensity", 1.35f);
                mat.SetFloat("_Opacity", 1f);
                mat.SetFloat("_NoiseScale", 2.4f);
                mat.SetFloat("_NoiseSpeed", 1.4f);
                mat.SetFloat("_Distortion", 1.2f);
                mat.SetFloat("_NoiseGain", 0.55f);
                mat.SetFloat("_Erosion", 0.35f);
                mat.SetFloat("_ErosionScale", 5.5f);
                mat.SetFloat("_RadialPower", 1.2f);
            }
            else
            {
                mat.SetColor("_Tint", Color.white);
                mat.SetFloat("_Intensity", 1f);
                mat.SetFloat("_Opacity", 0.95f);
                mat.SetFloat("_NoiseScale", 1.8f);
                mat.SetFloat("_NoiseSpeed", 0.45f);
                mat.SetFloat("_Distortion", 1.6f);
                mat.SetFloat("_NoiseGain", 0.65f);
                mat.SetFloat("_Erosion", 0.55f);
                mat.SetFloat("_ErosionScale", 4.5f);
                mat.SetFloat("_RadialPower", 1f);
                mat.SetFloat("_ShadeStrength", 0.35f);
            }

            if (isNew)
            {
                EnsureFolder(MatFolder);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        private static Material LoadOrCreateMaterial(string path, bool additive, Color baseColor)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("[ExplosionBuilder] Tidak menemukan shader particle URP!");
                return null;
            }

            Texture2D tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (tex == null)
            {
                Material builtin = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
                if (builtin != null) tex = builtin.mainTexture as Texture2D;
            }

            Material mat = new Material(shader);
            mat.name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

            if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0f);
            if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 0f);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 2f : 0f);
            if (mat.HasProperty("_SrcBlend"))
                mat.SetFloat("_SrcBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.SrcAlpha));
            if (mat.HasProperty("_DstBlend"))
                mat.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            EnsureFolder(MatFolder);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int idx = folder.LastIndexOf('/');
            if (idx <= 0) return;
            string parent = folder.Substring(0, idx);
            string leaf = folder.Substring(idx + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
        #endif
    }
}
