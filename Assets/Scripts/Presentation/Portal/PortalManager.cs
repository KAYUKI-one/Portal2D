using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 全局传送门成对管理器 (Portal Manager)
    /// 确保场景中严格同时仅存在一组传送门 (一个蓝色，一个红色)
    /// </summary>
    public class PortalManager : MonoBehaviour
    {
        private static PortalManager _instance;
        public static PortalManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<PortalManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[PortalManager]");
                        _instance = go.AddComponent<PortalManager>();
                    }
                }
                return _instance;
            }
        }

        private PortalEntity _bluePortal;
        private PortalEntity _redPortal;

        public PortalEntity BluePortal => _bluePortal;
        public PortalEntity RedPortal => _redPortal;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnEnable()
        {
            // 保持实例常驻
        }

        private void OnDisable()
        {
        }

        /// <summary>
        /// 检查指定点/包围盒是否正位于已联通的【地面传送门】开口上方
        /// 用于让角色动力学系统和物理物块不被原地图的实心地面挡住，能自然“落入”传送门开口中
        /// </summary>
        public bool IsPointOverFloorPortal(Vector2 point, Vector2 size, out PortalEntity floorPortal)
        {
            floorPortal = null;
            if (_bluePortal != null && _bluePortal.IsLinked && _bluePortal.IsFloorPortal)
            {
                float left = _bluePortal.transform.position.x - _bluePortal.HalfSpan - 0.1f;
                float right = _bluePortal.transform.position.x + _bluePortal.HalfSpan + 0.1f;
                if (point.x >= left && point.x <= right && Mathf.Abs(point.y - _bluePortal.transform.position.y) < 1.8f)
                {
                    floorPortal = _bluePortal;
                    return true;
                }
            }

            if (_redPortal != null && _redPortal.IsLinked && _redPortal.IsFloorPortal)
            {
                float left = _redPortal.transform.position.x - _redPortal.HalfSpan - 0.1f;
                float right = _redPortal.transform.position.x + _redPortal.HalfSpan + 0.1f;
                if (point.x >= left && point.x <= right && Mathf.Abs(point.y - _redPortal.transform.position.y) < 1.8f)
                {
                    floorPortal = _redPortal;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查水平移动是否正试图穿进某个【墙面传送门】开口
        /// </summary>
        public bool IsPointOverWallPortal(Vector2 point, Vector2 size, Vector2 moveDir, out PortalEntity wallPortal)
        {
            wallPortal = null;
            if (_bluePortal != null && _bluePortal.IsLinked && _bluePortal.IsWallPortal)
            {
                if (Vector2.Dot(moveDir, _bluePortal.FacingNormal) < -0.3f)
                {
                    float portalBottom = _bluePortal.transform.position.y - _bluePortal.HalfSpan - 0.25f;
                    float portalTop = _bluePortal.transform.position.y + _bluePortal.HalfSpan + 0.25f;
                    if (point.y >= portalBottom && point.y <= portalTop)
                    {
                        if (Mathf.Abs(point.x - _bluePortal.transform.position.x) < 0.8f)
                        {
                            wallPortal = _bluePortal;
                            return true;
                        }
                    }
                }
            }

            if (_redPortal != null && _redPortal.IsLinked && _redPortal.IsWallPortal)
            {
                if (Vector2.Dot(moveDir, _redPortal.FacingNormal) < -0.3f)
                {
                    float portalBottom = _redPortal.transform.position.y - _redPortal.HalfSpan - 0.25f;
                    float portalTop = _redPortal.transform.position.y + _redPortal.HalfSpan + 0.25f;
                    if (point.y >= portalBottom && point.y <= portalTop)
                    {
                        if (Mathf.Abs(point.x - _redPortal.transform.position.x) < 0.8f)
                        {
                            wallPortal = _redPortal;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 检查角色当前位置是否正在通过某个竖直墙面传送门
        /// 若在竖直门开口内，返回 true 并输出门洞地面的精确 Y 坐标，确保动力学系统为其提供坚实地面支撑，绝不掉入地底
        /// </summary>
        public bool IsPlayerPassingThroughWallPortal(Vector2 origin, Vector2 size, out float groundY)
        {
            groundY = 0f;

            if (_bluePortal != null && _bluePortal.IsLinked && _bluePortal.IsWallPortal)
            {
                float portalX = _bluePortal.transform.position.x;
                float portalY = _bluePortal.transform.position.y;
                float halfSpan = _bluePortal.HalfSpan;
                float floorLevel = portalY - halfSpan;

                // 水平范围：门面法线两侧各 0.95 米 (包含走入墙体与走出阶段)
                if (Mathf.Abs(origin.x - portalX) <= 0.95f)
                {
                    // 垂直范围：角色中心与脚底处于门洞开口高度
                    float playerBottom = origin.y - size.y * 0.5f;
                    if (playerBottom >= floorLevel - 0.45f && origin.y <= portalY + halfSpan + 0.45f)
                    {
                        groundY = floorLevel;
                        return true;
                    }
                }
            }

            if (_redPortal != null && _redPortal.IsLinked && _redPortal.IsWallPortal)
            {
                float portalX = _redPortal.transform.position.x;
                float portalY = _redPortal.transform.position.y;
                float halfSpan = _redPortal.HalfSpan;
                float floorLevel = portalY - halfSpan;

                if (Mathf.Abs(origin.x - portalX) <= 0.95f)
                {
                    float playerBottom = origin.y - size.y * 0.5f;
                    if (playerBottom >= floorLevel - 0.45f && origin.y <= portalY + halfSpan + 0.45f)
                    {
                        groundY = floorLevel;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 在指定世界坐标与表面法线部署传送门 (自动维护一蓝一红成对联动)
        /// </summary>
        public PortalEntity DeployPortal(PortalType type, Vector2 position, Vector2 normal)
        {
            PortalEntity target = (type == PortalType.Blue) ? _bluePortal : _redPortal;
            PortalEntity partner = (type == PortalType.Blue) ? _redPortal : _bluePortal;

            if (target == null)
            {
                var go = new GameObject(type == PortalType.Blue ? "[Portal_Blue]" : "[Portal_Red]");
                target = go.AddComponent<PortalEntity>();
                if (type == PortalType.Blue) _bluePortal = target;
                else _redPortal = target;
            }

            target.transform.position = new Vector3(position.x, position.y, 0f);
            target.gameObject.SetActive(true);

            target.Initialize(type, normal, partner);

            if (partner != null)
            {
                partner.PartnerPortal = target;
            }

            EventBus.Publish(new PortalPlacedEvent(type, position, normal));
            return target;
        }

        /// <summary>
        /// 清理场景中现存的所有传送门 (换关或重置时调用)
        /// </summary>
        public void ClearAllPortals()
        {
            if (_bluePortal != null)
            {
                if (Application.isPlaying) Destroy(_bluePortal.gameObject);
                else DestroyImmediate(_bluePortal.gameObject);
                _bluePortal = null;
            }
            if (_redPortal != null)
            {
                if (Application.isPlaying) Destroy(_redPortal.gameObject);
                else DestroyImmediate(_redPortal.gameObject);
                _redPortal = null;
            }
        }
    }
}
