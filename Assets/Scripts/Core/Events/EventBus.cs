using System;
using System.Collections.Generic;
using UnityEngine;
namespace Portal2D.Core.Events
{
    public static class EventBus
    {
        private static readonly Dictionary<Type, Delegate> Handlers = new();
        public static void Subscribe<T>(Action<T> handler)
        {
            Unsubscribe(handler);
            Handlers.TryGetValue(typeof(T), out var previous);
            Handlers[typeof(T)] = Delegate.Combine(previous, handler);
        }
        public static void Unsubscribe<T>(Action<T> handler)
        {
            if (!Handlers.TryGetValue(typeof(T), out var previous)) return;
            var remaining = Delegate.Remove(previous, handler);
            if (remaining == null) Handlers.Remove(typeof(T)); else Handlers[typeof(T)] = remaining;
        }
        public static void Publish<T>(T message)
        {
            if (!Handlers.TryGetValue(typeof(T), out var snapshot)) return;
            foreach (Action<T> handler in snapshot.GetInvocationList())
            {
                try { handler(message); } catch (Exception error) { Debug.LogException(error); }
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Handlers.Clear();
    }
}
