using Portal2D.Core.Events;
using SeamlessPortals2D;
using UnityEngine;
using PortalBody = SeamlessPortals2D.Portal2D;

namespace Portal2D.Presentation.Portal
{
    public sealed class PortalManager : MonoBehaviour
    {
        private static PortalManager instance;
        public static PortalManager Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<PortalManager>();
                if (instance == null) instance = new GameObject("PortalManager").AddComponent<PortalManager>();
                return instance;
            }
        }
        [SerializeField] private PortalBody bluePrefab, redPrefab;
        [SerializeField] private LayerMask surfaceMask = ~0;
        public PortalBody BluePortal { get; private set; }
        public PortalBody RedPortal { get; private set; }
        public float PortalLength => bluePrefab != null ? bluePrefab.Length : 2.2f;
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            if (bluePrefab == null) bluePrefab = Resources.Load<PortalBody>("Gameplay/PortalA");
            if (redPrefab == null) redPrefab = Resources.Load<PortalBody>("Gameplay/PortalB");
        }
        private void OnEnable()
        {
            EventBus.Subscribe<LevelUnloadingEvent>(OnUnloading);
            EventBus.Subscribe<PortalPlacementRequested>(OnPlacement);
            EventBus.Subscribe<PortalSnapshotRequested>(OnSnapshot);
        }
        private void OnDisable()
        {
            EventBus.Unsubscribe<LevelUnloadingEvent>(OnUnloading);
            EventBus.Unsubscribe<PortalPlacementRequested>(OnPlacement);
            EventBus.Unsubscribe<PortalSnapshotRequested>(OnSnapshot);
            ClearAllPortals();
        }
        private void OnPlacement(PortalPlacementRequested request) => DeployPortal(request.Type, request.Position, request.Normal);
        private void OnSnapshot(PortalSnapshotRequested request)
        {
            if (BluePortal != null) request.Receive?.Invoke(0, BluePortal.transform.position, BluePortal.Normal);
            if (RedPortal != null) request.Receive?.Invoke(1, RedPortal.transform.position, RedPortal.Normal);
        }
        private void OnDestroy() { if (instance == this) instance = null; }
        private void OnUnloading(LevelUnloadingEvent evt) => ClearAllPortals();
        public bool TryPlacement(PortalType type, Vector2 point, Vector2 normal, out PortalPlacement placement)
        {
            if (!PortalPlacement2D.TryFit(point, normal, PortalLength, surfaceMask, out placement)) return false;
            var other = type == PortalType.Blue ? RedPortal : BluePortal;
            if (other != null && Vector2.Distance(placement.Position, other.transform.position) < PortalLength + 0.05f &&
                Mathf.Abs(Vector2.Dot(placement.Normal, other.Normal)) > 0.99f) return false;
            return true;
        }
        public PortalBody DeployPortal(PortalType type, Vector2 position, Vector2 normal)
        {
            Physics2D.SyncTransforms();
            if (!TryPlacement(type, position, normal, out var placement)) return null;
            var prefab = type == PortalType.Blue ? bluePrefab : redPrefab;
            if (prefab == null) { Debug.LogError("Portal prefab is missing", this); return null; }
            var portal = type == PortalType.Blue ? BluePortal : RedPortal;
            if (portal == null) portal = Instantiate(prefab);
            portal.name = type == PortalType.Blue ? "Portal A" : "Portal B";
            portal.Place(placement.Position, placement.Normal, placement.Supports);
            if (type == PortalType.Blue) BluePortal = portal; else RedPortal = portal;
            var partner = type == PortalType.Blue ? RedPortal : BluePortal;
            if (partner != null) portal.Link(partner);
            EventBus.Publish(new PortalPlacedEvent(type, placement.Position, placement.Normal));
            return portal;
        }
        public void ClearAllPortals()
        {
            foreach (var p in new[] { BluePortal, RedPortal })
                if (p != null) { p.Unlink(); p.gameObject.SetActive(false); if (Application.isPlaying) Destroy(p.gameObject); else DestroyImmediate(p.gameObject); }
            BluePortal = RedPortal = null;
        }
    }
}
