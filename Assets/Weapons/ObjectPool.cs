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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // Optional: DontDestroyOnLoad(gameObject); // Jika butuh lintas scene
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Mengambil objek dari pool, atau membuat yang baru jika kosong.
        /// </summary>
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
                objectToSpawn.transform.position = position;
                objectToSpawn.transform.rotation = rotation;
                objectToSpawn.SetActive(true);
            }
            else
            {
                objectToSpawn = Instantiate(prefab, position, rotation);
                instanceToPrefabMap[objectToSpawn] = prefab; // Ingat asalnya
            }

            return objectToSpawn;
        }

        /// <summary>
        /// Mengembalikan objek ke dalam pool.
        /// </summary>
        public void Despawn(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);

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
    }
}
