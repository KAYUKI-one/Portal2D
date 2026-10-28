using System;
using System.Collections.Generic;

namespace Portal2D.Core.ECS
{
    public delegate void EntityAction<T1>(Entity entity, ref T1 c1);
    public delegate void EntityAction<T1, T2>(Entity entity, ref T1 c1, ref T2 c2);
    public delegate void EntityAction<T1, T2, T3>(Entity entity, ref T1 c1, ref T2 c2, ref T3 c3);

    /// <summary>
    /// ECS 核心世界容器
    /// 管理所有实体、组件池以及系统生命周期
    /// </summary>
    public class World
    {
        private int _nextEntityId = 1;
        private readonly Queue<int> _freeEntityIds = new Queue<int>(64);
        private readonly HashSet<int> _aliveEntities = new HashSet<int>(128);

        private readonly Dictionary<Type, IComponentPool> _pools = new Dictionary<Type, IComponentPool>(32);
        private readonly List<ISystem> _systems = new List<ISystem>(16);

        public int EntityCount => _aliveEntities.Count;

        #region Entity Lifecycle

        public Entity CreateEntity()
        {
            int id = _freeEntityIds.Count > 0 ? _freeEntityIds.Dequeue() : _nextEntityId++;
            _aliveEntities.Add(id);
            return new Entity(id);
        }

        public bool IsAlive(Entity entity)
        {
            return entity.IsValid && _aliveEntities.Contains(entity.Id);
        }

        public void DestroyEntity(Entity entity)
        {
            if (!IsAlive(entity)) return;

            foreach (var pool in _pools.Values)
            {
                pool.Remove(entity.Id);
            }

            _aliveEntities.Remove(entity.Id);
            _freeEntityIds.Enqueue(entity.Id);
        }

        #endregion

        #region Component Management

        public ComponentPool<T> GetPool<T>() where T : struct, IComponent
        {
            var type = typeof(T);
            if (!_pools.TryGetValue(type, out var pool))
            {
                pool = new ComponentPool<T>();
                _pools[type] = pool;
            }
            return (ComponentPool<T>)pool;
        }

        public ref T AddComponent<T>(Entity entity, T component = default) where T : struct, IComponent
        {
            if (!IsAlive(entity)) throw new ArgumentException($"Entity {entity} is not alive.");
            return ref GetPool<T>().Set(entity.Id, component);
        }

        public bool RemoveComponent<T>(Entity entity) where T : struct, IComponent
        {
            if (!IsAlive(entity)) return false;
            return GetPool<T>().Remove(entity.Id);
        }

        public bool HasComponent<T>(Entity entity) where T : struct, IComponent
        {
            if (!IsAlive(entity)) return false;
            return GetPool<T>().Has(entity.Id);
        }

        public ref T GetComponent<T>(Entity entity) where T : struct, IComponent
        {
            if (!IsAlive(entity)) throw new ArgumentException($"Entity {entity} is not alive.");
            return ref GetPool<T>().Get(entity.Id);
        }

        public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent
        {
            if (!IsAlive(entity))
            {
                component = default;
                return false;
            }
            return GetPool<T>().TryGet(entity.Id, out component);
        }

        #endregion

        #region Queries & Iterations

        public void ForEach<T1>(EntityAction<T1> action) where T1 : struct, IComponent
        {
            var pool1 = GetPool<T1>();
            int count = pool1.Count;
            for (int i = 0; i < count; i++)
            {
                int entityId = pool1.GetEntityAt(i);
                action(new Entity(entityId), ref pool1.GetComponentAt(i));
            }
        }

        public void ForEach<T1, T2>(EntityAction<T1, T2> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            var pool1 = GetPool<T1>();
            var pool2 = GetPool<T2>();

            // 遍历较小的池以提高缓存效率
            if (pool1.Count <= pool2.Count)
            {
                int count = pool1.Count;
                for (int i = 0; i < count; i++)
                {
                    int entityId = pool1.GetEntityAt(i);
                    if (pool2.Has(entityId))
                    {
                        action(new Entity(entityId), ref pool1.GetComponentAt(i), ref pool2.Get(entityId));
                    }
                }
            }
            else
            {
                int count = pool2.Count;
                for (int i = 0; i < count; i++)
                {
                    int entityId = pool2.GetEntityAt(i);
                    if (pool1.Has(entityId))
                    {
                        action(new Entity(entityId), ref pool1.Get(entityId), ref pool2.GetComponentAt(i));
                    }
                }
            }
        }

        public void ForEach<T1, T2, T3>(EntityAction<T1, T2, T3> action)
            where T1 : struct, IComponent
            where T2 : struct, IComponent
            where T3 : struct, IComponent
        {
            var pool1 = GetPool<T1>();
            var pool2 = GetPool<T2>();
            var pool3 = GetPool<T3>();

            int count = pool1.Count;
            for (int i = 0; i < count; i++)
            {
                int entityId = pool1.GetEntityAt(i);
                if (pool2.Has(entityId) && pool3.Has(entityId))
                {
                    action(new Entity(entityId), ref pool1.GetComponentAt(i), ref pool2.Get(entityId), ref pool3.Get(entityId));
                }
            }
        }

        #endregion

        #region Systems & World Loop

        public T RegisterSystem<T>(T system) where T : ISystem
        {
            _systems.Add(system);
            system.Initialize(this);
            return system;
        }

        public void Update(float deltaTime)
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Update(deltaTime);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _systems.Count; i++)
            {
                _systems[i].Cleanup();
            }
            _systems.Clear();

            foreach (var pool in _pools.Values)
            {
                pool.Clear();
            }
            _pools.Clear();

            _aliveEntities.Clear();
            _freeEntityIds.Clear();
            _nextEntityId = 1;
        }

        #endregion
    }
}
