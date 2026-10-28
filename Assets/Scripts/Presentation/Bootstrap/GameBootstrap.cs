using Portal2D.Core.Components;
using Portal2D.Core.ECS;
using Portal2D.Core.Events;
using Portal2D.Core.Systems;
using Portal2D.Presentation.CameraControl;
using Portal2D.Presentation.Input;
using Portal2D.Presentation.Inventory;
using Portal2D.Presentation.Level;
using Portal2D.Presentation.Portal;
using Portal2D.Presentation.Views;
using UnityEngine;

namespace Portal2D.Presentation.Bootstrap
{
    /// <summary>
    /// 游戏引导启动器 (Game Bootstrap)
    /// 组装核心 ECS、配置摄像机、委托 LevelManager 完成关卡加载与存档流转
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("--- 关卡管理器 (管理存档与关卡切换) ---")]
        [SerializeField] private LevelManager levelManager;

        [Header("--- 角色素材插拔与预制体 (可直接拖拽替换) ---")]
        [Tooltip("场景中已存在的角色实例 (例如 Amiya)。拖入后将直接接管，留空则自动搜索场景角色")]
        [SerializeField] private PlayerEntityView scenePlayerCharacter;

        [Tooltip("角色预制体 (Prefab)。若场景中无角色，将实例化该预制体")]
        [SerializeField] private GameObject playerPrefab;

        [Header("--- 摄像机配置 ---")]
        [SerializeField] private float cameraSmoothTime = 0.16f;
        [SerializeField] private float maxZoomMultiplier = 1.8f;

        private World _world;
        private Entity _playerEntity;
        private PlayerEntityView _activePlayerView;
        private SmoothCameraController _cameraController;

        public World World => _world;
        public Entity PlayerEntity => _playerEntity;
        public PlayerEntityView ActivePlayerView => _activePlayerView;
        public LevelManager LevelManager => levelManager;

        private void Awake()
        {
            // 1. 初始化核心 ECS 世界
            _world = new World();

            // 2. 准备物理层级 Mask (地面与实心障碍层)
            LayerMask solidMask = Portal2D.Core.Constants.GameLayers.SolidMask;
            LayerMask oneWayMask = Portal2D.Core.Constants.GameLayers.OneWayMask;

            // 3. 注册连续 2D 动力学控制器系统
            _world.RegisterSystem(new PlayerKinematic2DSystem(solidMask, oneWayMask));

            // 4. 发现并组装角色视觉层 (支持插拔)
            ResolvePlayerCharacter();

            // 5. 配置平滑跟随与受限缩放摄像机
            SetupCamera();

            // 6. 输入监听适配器
            var inputObj = new GameObject("[Input Adapter]");
            inputObj.transform.SetParent(transform);
            inputObj.AddComponent<PlayerInputAdapter>();

            // 7. 初始化关卡管理器 (负责读档、关卡资源装配与销毁前关)
            SetupLevelManager();

            // 8. 组装全局 UI 控制器 (主菜单、继续游戏、新游戏、HUD)
            SetupUI();

            // 9. 组装传送门管理器与角色手持传送枪系统
            SetupPortalSystem();
        }

        private void SetupPortalSystem()
        {
            // 初始化全局传送门管理器
            _ = PortalManager.Instance;

            // 为角色自动装配手持传送枪表现与抛物线发射控制器
            if (_activePlayerView != null)
            {
                var gunView = _activePlayerView.GetComponent<PortalGunEquippedView>() ?? _activePlayerView.gameObject.AddComponent<PortalGunEquippedView>();
                var gunCtrl = _activePlayerView.GetComponent<PortalGunController>() ?? _activePlayerView.gameObject.AddComponent<PortalGunController>();
                gunCtrl.EnsureDependencies();
            }

            // 初始化背包默认第一格持有传送枪
            InventoryManager.Instance?.EnsureInitialized();
        }

        private void Update()
        {
            // 驱动核心 ECS 世界动力学更新
            _world?.Update(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _world?.Clear();
            EventBus.Clear();
        }

        private void ResolvePlayerCharacter()
        {
            _activePlayerView = scenePlayerCharacter;

            if (_activePlayerView == null)
            {
                _activePlayerView = FindAnyObjectByType<PlayerEntityView>();
            }

            if (_activePlayerView == null)
            {
                var candidate = GameObject.Find("Amiya") ?? GameObject.FindWithTag("Player");
                if (candidate != null)
                {
                    _activePlayerView = candidate.GetComponent<PlayerEntityView>() ?? candidate.AddComponent<PlayerEntityView>();
                }
            }

            if (_activePlayerView == null && playerPrefab != null)
            {
                var instance = Instantiate(playerPrefab, Vector3.zero, Quaternion.identity);
                _activePlayerView = instance.GetComponent<PlayerEntityView>() ?? instance.AddComponent<PlayerEntityView>();
            }

            if (_activePlayerView == null)
            {
                var fallbackObj = new GameObject("[Player View]");
                _activePlayerView = fallbackObj.AddComponent<PlayerEntityView>();
            }

            Vector2 playerSize = (_activePlayerView.CollisionSize != Vector2.zero) ? _activePlayerView.CollisionSize : new Vector2(0.75f, 0.95f);

            _playerEntity = _world.CreateEntity();
            _world.AddComponent(_playerEntity, new PlayerTagComponent());
            _world.AddComponent(_playerEntity, new Transform2DComponent(Vector2.zero, playerSize));
            _world.AddComponent(_playerEntity, new KinematicMovement2DComponent(moveSpeed: 5.5f, gravity: 28.0f));

            _activePlayerView.BindEntity(_playerEntity, Vector2.zero, playerSize);
        }

        private void SetupCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("Main Camera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }

            cam.orthographic = true;
            cam.backgroundColor = new Color(0.11f, 0.12f, 0.15f);
            cam.clearFlags = CameraClearFlags.SolidColor;

            _cameraController = cam.GetComponent<SmoothCameraController>();
            if (_cameraController == null)
            {
                _cameraController = cam.gameObject.AddComponent<SmoothCameraController>();
            }

            Transform followTarget = (_activePlayerView != null) ? _activePlayerView.transform : transform;
            _cameraController.Initialize(followTarget, 6.0f, maxZoomMultiplier, cameraSmoothTime);
        }

        private void SetupLevelManager()
        {
            if (levelManager == null)
            {
                levelManager = GetComponent<LevelManager>() ?? gameObject.AddComponent<LevelManager>();
            }

            levelManager.Initialize(_world, _playerEntity, _activePlayerView, _cameraController);
        }

        private void SetupUI()
        {
            // 确保全局背包管理器与快捷栏数据就绪
            var inv = Portal2D.Presentation.Inventory.InventoryManager.Instance;

            // 确保场景中的 Canvas 挂载有 UIManager 并正常驱动事件路由
            var existingCanvas = FindAnyObjectByType<Canvas>();
            if (existingCanvas != null)
            {
                if (existingCanvas.GetComponent<Portal2D.Presentation.UI.Components.UIManager>() == null)
                {
                    existingCanvas.gameObject.AddComponent<Portal2D.Presentation.UI.Components.UIManager>();
                }
            }
            else
            {
                // 若场景尚未创建 Canvas，确保全局 UIManager 单例就绪
                var mgr = Portal2D.Presentation.UI.Components.UIManager.Instance;
            }
        }
    }
}
