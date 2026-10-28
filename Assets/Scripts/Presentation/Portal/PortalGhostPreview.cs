using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 瞄准落点半透明虚影预览门 (Portal Ghost Preview)
    /// 1. 严格使用用户资源：Assets/Resource/Portal/ 传送门红.png 与 传送门蓝.png；
    /// 2. 贴合表面吸附：地上水平吸附 (旋转90°)，墙面竖直吸附 (旋转0°)；
    /// 3. 比真门稍暗淡半透明 (alpha = 0.45)。
    /// </summary>
    public class PortalGhostPreview : MonoBehaviour
    {
        private SpriteRenderer _sr;
        private static Sprite _blueSprite;
        private static Sprite _redSprite;

        private void Awake()
        {
            EnsureSpriteRenderer();
        }

        private void EnsureSpriteRenderer()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();

            _sr.sortingLayerName = "Item";
            _sr.sortingOrder = 9;

            LoadSprites();
        }

        private static void LoadSprites()
        {
            if (_blueSprite == null)
            {
#if UNITY_EDITOR
                var blueAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resource/Portal/传送门蓝.png");
                foreach (var obj in blueAssets)
                {
                    if (obj is Sprite s) { _blueSprite = s; break; }
                }
#endif
                if (_blueSprite == null) _blueSprite = Resources.Load<Sprite>("Portal/传送门蓝");
            }

            if (_redSprite == null)
            {
#if UNITY_EDITOR
                var redAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Resource/Portal/传送门红.png");
                foreach (var obj in redAssets)
                {
                    if (obj is Sprite s) { _redSprite = s; break; }
                }
#endif
                if (_redSprite == null) _redSprite = Resources.Load<Sprite>("Portal/传送门红");
            }
        }

        /// <summary>
        /// 更新虚影位置、姿态吸附与半透明色彩
        /// </summary>
        public void UpdatePreview(Vector2 position, Vector2 normal, PortalType type, bool isVisible)
        {
            EnsureSpriteRenderer();

            if (!isVisible)
            {
                _sr.enabled = false;
                return;
            }

            _sr.enabled = true;
            // 贴合吸附于表面微小外沿，避免被地图深度剔除
            transform.position = new Vector3(position.x + normal.x * 0.04f, position.y + normal.y * 0.04f, 0f);
            // 尺寸缩减：刚好比角色略高一点 (约 2.95 米)
            transform.localScale = new Vector3(0.75f, 0.75f, 1f);

            // 表面贴合旋转：地面/天花板水平贴合(90°)，墙壁竖直贴合(0°)
            bool isHorizontalSurface = Mathf.Abs(normal.y) > Mathf.Abs(normal.x);
            transform.rotation = Quaternion.Euler(0, 0, isHorizontalSurface ? 90f : 0f);

            _sr.sprite = (type == PortalType.Blue) ? _blueSprite : _redSprite;
            // 比真门稍暗淡且半透明
            _sr.color = new Color(0.85f, 0.85f, 0.85f, 0.45f);
        }

        public void Hide()
        {
            if (_sr != null) _sr.enabled = false;
        }
    }
}
