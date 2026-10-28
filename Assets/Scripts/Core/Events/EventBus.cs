using System;
using System.Collections.Generic;

namespace Portal2D.Core.Events
{
    /// <summary>
    /// 强类型、零依赖解耦事件管线
    /// 核心逻辑层与表现层 (View/Audio/VFX) 的通信通道
    /// </summary>
    public static class EventBus
    {
        private interface IHandlerList
        {
            void Clear();
        }

        private class HandlerList<T> : IHandlerList
        {
            private readonly List<Action<T>> _handlers = new List<Action<T>>(8);
            private readonly List<Action<T>> _toAdd = new List<Action<T>>(2);
            private readonly List<Action<T>> _toRemove = new List<Action<T>>(2);
            private bool _isInvoking = false;

            public void Subscribe(Action<T> handler)
            {
                if (_isInvoking)
                {
                    _toAdd.Add(handler);
                }
                else if (!_handlers.Contains(handler))
                {
                    _handlers.Add(handler);
                }
            }

            public void Unsubscribe(Action<T> handler)
            {
                if (_isInvoking)
                {
                    _toRemove.Add(handler);
                }
                else
                {
                    _handlers.Remove(handler);
                }
            }

            public void Publish(T evt)
            {
                _isInvoking = true;
                for (int i = 0; i < _handlers.Count; i++)
                {
                    _handlers[i]?.Invoke(evt);
                }
                _isInvoking = false;

                // Process pending adds/removes to prevent collection modified issues
                if (_toRemove.Count > 0)
                {
                    for (int i = 0; i < _toRemove.Count; i++)
                    {
                        _handlers.Remove(_toRemove[i]);
                    }
                    _toRemove.Clear();
                }

                if (_toAdd.Count > 0)
                {
                    for (int i = 0; i < _toAdd.Count; i++)
                    {
                        if (!_handlers.Contains(_toAdd[i]))
                        {
                            _handlers.Add(_toAdd[i]);
                        }
                    }
                    _toAdd.Clear();
                }
            }

            public void Clear()
            {
                _handlers.Clear();
                _toAdd.Clear();
                _toRemove.Clear();
                _isInvoking = false;
            }
        }

        private static readonly Dictionary<Type, IHandlerList> _eventHandlers = new Dictionary<Type, IHandlerList>(32);

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_eventHandlers.TryGetValue(type, out var list))
            {
                list = new HandlerList<T>();
                _eventHandlers[type] = list;
            }
            ((HandlerList<T>)list).Subscribe(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_eventHandlers.TryGetValue(type, out var list))
            {
                ((HandlerList<T>)list).Unsubscribe(handler);
            }
        }

        public static void Publish<T>(T evt)
        {
            var type = typeof(T);
            if (_eventHandlers.TryGetValue(type, out var list))
            {
                ((HandlerList<T>)list).Publish(evt);
            }
        }

        public static void Clear()
        {
            foreach (var list in _eventHandlers.Values)
            {
                list.Clear();
            }
            _eventHandlers.Clear();
        }
    }
}
