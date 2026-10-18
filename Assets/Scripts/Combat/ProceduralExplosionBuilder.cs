using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Combat
{
    /// <summary>
    /// Generator Ledakan HE (High Explosive) kelas sinematik — 100% prosedural memakai
    /// tekstur Default-Particle bawaan Unity (tanpa alpha mask / material eksternal).
    ///
    /// FASE LEDAKAN (urut waktu, realistis):
    ///   0_Shockwave  : gelombang tekanan mengembang cepat, tipis & redup
    ///   1_Flash      : kilatan putih HDR instan (memicu Bloom URP) — 1-2 frame
    ///   2_FireballCore : inti terpanas, putih-kuning
    ///   3_FireballBody : bola api utama: kuning -> oranye -> merah bara -> jelaga
    ///   4_FireballOuter: selubung luar lebih dingin (merah tua), fireball mengempis
    ///   5_SmokeColumn: asap jelaga MENYUSUL dari balik api lalu bertahan paling lama
    ///   6_Shrapnel   : serpihan logam berpijar dengan trail
    ///   7_Sparks     : percikan api kecil berkelip (flicker)
    ///   8_FlashLight : point light berkedip, menerangi bodi mobil di sekitar
    ///
    /// RASIO API vs ASAP (fisikanya ledakan HE):
    ///   - 0.0-0.5s : API dominan (fase pembakaran / pressure front)
    ///   - 0.5-1.0s : api meredup & mendingin, asap mulai menutup
    ///   - >1.0s    : ASAP dominan (residu jelaga bertahan ~3-4 detik)
    ///
    /// URUTAN WARNA API (suhu turun = warna bergeser):
    ///   putih-kuning (>1500C) -> kuning -> oranye -> merah bara -> jelaga gelap (padam).
    ///   Api SELALU menghitam dulu sebelum hilang, jangan fade dari oranye terang.
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
        private const string MatFolder = "Assets/Scripts/Combat/GeneratedExplosion";
        private const string FlashMatPath = MatFolder + "/Explosion_Flash_Add.mat";
        private const string FireMatPath = MatFolder + "/Explosion_Fire_Add.mat";
        private const string SmokeMatPath = MatFolder + "/Explosion_Smoke_Alpha.mat";

        private void BuildCinematicExplosion()
        {
            while (transform.childCount > 0)
            {
                DestroyImmediate(transform.GetChild(0).gameObject);
            }

            // Flash sengaja over-bright (HDR) supaya kena Bloom; fire sedikit di atas 1
            // biar bara tetap "menyala" di tonemapping; smoke alpha-blend warna netral.
            Material flashMat = LoadOrCreateMaterial(FlashMatPath, true, new Color(2.4f, 2.15f, 1.7f, 1f));
            Material fireMat = LoadOrCreateMaterial(FireMatPath, true, new Color(1.8f, 1.55f, 1.25f, 1f));
            Material smokeMat = LoadOrCreateMaterial(SmokeMatPath, false, Color.white);

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
        // 0. SHOCKWAVE — gelombang tekanan, bukan api
        // =====================================================================
        private void CreateShockwave(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("0_Shockwave", mat, out ParticleSystemRenderer r);
            r.sortingFudge = 25f;

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.22f;
            main.startSpeed = 0f;
            main.startSize = 1.6f * s;
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.55f);
            sizeCurve.AddKey(0.4f, 2.1f);
            sizeCurve.AddKey(1f, 5.0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.82f, 0.84f, 0.86f), 0f),
                    new GradientColorKey(new Color(0.55f, 0.55f, 0.56f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.30f, 0f),
                    new GradientAlphaKey(0.10f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // =====================================================================
        // 1. FLASH — kilatan putih instan (HDR -> bloom)
        // =====================================================================
        private void CreateFlash(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("1_Flash", mat, out ParticleSystemRenderer r);
            r.sortingFudge = -30f;

            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(6.5f * s, 8.5f * s);
            main.startColor = new Color(1f, 0.97f, 0.9f, 1f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 1),
                new ParticleSystem.Burst(0.05f, 1) // kilatan kedua tipis, biar tidak pop-out
            });

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.7f);
            sizeCurve.AddKey(1f, 1.35f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.97f, 0.9f), 0f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.7f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.7f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // =====================================================================
        // 2. FIREBALL CORE — inti terpanas (putih-kuning)
        // =====================================================================
        private void CreateFireballCore(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("2_FireballCore", mat, out ParticleSystemRenderer r);
            r.sortingFudge = -20f;

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * s, 4.5f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(1.2f * s, 2f * s);
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 10) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f * s;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.45f);
            sizeCurve.AddKey(0.4f, 1.5f);
            sizeCurve.AddKey(1f, 1.3f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.98f, 0.9f), 0f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0.4f),
                    new GradientColorKey(new Color(1f, 0.72f, 0.25f), 0.8f),
                    new GradientColorKey(new Color(1f, 0.6f, 0.15f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.5f * s, 1.2f * s);
            noise.frequency = 1.2f;
            noise.scrollSpeed = 2f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        // =====================================================================
        // 3. FIREBALL BODY — bola api utama yang bergulung
        // =====================================================================
        private void CreateFireballBody(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("3_FireballBody", mat, out ParticleSystemRenderer r);
            r.sortingFudge = -10f;

            var main = ps.main;
            main.duration = 0.7f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f * s, 9f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(2f * s, 3.4f * s);
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 26),
                new ParticleSystem.Burst(0.08f, 14) // gelombang kedua: apinya bergulung keluar
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f * s;

            // Buoyancy: api menggelembung naik, bukan jatuh seperti logam
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            AnimationCurve rise = new AnimationCurve();
            rise.AddKey(0f, 0f);
            rise.AddKey(0.3f, 2.4f * s);
            rise.AddKey(1f, 1.2f * s);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, rise);

            // Ekspansi fireball melambat karena hambatan udara
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.space = ParticleSystemSimulationSpace.World;
            limit.limit = new ParticleSystem.MinMaxCurve(6f * s);
            limit.dampen = 0.3f;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.35f);
            sizeCurve.AddKey(0.3f, 1.35f);
            sizeCurve.AddKey(0.6f, 1.5f);
            sizeCurve.AddKey(1f, 1.15f); // mengempis pelan saat mendingin
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Transisi warna api: kuning terang -> oranye pekat -> merah bara -> jelaga
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.82f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.07f), 0.4f),
                    new GradientColorKey(new Color(0.8f, 0.13f, 0.02f), 0.72f),
                    new GradientColorKey(new Color(0.16f, 0.07f, 0.045f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0.85f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(1.5f * s, 3.2f * s);
            noise.frequency = 0.55f;
            noise.scrollSpeed = 1.1f;
            noise.damping = true;
            noise.octaveCount = 2;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        // =====================================================================
        // 4. FIREBALL OUTER — selubung luar yang lebih dingin
        // =====================================================================
        private void CreateFireballOuter(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("4_FireballOuter", mat, out ParticleSystemRenderer r);
            r.sortingFudge = 0f;

            var main = ps.main;
            main.duration = 0.9f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * s, 4f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(2.6f * s, 4.4f * s);
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0.05f, 16),
                new ParticleSystem.Burst(0.2f, 8)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.8f * s;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            AnimationCurve rise = new AnimationCurve();
            rise.AddKey(0f, 0f);
            rise.AddKey(0.4f, 1.6f * s);
            rise.AddKey(1f, 0.9f * s);
            velocity.y = new ParticleSystem.MinMaxCurve(1f, rise);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.5f);
            sizeCurve.AddKey(0.5f, 1.15f);
            sizeCurve.AddKey(1f, 1.4f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Lebih dingin dari Body: mulai dari oranye, habis jadi cokelat jelaga
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.52f, 0.1f), 0f),
                    new GradientColorKey(new Color(0.68f, 0.15f, 0.03f), 0.5f),
                    new GradientColorKey(new Color(0.32f, 0.08f, 0.04f), 0.8f),
                    new GradientColorKey(new Color(0.17f, 0.1f, 0.08f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(2f * s, 3.6f * s);
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.8f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
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
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f * s, 2f * s);
            // Gumpalan besar & saling tumpuk rapat supaya tidak terlihat bola-bola terpisah
            main.startSize = new ParticleSystem.MinMaxCurve(3f * s, 4.6f * s);
            main.startColor = Color.white;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI); // tiap gumpalan beda rotasi
            main.gravityModifier = -0.12f; // asap panas mengapung

            var emission = ps.emission;
            emission.rateOverTime = 10f * s; // tetesan kontinu mengisi celah antar-burst
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0.15f, 16), // asap mulai menyusul dari balik api
                new ParticleSystem.Burst(0.45f, 14)
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f * s;
            shape.radiusThickness = 0.35f; // diisi dari volume (bukan cangkang) → massa menyatu
            shape.randomDirectionAmount = 0.6f; // arah sebar tidak seragam, biar tidak "mekar" teratur

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            AnimationCurve drift = new AnimationCurve();
            drift.AddKey(0f, 0f);
            drift.AddKey(0.35f, 1.4f * s);
            drift.AddKey(1f, 0.5f * s); // naik lalu tertahan saat mendingin
            velocity.y = new ParticleSystem.MinMaxCurve(1f, drift);

            // Rem gumpalan: tiap puff berhenti mengembang cepat → menumpuk jadi satu massa
            var limit = ps.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.space = ParticleSystemSimulationSpace.World;
            limit.limit = new ParticleSystem.MinMaxCurve(2.2f * s);
            limit.dampen = 0.45f;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.55f);
            sizeCurve.AddKey(0.3f, 1f);
            sizeCurve.AddKey(1f, 1.55f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Alpha per-gumpalan RENDAH (0.45) — kepadatan dibangun dari tumpukan,
            // jadi tepi tiap blob tidak kelihatan, hasilnya asap pekat menyatu.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.15f, 0.13f, 0.115f), 0f),
                    new GradientColorKey(new Color(0.30f, 0.29f, 0.28f), 0.7f),
                    new GradientColorKey(new Color(0.42f, 0.42f, 0.43f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.42f, 0.12f),
                    new GradientAlphaKey(0.38f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(1.2f * s, 2.4f * s);
            noise.frequency = 0.3f; // fitur lebih besar = pusaran asap, bukan keriting kecil
            noise.scrollSpeed = 0.5f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        // =====================================================================
        // 6. SHRAPNEL — serpihan logam berpijar (stretch + trail)
        // =====================================================================
        private void CreateShrapnel(Material mat, float s)
        {
            ParticleSystem ps = CreateSystem("6_Shrapnel", mat, out ParticleSystemRenderer r);
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.12f;
            r.lengthScale = 1.6f;
            r.sortingFudge = -15f;
            r.trailMaterial = mat;

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(16f * s, 34f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f * s, 0.22f * s);
            main.startColor = Color.white;
            main.gravityModifier = 2.6f; // besi jatuh melengkung tajam

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.03f, 26) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.2f * s;

            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = 0.35f;
            trails.lifetime = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
            trails.minVertexDistance = 0.04f * s;
            trails.worldSpace = true;
            trails.dieWithParticles = true;
            AnimationCurve trailWidth = new AnimationCurve();
            trailWidth.AddKey(0f, 1f);
            trailWidth.AddKey(1f, 0.15f);
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, trailWidth);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.6f, 0.16f), 0f),
                    new GradientColorKey(new Color(0.6f, 0.16f, 0.03f), 0.45f),
                    new GradientColorKey(new Color(0.14f, 0.11f, 0.1f), 0.8f),
                    new GradientColorKey(new Color(0.1f, 0.09f, 0.09f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.7f),
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
            r.velocityScale = 0.06f;
            r.lengthScale = 2.2f;
            r.sortingFudge = -25f;

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(9f * s, 20f * s);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f * s, 0.1f * s);
            main.startColor = Color.white;
            main.gravityModifier = 0.9f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f * s;
            shape.randomDirectionAmount = 0.4f;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.4f * s, 1f * s);
            noise.frequency = 2.2f;
            noise.scrollSpeed = 3f;
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Low;

            // Alpha "zigzag" = efek berkelip khas percikan logam panas
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = MakeGradient(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.92f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.65f, 0.2f), 0.35f),
                    new GradientColorKey(new Color(0.6f, 0.2f, 0.05f), 0.75f),
                    new GradientColorKey(new Color(0.3f, 0.1f, 0.03f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.55f, 0.18f),
                    new GradientAlphaKey(1f, 0.3f),
                    new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(1f, 0.62f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        // =====================================================================
        // 8. FLASH LIGHT — menerangi lingkungan & bodi mobil sekitar
        // =====================================================================
        private void CreateFlashLight(float s)
        {
            GameObject go = new GameObject("8_FlashLight");
            go.transform.SetParent(transform, false);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.4f);
            light.range = 10f * s;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            ExplosionFlashLight pulse = go.AddComponent<ExplosionFlashLight>();
            pulse.peakIntensity = 14f * s;
            pulse.duration = 0.4f;
            pulse.decayPower = 2.2f;
            pulse.flickerAmount = 0.15f;
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

        /// <summary>
        /// Material URP: additive (api) atau alpha-blend (asap), memakai tekstur
        /// Default-Particle bawaan Unity — tanpa alpha mask eksternal.
        /// </summary>
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
