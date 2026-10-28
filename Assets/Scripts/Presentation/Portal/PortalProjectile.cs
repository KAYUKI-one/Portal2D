using Portal2D.Core.Constants;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using UnityEngine;

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 传送枪发射的能量弹丸 (Portal Projectile)
    /// 沿抛物线轨迹飞行并在撞击静态墙体或地面时精确在预瞄吸附点部署传送门
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class PortalProjectile : MonoBehaviour
    {
        private PortalType _portalType = PortalType.Blue;
        private Vector2 _deployPos = Vector2.zero;
        private Vector2 _deployNormal = Vector2.up;
        private Rigidbody2D _rb;
        private SpriteRenderer _sr;
        private bool _hasImpacted = false;

        public static PortalProjectile Spawn(Vector2 origin, Vector2 velocity, float gravityScale, PortalType type, Vector2 deployPos = default, Vector2 deployNormal = default, Transform parent = null)
        {
            var go = new GameObject($"[PortalProjectile_{type}]");
            if (parent != null) go.transform.SetParent(parent, true);
            go.transform.position = origin;

            var proj = go.AddComponent<PortalProjectile>();
            proj.Initialize(velocity, gravityScale, type, deployPos, deployNormal);
            return proj;
        }

        public void Initialize(Vector2 velocity, float gravityScale, PortalType type, Vector2 deployPos, Vector2 deployNormal)
        {
            _portalType = type;
            _deployPos = deployPos;
            _deployNormal = deployNormal;

            _rb = GetComponent<Rigidbody2D>();
            _sr = gameObject.AddComponent<SpriteRenderer>();

            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = gravityScale;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = velocity;
#else
            _rb.velocity = velocity;
#endif

            var col = GetComponent<CircleCollider2D>();
            col.radius = 0.15f;
            col.isTrigger = false;

            // 美术视觉：微型发光能量核心
            var dotSprite = Resources.Load<Sprite>("Portal2DItem/TrajectoryDot");
#if UNITY_EDITOR
            if (dotSprite == null) dotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resource/Portal2DItem/TrajectoryDot.png");
#endif
            _sr.sprite = dotSprite;
            _sr.color = (_portalType == PortalType.Blue) ? new Color(0.1f, 0.88f, 1f, 1f) : new Color(1f, 0.45f, 0.1f, 1f);
            _sr.sortingLayerName = "Item";
            _sr.sortingOrder = 20;
            transform.localScale = Vector3.one * 0.45f;

            // 5 秒后若未命中自动销毁
            Destroy(gameObject, 5f);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_hasImpacted) return;

            // 严禁附着在可移动物块与交互机关 (压力板、开关等) 上
            bool isInvalid = collision.collider.GetComponentInParent<PushableBlockEntity>() != null ||
                             collision.collider.GetComponentInParent<PuzzleGimmickBase>() != null ||
                             collision.collider.gameObject.layer == GameLayers.FrontGroundLayer ||
                             collision.collider.CompareTag("Gimmick") ||
                             collision.collider.name.ToLower().Contains("button") ||
                             collision.collider.name.ToLower().Contains("plate") ||
                             collision.collider.name.ToLower().Contains("switch");

            if (isInvalid)
            {
                return;
            }

            _hasImpacted = true;
            // 精确在虚影吸附对齐的世界坐标与法线部署传送门
            PortalManager.Instance.DeployPortal(_portalType, _deployPos, _deployNormal);
            Destroy(gameObject);
        }
    }
}
