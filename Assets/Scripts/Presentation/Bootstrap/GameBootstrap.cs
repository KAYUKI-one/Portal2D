using Portal2D.Presentation.CameraControl;
using Portal2D.Presentation.Input;
using Portal2D.Presentation.Inventory;
using Portal2D.Presentation.Level;
using Portal2D.Presentation.Portal;
using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Bootstrap
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private PlayerEntityView scenePlayerCharacter;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private float cameraSmoothTime = 0.16f, maxZoomMultiplier = 1.8f;
        public PlayerEntityView ActivePlayerView { get; private set; }
        public LevelManager LevelManager => levelManager;
        private void Awake()
        {
            ActivePlayerView = scenePlayerCharacter != null && scenePlayerCharacter.gameObject.activeInHierarchy
                ? scenePlayerCharacter : FindAnyObjectByType<PlayerEntityView>();
            if (ActivePlayerView == null)
            {
                var prefab = playerPrefab != null ? playerPrefab : Resources.Load<GameObject>("Gameplay/Player");
                if (prefab == null) { Debug.LogError("Player prefab is missing", this); enabled = false; return; }
                ActivePlayerView = Instantiate(prefab).GetComponent<PlayerEntityView>();
            }
            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            var follow = cam.GetComponent<SmoothCameraController>() ?? cam.gameObject.AddComponent<SmoothCameraController>();
            follow.Initialize(ActivePlayerView.transform, 6, maxZoomMultiplier, cameraSmoothTime);
            if (GetComponent<PlayerInputAdapter>() == null) gameObject.AddComponent<PlayerInputAdapter>();
            levelManager = levelManager != null ? levelManager : GetComponent<LevelManager>() ?? gameObject.AddComponent<LevelManager>();
            levelManager.Initialize(ActivePlayerView, follow);
            _ = PortalManager.Instance;
            InventoryManager.Instance.EnsureInitialized();
            var gun = ActivePlayerView.GetComponent<PortalGunController>() ?? ActivePlayerView.gameObject.AddComponent<PortalGunController>();
            gun.EnsureDependencies();
            _ = UI.Components.UIManager.Instance;
        }
    }
}
