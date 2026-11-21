using System;
using System.Collections.Generic;
using UnityEngine;

namespace PixelGravity
{
    /// <summary>
    /// 对象池（全局四单例之一）。
    ///
    /// 设计要点：
    /// 1. 用「池键」而不是预制体引用作为索引——本项目的对象由 ObjectFactory 在代码里构建，
    ///    没有 Prefab 资产，用键名同样能做到"一种对象一条队列"。
    /// 2. 池空了就现场扩容，绝不返回 null，调用方不用写判空。
    /// 3. 回收时只失活 + 归队，不 Destroy，因此运行时不会产生 GC 尖峰。
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        private readonly Dictionary<string, Queue<GameObject>> pools = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, Func<GameObject>> factories = new Dictionary<string, Func<GameObject>>();

        /// <summary>
        /// 每个池对象身上的 OnSpawn / OnDespawn 接收者，建池时算一次就存下来。
        ///
        /// 原来每次 Spawn 都调 GetComponentsInChildren —— 它返回的是**新数组**，
        /// 等于每取一颗子弹就分配一次。对象池存在的意义就是消掉这个分配，
        /// 这样写等于把省下来的又还回去一部分。池对象身上组件是固定的，
        /// 建的时候缓存一次就够，取放都只是查字典。
        /// </summary>
        private readonly Dictionary<GameObject, IPoolable[]> poolablesByObject = new Dictionary<GameObject, IPoolable[]>();

        private Transform poolRoot;

        /// <summary>累计创建过的对象数，用于报告里「对象池前后实例化次数对比」</summary>
        public int CreatedCount { get; private set; }

        private void Awake()
        {
            Instance = this;
            GameObject root = new GameObject("_Pool");
            root.transform.SetParent(transform, false);
            poolRoot = root.transform;
        }

        /// <summary>
        /// 注册一种对象。factory 负责「造一个新的」，preload 是预热数量。
        /// 预热的意义：避免第一次开火/第一波敌人才临时创建造成的卡顿。
        /// </summary>
        public void Register(string key, Func<GameObject> factory, int preload)
        {
            if (factories.ContainsKey(key)) return;

            factories[key] = factory;
            pools[key] = new Queue<GameObject>();

            for (int i = 0; i < preload; i++)
            {
                GameObject go = CreateNew(key);
                go.SetActive(false);
                go.transform.SetParent(poolRoot, false);
                pools[key].Enqueue(go);
            }
        }

        private GameObject CreateNew(string key)
        {
            GameObject go = factories[key]();
            go.name = key;
            CreatedCount++;

            // 组件在出生这一刻就固定了，这里把回调列表缓存下来
            poolablesByObject[go] = go.GetComponentsInChildren<IPoolable>(true);
            return go;
        }

        /// <summary>取回调列表。正常情况下永远命中缓存，兜底那行只是防御。</summary>
        private IPoolable[] PoolablesOf(GameObject go)
        {
            IPoolable[] cached;
            if (poolablesByObject.TryGetValue(go, out cached)) return cached;

            cached = go.GetComponentsInChildren<IPoolable>(true);
            poolablesByObject[go] = cached;
            return cached;
        }

        /// <summary>取出一个对象并放到指定位置；池为空时自动扩容</summary>
        public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
        {
            Queue<GameObject> queue;
            if (!pools.TryGetValue(key, out queue))
            {
                Debug.LogError("[PoolManager] 未注册的池键：" + key);
                return null;
            }

            GameObject go = queue.Count > 0 ? queue.Dequeue() : CreateNew(key);
            go.transform.SetParent(null);
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);

            IPoolable[] poolables = PoolablesOf(go);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnSpawn();
            return go;
        }

        public T Spawn<T>(string key, Vector3 position, Quaternion rotation) where T : Component
        {
            GameObject go = Spawn(key, position, rotation);
            return go != null ? go.GetComponent<T>() : null;
        }

        /// <summary>
        /// 回收：先让对象自己复位，再失活归队。
        /// 开头判断 activeSelf 是为了防重复回收——同一个对象被回收两次会让它同时出现在队列里两次，
        /// 之后就会被"生成两份"，是对象池最隐蔽的一类 bug。
        /// </summary>
        public void Despawn(string key, GameObject go)
        {
            if (go == null) return;
            if (!go.activeSelf) return;

            Queue<GameObject> queue;
            if (!pools.TryGetValue(key, out queue))
            {
                poolablesByObject.Remove(go);
                Destroy(go);
                return;
            }

            IPoolable[] poolables = PoolablesOf(go);
            for (int i = 0; i < poolables.Length; i++) poolables[i].OnDespawn();

            go.SetActive(false);
            go.transform.SetParent(poolRoot, false);
            queue.Enqueue(go);
        }

        public int PooledCount(string key)
        {
            Queue<GameObject> queue;
            return pools.TryGetValue(key, out queue) ? queue.Count : 0;
        }
    }
}
