using Portal2D.Core.Events;
using Portal2D.Presentation.Inventory;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 角色手持传送枪视觉表现组件 (Equipped Portal Gun View)
    /// 1. 美术无缝契合：动态贴合在角色手腕/腰腹前方，解决角色序列帧动画与手持物体的脱节问题；
    /// 2. 鼠标朝向准星联动：传送枪自动指向鼠标光标，并在角色左/右翻转时自适应 Flip 矫正姿态；
    /// 3. 槽位状态监听：仅当玩家手持第一个格子（传送枪）时显现，切换其他槽位时自动隐藏；
    /// 4. 枪口发光核心色彩联动：发射蓝门时光晕为天青色，发射红门时光晕为赤橙色。
    /// </summary>
    public class PortalGunEquippedView : MonoBehaviour
    {
        [Header("--- 枪体位置偏移与旋转配置 ---")]
        [Tooltip("枪身相对于角色根节点的局部坐标偏置")]
        [SerializeField] private Vector3 localOffset = new Vector3(0.18f, -0.05f, 0f);

        [Tooltip("上下仰角/俯角的最大旋转角度限制")]
        [SerializeField] private float maxAimAngle = 75f;

        [Header("--- 美术引用 ---")]
        [SerializeField] private Sprite portalGunSprite;
        [SerializeField] private SpriteRenderer gunSpriteRenderer;

        private Transform _gunTransform;
        private Transform _muzzleTransform;
        private bool _isEquipped = false;
        private PortalType _currentPortalType = PortalType.Blue;

        public Transform MuzzleTransform => _muzzleTransform != null ? _muzzleTransform : _gunTransform;
        public Vector2 MuzzlePosition => MuzzleTransform != null ? (Vector2)MuzzleTransform.position : (Vector2)transform.position;
        public bool IsEquipped => _isEquipped;

        private void Awake()
        {
            EnsureGunHierarchy();
        }

        private void OnEnable()
        {
            EventBus.Unsubscribe<InventorySlotSelectedEvent>(OnInventorySlotSelected);
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);

            EventBus.Subscribe<InventorySlotSelectedEvent>(OnInventorySlotSelected);
            EventBus.Subscribe<InventoryChangedEvent>(OnInventoryChanged);

            CheckEquippedState();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InventorySlotSelectedEvent>(OnInventorySlotSelected);
            EventBus.Unsubscribe<InventoryChangedEvent>(OnInventoryChanged);
        }

        private void EnsureGunHierarchy()
        {
            if (_gunTransform == null)
            {
                var existing = transform.Find("[HeldPortalGun]");
                if (existing != null)
                {
                    _gunTransform = existing;
                }
                else
                {
                    var gunObj = new GameObject("[HeldPortalGun]");
                    gunObj.transform.SetParent(transform, false);
                    gunObj.transform.localPosition = localOffset;
                    _gunTransform = gunObj.transform;
                }
            }

            if (gunSpriteRenderer == null)
            {
                gunSpriteRenderer = _gunTransform.GetComponent<SpriteRenderer>();
                if (gunSpriteRenderer == null) gunSpriteRenderer = _gunTransform.gameObject.AddComponent<SpriteRenderer>();
            }

            if (portalGunSprite == null)
            {
                portalGunSprite = Resources.Load<Sprite>("Portal2DItem/PortalGun");
#if UNITY_EDITOR
                if (portalGunSprite == null) portalGunSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resource/Portal2DItem/PortalGun.png");
#endif
            }

            gunSpriteRenderer.sprite = portalGunSprite;
            gunSpriteRenderer.sortingLayerName = "Item";
            gunSpriteRenderer.sortingOrder = 12; // 渲染在角色服装最上层，自然贴合手部

            // 创建枪口发射定位点
            if (_muzzleTransform == null)
            {
                var muzzle = _gunTransform.Find("Muzzle");
                if (muzzle == null)
                {
                    var muzzleObj = new GameObject("Muzzle");
                    muzzleObj.transform.SetParent(_gunTransform, false);
                    muzzleObj.transform.localPosition = new Vector3(0.72f, 0.08f, 0f);
                    _muzzleTransform = muzzleObj.transform;
                }
                else
                {
                    _muzzleTransform = muzzle;
                }
            }
        }

        private void OnInventorySlotSelected(InventorySlotSelectedEvent evt)
        {
            CheckEquippedState();
        }

        private void OnInventoryChanged(InventoryChangedEvent evt)
        {
            CheckEquippedState();
        }

        private void CheckEquippedState()
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return;

            var item = inv.GetSelectedItem();
            bool shouldEquip = (item != null && item.id == "PortalGun");

            _isEquipped = shouldEquip;
            if (_gunTransform != null)
            {
                _gunTransform.gameObject.SetActive(_isEquipped);
            }
        }

        public void SetAimingPortalType(PortalType type)
        {
            _currentPortalType = type;
        }

        private void LateUpdate()
        {
            if (!_isEquipped || _gunTransform == null) return;

            // 获取鼠标在世界空间的位置
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 mouseScreen = Vector3.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) mouseScreen = Mouse.current.position.ReadValue();
#endif
            if (mouseScreen == Vector3.zero)
            {
                try { mouseScreen = UnityEngine.Input.mousePosition; } catch { }
            }

            Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
            Vector2 aimDir = (mouseWorld - _gunTransform.position);
            if (aimDir.sqrMagnitude < 0.001f) aimDir = Vector2.right;

            // 保持枪身在手部基准点 (轻微跟随呼吸微调)
            _gunTransform.localPosition = localOffset;

            // 判定角色面向：localScale.x < 0 为向左，> 0 为向右
            bool isFacingLeft = transform.localScale.x < 0;

            float worldAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;

            if (!isFacingLeft)
            {
                // 朝右：限制仰角在 [-maxAimAngle, maxAimAngle]
                float clampedAngle = Mathf.Clamp(worldAngle, -maxAimAngle, maxAimAngle);
                _gunTransform.rotation = Quaternion.Euler(0, 0, clampedAngle);
                gunSpriteRenderer.flipY = false;
            }
            else
            {
                // 朝左：以 180° 为基准偏角，并翻转 Y 轴，保持握把向下枪口向外
                float delta = Mathf.DeltaAngle(180f, worldAngle);
                float clampedDelta = Mathf.Clamp(delta, -maxAimAngle, maxAimAngle);
                _gunTransform.rotation = Quaternion.Euler(0, 0, 180f + clampedDelta);
                gunSpriteRenderer.flipY = true;
            }
        }
    }
}
