using System;
using System.Collections.Generic;

namespace Portal2D.Core.ECS
{
    public interface IComponentPool
    {
        bool Has(int entityId);
        bool Remove(int entityId);
        void Clear();
        int Count { get; }
        int GetEntityAt(int denseIndex);
    }

    /// <summary>
    /// 基于 Sparse-Set (稀疏集合) 的高性能组件池
    /// 支持纯结构体零装箱、O(1)增删查、按引用读写 (ref T)
    /// </summary>
    public class ComponentPool<T> : IComponentPool where T : struct, IComponent
    {
        private int[] _sparse;
        private int[] _dense;
        private T[] _components;
        private int _count;

        public int Count => _count;

        public ComponentPool(int initialCapacity = 64)
        {
            _sparse = new int[initialCapacity];
            Array.Fill(_sparse, -1);
            _dense = new int[initialCapacity];
            _components = new T[initialCapacity];
            _count = 0;
        }

        public bool Has(int entityId)
        {
            if (entityId <= 0 || entityId >= _sparse.Length) return false;
            int denseIdx = _sparse[entityId];
            return denseIdx >= 0 && denseIdx < _count && _dense[denseIdx] == entityId;
        }

        public ref T Set(int entityId, T component)
        {
            EnsureCapacity(entityId);

            int denseIdx = _sparse[entityId];
            if (denseIdx >= 0 && denseIdx < _count && _dense[denseIdx] == entityId)
            {
                _components[denseIdx] = component;
                return ref _components[denseIdx];
            }

            // New addition
            denseIdx = _count;
            if (denseIdx >= _dense.Length)
            {
                int newCap = _dense.Length * 2;
                Array.Resize(ref _dense, newCap);
                Array.Resize(ref _components, newCap);
            }

            _dense[denseIdx] = entityId;
            _sparse[entityId] = denseIdx;
            _components[denseIdx] = component;
            _count++;

            return ref _components[denseIdx];
        }

        public ref T Get(int entityId)
        {
            int denseIdx = _sparse[entityId];
            return ref _components[denseIdx];
        }

        public bool TryGet(int entityId, out T component)
        {
            if (Has(entityId))
            {
                component = _components[_sparse[entityId]];
                return true;
            }
            component = default;
            return false;
        }

        public bool Remove(int entityId)
        {
            if (!Has(entityId)) return false;

            int denseIdx = _sparse[entityId];
            int lastDenseIdx = _count - 1;

            if (denseIdx != lastDenseIdx)
            {
                // Swap with last element
                int lastEntity = _dense[lastDenseIdx];
                _dense[denseIdx] = lastEntity;
                _components[denseIdx] = _components[lastDenseIdx];
                _sparse[lastEntity] = denseIdx;
            }

            _sparse[entityId] = -1;
            _count--;
            return true;
        }

        public int GetEntityAt(int denseIndex) => _dense[denseIndex];

        public ref T GetComponentAt(int denseIndex) => ref _components[denseIndex];

        public void Clear()
        {
            Array.Fill(_sparse, -1);
            _count = 0;
        }

        private void EnsureCapacity(int entityId)
        {
            if (entityId >= _sparse.Length)
            {
                int newCap = Math.Max(entityId + 1, _sparse.Length * 2);
                int oldCap = _sparse.Length;
                Array.Resize(ref _sparse, newCap);
                for (int i = oldCap; i < newCap; i++)
                {
                    _sparse[i] = -1;
                }
            }
        }
    }
}
