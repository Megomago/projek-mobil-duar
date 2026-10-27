using System.Collections.Generic;
using UnityEngine;

namespace Weapons
{
    /// <summary>
    /// Simple generic object pooler for optimizing projectiles, particle effects, and casings.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        [Tooltip("Jumlah objek yang di-instantiate langsung saat prefab pertama kali dipakai (cold start). Ini memindahkan spike alloc dari tiap-tembakan ke satu kali saja, dan pool hidup terus antar scene.")]
        public int lazyWarmupCount = 8;

        private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
        private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

        private class CachedRefs
        {
            public ParticleSystem[] Particles;
            public TrailRenderer[] Trails;
            public Rigidbody Body;
        }

        private readonly Dictionary<GameObject, CachedRefs> _refsCache = new Dictionary<GameObject, CachedRefs>();
        private readonly List<GameObject> _deadKeys = new List<GameObject>();

        private struct DelayedDespawnData
        {
            public GameObject Obj;
            public float DespawnTime;
        }
        private List<DelayedDespawnData> delayedDespawns = new List<DelayedDespawnData>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Pool hidup antar scene — kalau mati, semua object di-destroy massal
                // (GC spike saat transisi) dan battlefield berikutnya mulai dingin lagi.
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            DespawnAll();
            PruneDestroyedEntries();
        }

        private void PruneDestroyedEntries()
        {
            _deadKeys.Clear();
            foreach (var pair in _refsCache)
            {
                if (pair.Key == null) _deadKeys.Add(pair.Key);
            }
            for (int i = 0; i < _deadKeys.Count; i++)
                _refsCache.Remove(_deadKeys[i]);

            _deadKeys.Clear();
            foreach (var pair in instanceToPrefabMap)
            {
                if (pair.Key == null) _deadKeys.Add(pair.Key);
            }
            for (int i = 0; i < _deadKeys.Count; i++)
                instanceToPrefabMap.Remove(_deadKeys[i]);
        }

        /// <summary>
        /// Mengembalikan semua objek yang sedang aktif keluar ke pool.
        /// Dipanggil otomatis saat perpindahan scene (misal tekan ESC ke Garasi / masuk Battlefield)
        /// agar tidak ada partikel ledakan, kobaran api, selongsong, atau peluru tersisa di scene baru.
        /// </summary>
        public void DespawnAll()
        {
            delayedDespawns.Clear();

            foreach (var pair in instanceToPrefabMap)
            {
                GameObject obj = pair.Key;
                if (obj != null && obj.activeSelf)
                {
                    Despawn(obj);
                }
            }
        }

        public void Warmup(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;

            if (!poolDictionary.ContainsKey(prefab))
                poolDictionary[prefab] = new Queue<GameObject>();

            var queue = poolDictionary[prefab];
            int existing = queue.Count;

            for (int i = existing; i < count; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                instanceToPrefabMap[obj] = prefab;
                queue.Enqueue(obj);
            }
        }

        /// <summary>
        /// Mengambil objek dari pool, atau membuat yang baru jika kosong.
        /// </summary>
        public T Spawn<T>(GameObject prefab, Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject obj = Spawn(prefab, position, rotation);
            return obj != null ? obj.GetComponent<T>() : null;
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null) return null;

            if (!poolDictionary.ContainsKey(prefab))
            {
                poolDictionary[prefab] = new Queue<GameObject>();
                // Cold start: langsung warmup beberapa objek sekaligus (satu kali alloc),
                // supaya tembakan berikutnya tidak Instantiate per-tembakan.
                Warmup(prefab, lazyWarmupCount);
            }

            GameObject objectToSpawn;

            if (poolDictionary[prefab].Count > 0)
            {
                objectToSpawn = poolDictionary[prefab].Dequeue();
                objectToSpawn.transform.SetParent(null);
                objectToSpawn.transform.position = position;
                objectToSpawn.transform.rotation = rotation;
                objectToSpawn.SetActive(true);
            }
            else
            {
                objectToSpawn = Instantiate(prefab, position, rotation);
                objectToSpawn.transform.SetParent(null);
                instanceToPrefabMap[objectToSpawn] = prefab;
            }

            return objectToSpawn;
        }

        /// <summary>
        /// Mengembalikan objek ke dalam pool.
        /// </summary>
        public void Despawn(GameObject obj)
        {
            if (obj == null) return;

            // Mencegah duplicate enqueue jika object sudah despawn/inactive
            if (!obj.activeSelf) return;

            // Bersihkan sisa partikel & trail agar tidak 'flash' saat dipake ulang.
            // Komponen di-cache sekali per instance supaya tidak alloc tiap despawn.
            if (!_refsCache.TryGetValue(obj, out CachedRefs refs))
            {
                refs = new CachedRefs
                {
                    Particles = obj.GetComponentsInChildren<ParticleSystem>(true),
                    Trails = obj.GetComponentsInChildren<TrailRenderer>(true),
                    Body = obj.GetComponent<Rigidbody>()
                };
                _refsCache[obj] = refs;
            }

            var particles = refs.Particles;
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null) particles[i].Clear();
            }

            var trails = refs.Trails;
            for (int i = 0; i < trails.Length; i++)
            {
                if (trails[i] != null) trails[i].Clear();
            }

            var rb = refs.Body;
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform); // Tarik kembali ke Object Pool agar aman dari Destroy parent

            if (instanceToPrefabMap.TryGetValue(obj, out GameObject prefab))
            {
                if (poolDictionary.ContainsKey(prefab))
                {
                    poolDictionary[prefab].Enqueue(obj);
                }
            }
            else
            {
                // Jika tidak tercatat di pool, hancurkan biasa (fallback)
                Destroy(obj);
            }
        }

        /// <summary>
        /// Mengembalikan objek ke dalam pool setelah jeda waktu tertentu.
        /// </summary>
        public void Despawn(GameObject obj, float delay)
        {
            if (gameObject.activeInHierarchy)
            {
                delayedDespawns.Add(new DelayedDespawnData { Obj = obj, DespawnTime = Time.time + delay });
            }
            else
            {
                Despawn(obj);
            }
        }

        private void Update()
        {
            for (int i = delayedDespawns.Count - 1; i >= 0; i--)
            {
                if (Time.time >= delayedDespawns[i].DespawnTime)
                {
                    Despawn(delayedDespawns[i].Obj);
                    delayedDespawns.RemoveAt(i);
                }
            }
        }
    }
}
