using System.Collections.Generic;
using Portal2D.Core.Constants;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Inventory;
using Portal2D.Presentation.Views;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 传送枪发射与智能弹道瞄准控制器 (Portal Gun Controller)
    /// 1. 优先指向落点：优先考虑鼠标指向的有效平面落点，自动逆解计算符合力度的弹道抛物线虚线；
    /// 2. 平面吸附与严格过滤：
    ///    - 射线触碰有效平面时，虚影门与生成的传送门紧密贴合吸附（地面水平，墙面竖直）；
    ///    - 绝对拦截压力板 (FloorButton)、墙壁开关、机关等交互物品，以及所有可移动物块；
    /// 3. 即时响应发射：告别漫长的长按蓄力，所指即所打，支持左键射蓝门、右键射红门或按键切换。
    /// </summary>
    public class PortalGunController : MonoBehaviour
    {
        [Tooltip("弹丸重力系数 (与物理系统重力加速度匹配)")]
        [SerializeField] private float projectileGravityScale = 2.0f;

        [Header("--- 组件引用 (若为空则自动构建) ---")]
        [SerializeField] private PortalGunEquippedView gunView;
        [SerializeField] private PortalTrajectoryRenderer trajectoryRenderer;
        [SerializeField] private PortalGhostPreview ghostPreview;

        private PortalType _activePortalType = PortalType.Blue;

        // 轨迹模拟缓存
        private readonly List<Vector2> _trajectoryPoints = new List<Vector2>(64);
        private bool _hasValidTarget = false;
        private Vector2 _targetHitPoint = Vector2.zero;
        private Vector2 _targetNormal = Vector2.up;
        private Vector2 _calculatedLaunchVelocity = Vector2.zero;

        private void Awake()
        {
            EnsureDependencies();
        }

        public void EnsureDependencies()
        {
            if (gunView == null) gunView = GetComponentInChildren<PortalGunEquippedView>();
            if (gunView == null)
            {
                var player = FindAnyObjectByType<PlayerEntityView>();
                if (player != null)
                {
                    gunView = player.gameObject.GetComponent<PortalGunEquippedView>() ?? player.gameObject.AddComponent<PortalGunEquippedView>();
                }
            }

            if (trajectoryRenderer == null)
            {
                var trObj = new GameObject("[PortalTrajectoryRenderer]");
                trObj.transform.SetParent(transform, false);
                trajectoryRenderer = trObj.AddComponent<PortalTrajectoryRenderer>();
            }

            if (ghostPreview == null)
            {
                var gpObj = new GameObject("[PortalGhostPreview]");
                gpObj.transform.SetParent(transform, false);
                ghostPreview = gpObj.AddComponent<PortalGhostPreview>();
                ghostPreview.Hide();
            }
        }

        private void Update()
        {
            var inv = InventoryManager.Instance;
            if (inv == null) return;

            var selected = inv.GetSelectedItem();
            bool isHoldingGun = (selected != null && selected.id == "PortalGun");

            if (!isHoldingGun)
            {
                HideAimVisuals();
                return;
            }

            HandleColorSwitchInput();
            UpdateAimTrajectory();
            HandleFireInput();
        }

        private void HandleColorSwitchInput()
        {
            bool switchKey = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                switchKey = Keyboard.current.qKey.wasPressedThisFrame ||
                            Keyboard.current.eKey.wasPressedThisFrame ||
                            Keyboard.current.rKey.wasPressedThisFrame;
            }
#endif
            if (!switchKey)
            {
                try
                {
                    if (UnityEngine.Input.GetKeyDown(KeyCode.Q) || UnityEngine.Input.GetKeyDown(KeyCode.E) || UnityEngine.Input.GetKeyDown(KeyCode.R))
                        switchKey = true;
                }
                catch { }
            }

            if (switchKey)
            {
                _activePortalType = (_activePortalType == PortalType.Blue) ? PortalType.Red : PortalType.Blue;
                if (gunView != null) gunView.SetAimingPortalType(_activePortalType);
            }
        }

        /// <summary>
        /// 遵从需求：“优先考虑鼠标指向的落点，然后根据指向自动生成符合力度的投掷物虚线”
        /// </summary>
        private void UpdateAimTrajectory()
        {
            // 排除鼠标悬停在 UI 上的情况
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HideAimVisuals();
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            Vector2 origin = (gunView != null) ? gunView.MuzzlePosition : (Vector2)transform.position;

            Vector3 mouseScreen = Vector3.zero;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null) mouseScreen = Mouse.current.position.ReadValue();
#endif
            if (mouseScreen == Vector3.zero)
            {
                try { mouseScreen = UnityEngine.Input.mousePosition; } catch { }
            }

            Vector3 mouseWorld = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
            Vector2 toMouse = ((Vector2)mouseWorld - origin);
            if (toMouse.sqrMagnitude < 0.001f) toMouse = Vector2.right;

            // 1. 优先探测鼠标指向落点：沿视线发射探测射线寻找首个非触发器有效碰撞表面
            int solidMask = GameLayers.SolidMask | GameLayers.FrontGroundMask;
            RaycastHit2D hit = default;
            var initialHits = Physics2D.RaycastAll(origin, toMouse.normalized, 50f, solidMask);
            for (int i = 0; i < initialHits.Length; i++)
            {
                if (initialHits[i].collider != null && !initialHits[i].collider.isTrigger)
                {
                    hit = initialHits[i];
                    break;
                }
            }

            // 若鼠标正直接悬停在一个碰撞体上方，优先使用悬停接触面
            var hoverCollider = Physics2D.OverlapPoint(mouseWorld, solidMask);
            if (hoverCollider != null && !hoverCollider.isTrigger)
            {
                Vector2 closest = hoverCollider.ClosestPoint(origin);
                var directHits = Physics2D.LinecastAll(origin, closest, solidMask);
                for (int i = 0; i < directHits.Length; i++)
                {
                    if (directHits[i].collider != null && !directHits[i].collider.isTrigger)
                    {
                        hit = directHits[i];
                        break;
                    }
                }
            }

            _hasValidTarget = false;

            if (hit.collider != null)
            {
                // 严禁吸附于：可移动物块、压力板/地面开关、墙壁拉杆、机关
                if (!IsInvalidSurfaceCollider(hit.collider))
                {
                    // 对撞击点进行表面吸附与合规性校验：
                    // 1. 传送门高度缩小后长度约为 2.96 米
                    // 2. 严禁两端悬空暴露于空气中 (有一侧必须完全贴合在墙壁/地面)
                    // 3. 若落点靠近悬崖边缘，自动沿切线向内吸附对齐到边缘，消除悬空切面
                    // 4. 若遇垂直障碍物(如内拐角墙体)，自动向外退让吸附，防止穿墙
                    // 5. 若表面连续实体长度不足以容纳整个传送门 (如 1 格或 2 格方块)，拒绝放置
                    if (SnapAndValidateSurface(hit.point, hit.normal, solidMask, out Vector2 snappedPoint, out Vector2 snappedNormal))
                    {
                        _hasValidTarget = true;
                        _targetHitPoint = snappedPoint;
                        _targetNormal = snappedNormal;
                    }
                }
            }

            if (!_hasValidTarget)
            {
                HideAimVisuals();
                return;
            }

            // 2. 根据目标落点逆解初速度 (自动生成完全吻合落点力度的抛物线)
            Vector2 gravity = Physics2D.gravity * projectileGravityScale;
            if (gravity == Vector2.zero) gravity = new Vector2(0, -9.81f * projectileGravityScale);

            float deltaX = _targetHitPoint.x - origin.x;
            float deltaY = _targetHitPoint.y - origin.y;
            float hDist = Mathf.Abs(deltaX);

            // 动态飞行时间
            float flightTime = Mathf.Clamp(Mathf.Sqrt(hDist * 0.1f + Mathf.Max(0, deltaY) * 0.08f + 0.12f), 0.25f, 0.85f);

            float vx = deltaX / flightTime;
            float vy = (deltaY - 0.5f * gravity.y * flightTime * flightTime) / flightTime;
            _calculatedLaunchVelocity = new Vector2(vx, vy);

            // 3. 构建虚线点列
            _trajectoryPoints.Clear();
            int steps = 28;
            for (int i = 0; i <= steps; i++)
            {
                float t = (flightTime / steps) * i;
                Vector2 pt = origin + _calculatedLaunchVelocity * t + 0.5f * gravity * (t * t);
                _trajectoryPoints.Add(pt);
            }

            // 4. 实时绘制虚线与落点吸附虚影门
            if (trajectoryRenderer != null)
            {
                trajectoryRenderer.DrawTrajectory(_trajectoryPoints);
            }

            if (ghostPreview != null)
            {
                ghostPreview.UpdatePreview(_targetHitPoint, _targetNormal, _activePortalType, true);
            }
        }

        private void HandleFireInput()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            bool leftClick = false;
            bool rightClick = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                leftClick = Mouse.current.leftButton.wasPressedThisFrame;
                rightClick = Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif
            if (!leftClick && !rightClick)
            {
                try
                {
                    leftClick = UnityEngine.Input.GetMouseButtonDown(0);
                    rightClick = UnityEngine.Input.GetMouseButtonDown(1);
                }
                catch { }
            }

            if (leftClick || rightClick)
            {
                if (!_hasValidTarget) return;

                // 左键发射蓝门，右键发射红门 (若用户用右键连续发射也可智能补位)
                PortalType fireType;
                if (leftClick)
                {
                    fireType = PortalType.Blue;
                }
                else
                {
                    // 右键点击
                    var pm = PortalManager.Instance;
                    if (pm.BluePortal != null && pm.RedPortal == null)
                    {
                        fireType = PortalType.Red;
                    }
                    else if (pm.BluePortal == null && pm.RedPortal != null)
                    {
                        fireType = PortalType.Blue;
                    }
                    else
                    {
                        fireType = _activePortalType;
                    }
                }

                FirePortal(fireType);
            }
        }

        private void FirePortal(PortalType type)
        {
            Vector2 origin = (gunView != null) ? gunView.MuzzlePosition : (Vector2)transform.position;

            // 立即发射贴合弹道的物理弹丸，附带落点吸附目标
            PortalProjectile.Spawn(origin, _calculatedLaunchVelocity, projectileGravityScale, type, _targetHitPoint, _targetNormal);

            // 自动交替下次预备颜色
            _activePortalType = (type == PortalType.Blue) ? PortalType.Red : PortalType.Blue;
            if (gunView != null) gunView.SetAimingPortalType(_activePortalType);
        }

        /// <summary>
        /// 检验并贴合表面吸附逻辑：
        /// 1. 传送门缩放后长度约为 2.96 米 (半长约 1.48 米)
        /// 2. 严禁两端悬空暴露于空气中 (有一侧必须完全贴合在墙壁/地面)
        /// 3. 若落点靠近悬崖边缘，自动沿切线向内吸附对齐到边缘，消除悬空切面
        /// 4. 若遇垂直障碍物(如内拐角墙体)，自动向外退让吸附，防止穿墙
        /// 5. 若表面连续实体长度不足以容纳整个传送门 (如 1 格或 2 格方块)，拒绝放置
        /// </summary>
        private static bool SnapAndValidateSurface(Vector2 rawHit, Vector2 normal, int solidMask, out Vector2 snappedPoint, out Vector2 snappedNormal)
        {
            snappedPoint = rawHit;
            snappedNormal = normal;

            // 1. 标准化表面法线与切线
            Vector2 tangent;
            if (Mathf.Abs(normal.y) >= Mathf.Abs(normal.x))
            {
                snappedNormal = new Vector2(0f, Mathf.Sign(normal.y));
                tangent = new Vector2(1f, 0f);
            }
            else
            {
                snappedNormal = new Vector2(Mathf.Sign(normal.x), 0f);
                tangent = new Vector2(0f, 1f);
            }

            const float HALF_PORTAL_LENGTH = 1.48125f;
            const float MIN_REQUIRED_SPAN = 2.80f; // 至少需要 2.8 米连续实体表面 (拒绝 1 格/2 格小方块)
            const float MAX_PROBE_DIST = 3.6f;     // 最大探测延伸距离
            const float STEP_SIZE = 0.08f;         // 步进探测步长

            // 2. 正向切线 (+tangent) 探测最大可用延伸长度
            float maxPositive = MAX_PROBE_DIST;
            // A. 空中垂直障碍物探测 (例如地面上的垂直立墙拐角)
            var obstHitsPos = Physics2D.RaycastAll(rawHit + snappedNormal * 0.1f, tangent, MAX_PROBE_DIST, solidMask);
            for (int i = 0; i < obstHitsPos.Length; i++)
            {
                var h = obstHitsPos[i];
                if (h.collider != null && !h.collider.isTrigger && !IsInvalidSurfaceCollider(h.collider))
                {
                    maxPositive = Mathf.Max(0f, h.distance - 0.05f);
                    break;
                }
            }
            // B. 实体底衬探测 (检测悬崖掉落边缘或空洞)
            for (float s = 0.05f; s <= maxPositive; s += STEP_SIZE)
            {
                Vector2 probePos = rawHit + tangent * s + snappedNormal * 0.05f;
                var backHits = Physics2D.RaycastAll(probePos, -snappedNormal, 0.25f, solidMask);
                bool hasSolidBacking = false;
                for (int b = 0; b < backHits.Length; b++)
                {
                    var bh = backHits[b];
                    if (bh.collider != null && !bh.collider.isTrigger && !IsInvalidSurfaceCollider(bh.collider))
                    {
                        hasSolidBacking = true;
                        break;
                    }
                }
                if (!hasSolidBacking)
                {
                    maxPositive = Mathf.Max(0f, s - STEP_SIZE * 0.5f);
                    break;
                }
            }

            // 3. 负向切线 (-tangent) 探测最大可用延伸长度
            float maxNegative = MAX_PROBE_DIST;
            var obstHitsNeg = Physics2D.RaycastAll(rawHit + snappedNormal * 0.1f, -tangent, MAX_PROBE_DIST, solidMask);
            for (int i = 0; i < obstHitsNeg.Length; i++)
            {
                var h = obstHitsNeg[i];
                if (h.collider != null && !h.collider.isTrigger && !IsInvalidSurfaceCollider(h.collider))
                {
                    // 若是竖直墙面向下探测地面，直接精确对齐至地面表面 (杜绝底部留缝浮空)
                    bool isWallProbingDown = Mathf.Abs(snappedNormal.x) > 0.5f;
                    maxNegative = Mathf.Max(0f, isWallProbingDown ? h.distance : h.distance - 0.05f);
                    break;
                }
            }
            for (float s = 0.05f; s <= maxNegative; s += STEP_SIZE)
            {
                Vector2 probePos = rawHit - tangent * s + snappedNormal * 0.05f;
                var backHits = Physics2D.RaycastAll(probePos, -snappedNormal, 0.25f, solidMask);
                bool hasSolidBacking = false;
                for (int b = 0; b < backHits.Length; b++)
                {
                    var bh = backHits[b];
                    if (bh.collider != null && !bh.collider.isTrigger && !IsInvalidSurfaceCollider(bh.collider))
                    {
                        hasSolidBacking = true;
                        break;
                    }
                }
                if (!hasSolidBacking)
                {
                    maxNegative = Mathf.Max(0f, s - STEP_SIZE * 0.5f);
                    break;
                }
            }

            // 4. 表面可用连续跨度校验
            float totalSpan = maxPositive + maxNegative;
            if (totalSpan < MIN_REQUIRED_SPAN)
            {
                // 表面太短 (例如 1 格方块)，无法放置完整的传送门
                return false;
            }

            // 5. 计算向内吸附偏移量：确保传送门两端完全落在有效实体表面内，杜绝悬空与穿墙
            float minOffset = HALF_PORTAL_LENGTH - maxNegative;
            float maxOffset = maxPositive - HALF_PORTAL_LENGTH;

            if (minOffset > maxOffset)
            {
                return false;
            }

            float targetOffset = 0f;

            // 优先贴合转角需求 (Corner Snapping)：
            // A. 竖直墙面 (Wall)：优先向下吸附贴合地面转角，让传送门底部紧贴地面，方便角色平滑走入走出的门洞
            if (Mathf.Abs(snappedNormal.x) > 0.5f)
            {
                // 若下方有地面，且落点在地面上方 2.6 米内，优先贴合地面转角
                if (maxNegative <= HALF_PORTAL_LENGTH + 1.2f)
                {
                    targetOffset = minOffset; // 底部精确贴合地面
                }
                else if (maxPositive <= HALF_PORTAL_LENGTH + 0.6f)
                {
                    targetOffset = maxOffset; // 贴合天花板转角
                }
            }
            // B. 水平地面 (Floor)：若落点靠近左右立墙拐角，优先贴合拐角
            else
            {
                if (maxNegative <= HALF_PORTAL_LENGTH + 0.8f && maxNegative < maxPositive)
                {
                    targetOffset = minOffset; // 贴合左/负向拐角
                }
                else if (maxPositive <= HALF_PORTAL_LENGTH + 0.8f && maxPositive < maxNegative)
                {
                    targetOffset = maxOffset; // 贴合右/正向拐角
                }
            }

            float clampedOffset = Mathf.Clamp(targetOffset, minOffset, maxOffset);
            snappedPoint = rawHit + tangent * clampedOffset;
            return true;
        }

        private static bool IsInvalidSurfaceCollider(Collider2D col)
        {
            if (col == null) return true;
            if (col.isTrigger) return true;
            if (col.GetComponentInParent<PushableBlockEntity>() != null) return true;
            if (col.GetComponentInParent<PuzzleGimmickBase>() != null) return true;
            if (col.gameObject.layer == GameLayers.FrontGroundLayer) return true;
            if (col.CompareTag("Gimmick")) return true;
            string name = col.name.ToLower();
            if (name.Contains("button") || name.Contains("plate") || name.Contains("switch")) return true;
            return false;
        }

        private void HideAimVisuals()
        {
            _hasValidTarget = false;
            if (trajectoryRenderer != null) trajectoryRenderer.Hide();
            if (ghostPreview != null) ghostPreview.Hide();
        }
    }
}
