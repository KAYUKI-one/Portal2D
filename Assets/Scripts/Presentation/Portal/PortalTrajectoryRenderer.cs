using System.Collections.Generic;
using UnityEngine;

namespace Portal2D.Presentation.Portal
{
    /// <summary>
    /// 白色的投掷物虚线渲染器 (White Projectile Trajectory Dot Renderer)
    /// 遵从需求：“右键会在图上显示一个白色的投掷物虚线，右键长按则会增大投掷力度”
    /// </summary>
    public class PortalTrajectoryRenderer : MonoBehaviour
    {
        [SerializeField] private Sprite dotSprite;
        [SerializeField] private float dotSpacing = 0.35f;
        [SerializeField] private float dotScale = 0.18f;

        private readonly List<GameObject> _dotPool = new List<GameObject>();
        private Transform _dotContainer;

        private void Awake()
        {
            EnsureDotSprite();
        }

        private void EnsureDotSprite()
        {
            if (dotSprite == null)
            {
                dotSprite = Resources.Load<Sprite>("Portal2DItem/TrajectoryDot");
#if UNITY_EDITOR
                if (dotSprite == null) dotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resource/Portal2DItem/TrajectoryDot.png");
#endif
            }

            if (_dotContainer == null)
            {
                var go = new GameObject("DotsContainer");
                go.transform.SetParent(transform, false);
                _dotContainer = go.transform;
            }
        }

        /// <summary>
        /// 绘制抛物线采样点序列为纯白虚线点列
        /// </summary>
        public void DrawTrajectory(List<Vector2> points)
        {
            EnsureDotSprite();

            if (points == null || points.Count < 2)
            {
                Hide();
                return;
            }

            // 根据点列计算等间距虚线点
            List<Vector2> displayDots = new List<Vector2>();
            float accumulatedDist = 0f;
            displayDots.Add(points[0]);

            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];
                float segLen = Vector2.Distance(p0, p1);

                while (accumulatedDist + segLen >= dotSpacing)
                {
                    float remain = dotSpacing - accumulatedDist;
                    float t = remain / segLen;
                    Vector2 interp = Vector2.Lerp(p0, p1, t);
                    displayDots.Add(interp);

                    p0 = interp;
                    segLen = Vector2.Distance(p0, p1);
                    accumulatedDist = 0f;
                }
                accumulatedDist += segLen;
            }

            // 激活并放置虚线点
            for (int i = 0; i < displayDots.Count; i++)
            {
                GameObject dotObj = GetOrCreateDot(i);
                dotObj.SetActive(true);
                dotObj.transform.position = new Vector3(displayDots[i].x, displayDots[i].y, 0f);

                // 虚线点越靠后略微微缩，呈现由粗到细的视觉动势
                float fade = Mathf.Lerp(1.0f, 0.6f, (float)i / displayDots.Count);
                dotObj.transform.localScale = Vector3.one * (dotScale * fade);
            }

            // 隐藏多余的池对象
            for (int i = displayDots.Count; i < _dotPool.Count; i++)
            {
                _dotPool[i].SetActive(false);
            }
        }

        public void Hide()
        {
            for (int i = 0; i < _dotPool.Count; i++)
            {
                if (_dotPool[i] != null) _dotPool[i].SetActive(false);
            }
        }

        private GameObject GetOrCreateDot(int index)
        {
            while (index >= _dotPool.Count)
            {
                var dotObj = new GameObject($"Dot_{_dotPool.Count}");
                dotObj.transform.SetParent(_dotContainer, false);
                var sr = dotObj.AddComponent<SpriteRenderer>();
                sr.sprite = dotSprite;
                sr.color = Color.white; // 纯白色虚线
                sr.sortingLayerName = "Item";
                sr.sortingOrder = 15;
                _dotPool.Add(dotObj);
            }
            return _dotPool[index];
        }
    }
}
