using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Portal2D.Presentation.CameraControl
{
    /// <summary>
    /// 平滑跟随与限制缩放摄像机控制器
    /// 支持连续目标追踪、滚轮缩放和整个正交画面的地图边界约束。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SmoothCameraController : MonoBehaviour
    {
        [Header("--- 跟随目标配置 ---")]
        [Tooltip("跟随的目标对象 (通常为玩家角色)")]
        [SerializeField] private Transform target;

        [Tooltip("平滑阻尼时间 (数值越小跟随越紧密，推荐 0.1 ~ 0.25)")]
        [SerializeField] private float smoothTime = 0.18f;

        [Tooltip("摄像机相对于目标的偏移")]
        [SerializeField] private Vector2 targetOffset = new Vector2(0f, 0.8f);

        [Header("--- 缩放限制配置 ---")]
        [Tooltip("最小正交视野大小 (最近视角，允许缩近看清角色与贴图细节)")]
        [SerializeField] private float minOrthoSize = 2.8f;

        [Tooltip("最大正交视野大小 (拉远看全局的最大视角)")]
        [SerializeField] private float maxOrthoSize = 13.5f;

        [Tooltip("滚轮缩放速度 (每格滚轮改变的视野大小)")]
        [SerializeField] private float zoomSpeed = 1.0f;

        [Tooltip("缩放平滑过渡速度")]
        [SerializeField] private float zoomSmoothSpeed = 16.0f;

        private Camera _cam;
        private Vector3 _currentVelocity;
        private float _targetOrthoSize;
        private float _initialOrthoSize;
        private bool _confined;
        private Rect _bounds;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public float MinOrthoSize => minOrthoSize;
        public float MaxOrthoSize => maxOrthoSize;

        public void ConfigureEnvironment(bool confined, Rect bounds, Color background)
        {
            _cam = GetComponent<Camera>();
            _confined = confined && bounds.width > 0 && bounds.height > 0;
            _bounds = bounds;
            _cam.backgroundColor = background;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            ApplyViewLimit();
            transform.position = Confine(transform.position);
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam != null)
            {
                _initialOrthoSize = _cam.orthographicSize;
                minOrthoSize = Mathf.Max(2.5f, _initialOrthoSize * 0.45f);
                maxOrthoSize = _initialOrthoSize * 2.2f;
                _targetOrthoSize = _initialOrthoSize;
            }
        }

        public void Initialize(Transform followTarget, float initialSize, float maxSizeMultiplier = 2.2f, float followSmoothTime = 0.16f)
        {
            target = followTarget;
            smoothTime = followSmoothTime;
            _cam = GetComponent<Camera>();
            _initialOrthoSize = initialSize;
            minOrthoSize = Mathf.Max(2.5f, initialSize * 0.45f);
            maxOrthoSize = initialSize * maxSizeMultiplier;
            _targetOrthoSize = initialSize;
            _cam.orthographicSize = initialSize;
            ApplyViewLimit();
            _currentVelocity = Vector3.zero;
            transform.rotation = Quaternion.identity;

            if (target != null)
            {
                Vector3 targetPos = target.position + new Vector3(targetOffset.x, targetOffset.y, 0f);
                transform.position = Confine(new Vector3(targetPos.x, targetPos.y, -10f));
            }
        }

        private void LateUpdate()
        {
            transform.rotation = Quaternion.identity;
            // 1. 鼠标滚轮缩放逻辑
            HandleZoomInput();

            // 2. 平滑缩放插值
            if (_cam != null && Mathf.Abs(_cam.orthographicSize - _targetOrthoSize) > 0.001f)
            {
                _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _targetOrthoSize, 1-Mathf.Exp(-zoomSmoothSpeed * Time.deltaTime));
            }
            ApplyViewLimit();

            // 3. 平滑跟随目标
            if (target != null)
            {
                Vector3 destPos = target.position + new Vector3(targetOffset.x, targetOffset.y, 0f);
                destPos.z = -10f; // 锁定 2D 摄像机深度

                // Teleports only change the target. Keep camera position and damping velocity continuous,
                // even if another crossing arrives before the previous camera move has finished.
                destPos = Confine(destPos);
                Vector3 next = Vector3.SmoothDamp(transform.position, destPos, ref _currentVelocity, smoothTime);
                Vector3 clamped = Confine(next);
                if (!Mathf.Approximately(next.x, clamped.x)) _currentVelocity.x = 0;
                if (!Mathf.Approximately(next.y, clamped.y)) _currentVelocity.y = 0;
                transform.position = clamped;
            }
        }

        private float MaximumViewSize() => _confined
            ? Mathf.Min(maxOrthoSize, _bounds.height * 0.5f, _bounds.width / (2 * Mathf.Max(0.01f, _cam.aspect)))
            : maxOrthoSize;
        private void ApplyViewLimit()
        {
            if (_cam == null) return;
            float max = Mathf.Max(0.01f, MaximumViewSize());
            _targetOrthoSize = Mathf.Clamp(_targetOrthoSize, Mathf.Min(minOrthoSize, max), max);
            // A changed aspect ratio or a small map must never expose space outside the map.
            _cam.orthographicSize = Mathf.Min(_cam.orthographicSize, max);
        }
        private Vector3 Confine(Vector3 position)
        {
            if (!_confined || _cam == null) return position;
            float halfHeight = _cam.orthographicSize, halfWidth = halfHeight * _cam.aspect;
            position.x = Mathf.Clamp(position.x, _bounds.xMin + halfWidth, _bounds.xMax - halfWidth);
            position.y = Mathf.Clamp(position.y, _bounds.yMin + halfHeight, _bounds.yMax - halfHeight);
            return position;
        }

        private void HandleZoomInput()
        {
            float scrollDelta = 0f;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                scrollDelta = mouse.scroll.ReadValue().y;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Mathf.Abs(scrollDelta) < 0.01f)
            {
                scrollDelta = UnityEngine.Input.mouseScrollDelta.y * 120f;
            }
#endif

            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                // 标准化鼠标滚轮步长 (通常单格为 120，部分设备为 1)
                float normalizedScroll = Mathf.Abs(scrollDelta) >= 10f ? (scrollDelta / 120f) : Mathf.Sign(scrollDelta);

                // 向上滚缩小视野 (数值变小，拉近放大角色)，向下滚放大视野 (数值变大，拉远看全景)
                float zoomChange = -normalizedScroll * zoomSpeed;
                float max = Mathf.Max(0.01f, MaximumViewSize());
                _targetOrthoSize = Mathf.Clamp(_targetOrthoSize + zoomChange, Mathf.Min(minOrthoSize, max), max);
            }
        }
    }
}
