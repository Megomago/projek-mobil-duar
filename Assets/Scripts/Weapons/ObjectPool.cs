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

        private Dictionary<GameObject, Queue<GameObject>> poolDictionary = new Dictionary<GameObject, Queue<GameObject>>();
        private Dictionary<GameObject, GameObject> instanceToPrefabMap = new Dictionary<GameObject, GameObject>();

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
            }
            else
            {
                Destroy(gameObject);
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
                objectToSpawn.transform.SetParent(transform);
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
