using System;
using UnityEngine;

namespace SeamlessPortals2D
{
    public enum PortalTransitPhase { Entered, Teleported, Exited, Cancelled }

    public readonly struct PortalTransitEvent
    {
        public readonly PortalTransitPhase Phase;
        public readonly Rigidbody2D Body;
        public readonly Portal2D Entry;
        public readonly Portal2D Exit;
        public readonly Vector2 PreviousPosition;
        public readonly Vector2 Position;
        public readonly Vector2 Velocity;
        public readonly float RotationDelta;

        public PortalTransitEvent(PortalTransitPhase phase, Rigidbody2D body, Portal2D entry,
            Portal2D exit, Vector2 previousPosition, float rotationDelta = 0)
        {
            Phase = phase; Body = body; Entry = entry; Exit = exit;
            PreviousPosition = previousPosition;
            Position = body != null ? body.position : previousPosition;
            Velocity = body != null ? body.linearVelocity : Vector2.zero;
            RotationDelta = rotationDelta;
        }
    }

    /// <summary>The portable module's only game-facing notification channel.</summary>
    public static class PortalEvents
    {
        public static event Action<PortalTransitEvent> Transit;
        internal static void Publish(PortalTransitEvent value)
        {
            var snapshot = Transit;
            if (snapshot == null) return;
            foreach (Action<PortalTransitEvent> handler in snapshot.GetInvocationList())
            {
                try { handler(value); } catch (Exception error) { Debug.LogException(error); }
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Transit = null;
    }
}
