using UnityEngine;
namespace Portal2D.Core.Events
{
    public enum PortalType { Blue, Red }
    public readonly struct PortalPlacedEvent
    {
        public readonly PortalType Type;
        public readonly Vector2 Position, Normal;
        public PortalPlacedEvent(PortalType type, Vector2 position, Vector2 normal) { Type = type; Position = position; Normal = normal; }
    }
    public readonly struct LevelUnloadingEvent { }
    public readonly struct PortalPlacementRequested
    {
        public readonly PortalType Type;
        public readonly Vector2 Position, Normal;
        public PortalPlacementRequested(PortalType type, Vector2 position, Vector2 normal) { Type = type; Position = position; Normal = normal; }
    }
    public readonly struct PortalSnapshotRequested
    {
        public readonly System.Action<int, Vector2, Vector2> Receive;
        public PortalSnapshotRequested(System.Action<int, Vector2, Vector2> receive) => Receive = receive;
    }
    public readonly struct ActivateOrbRequest
    {
        public readonly GameObject Target;
        public readonly Vector2 PlayerPosition;
        public ActivateOrbRequest(GameObject target, Vector2 playerPosition) { Target = target; PlayerPosition = playerPosition; }
    }
}
