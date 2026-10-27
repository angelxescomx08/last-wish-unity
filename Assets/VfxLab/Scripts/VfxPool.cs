using System.Collections.Generic;
using UnityEngine;

namespace LastWish.VfxLab
{
    /// <summary>
    /// Object pool for effect prefabs: no Instantiate/Destroy (and no GC) while effects play.
    /// Spawned objects carry a <see cref="PooledVfx"/> that returns them when their particles finish.
    /// </summary>
    public static class VfxPool
    {
        private static readonly Dictionary<GameObject, Stack<PooledVfx>> Free = new Dictionary<GameObject, Stack<PooledVfx>>();
        private static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Free.Clear(); root = null; }

        private static Transform Root
        {
            get
            {
                if (root == null) root = new GameObject("VfxPool").transform;
                return root;
            }
        }

        public static void Prewarm(GameObject prefab, int count)
        {
            if (prefab == null) return;
            var list = new List<PooledVfx>();
            for (var i = 0; i < count; i++) list.Add(Spawn(prefab, Vector3.zero));
            foreach (var p in list) Release(p);
        }

        public static PooledVfx Spawn(GameObject prefab, Vector3 position)
        {
            if (!Free.TryGetValue(prefab, out var stack)) Free[prefab] = stack = new Stack<PooledVfx>();
            PooledVfx item = null;
            while (stack.Count > 0 && item == null) item = stack.Pop();          // skip destroyed entries
            if (item == null)
            {
                var go = Object.Instantiate(prefab, position, Quaternion.identity, Root);
                if (!go.TryGetComponent(out item)) item = go.AddComponent<PooledVfx>();
                item.Prefab = prefab;
            }
            item.transform.SetPositionAndRotation(position, Quaternion.identity);
            item.gameObject.SetActive(true);
            return item;
        }

        public static T Spawn<T>(GameObject prefab, Vector3 position) where T : Component
        {
            return Spawn(prefab, position).GetComponent<T>();
        }

        public static void Release(PooledVfx item)
        {
            if (item == null || item.Prefab == null) return;
            item.gameObject.SetActive(false);
            if (!Free.TryGetValue(item.Prefab, out var stack)) Free[item.Prefab] = stack = new Stack<PooledVfx>();
            if (!stack.Contains(item)) stack.Push(item);
        }

        public static int FreeCount(GameObject prefab) => Free.TryGetValue(prefab, out var s) ? s.Count : 0;
    }
}
