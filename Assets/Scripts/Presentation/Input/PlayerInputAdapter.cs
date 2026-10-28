using Portal2D.Core.Events;
using Portal2D.Presentation.Views;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Portal2D.Presentation.Input
{
    /// <summary>
    /// 连续 2D 输入与交互适配器：
    /// 1. 将键盘移动/跳跃输入转换为 PlayerInputAxisCommand 广播；
    ///    - A / D / 方向键控制水平移动；
    ///    - S / 下方向键控制单向平台下穿；
    ///    - W / 空格 / 上方向键作为跳跃键触发起跳；
    /// 2. 监听鼠标右键点击并广播 PlayerWorldInteractEvent，驱动场景物块通过管线通讯直接收回背包。
    /// </summary>
    public class PlayerInputAdapter : MonoBehaviour
    {
        private PlayerEntityView _cachedPlayerView;

        private void Update()
        {
            // 1. 水平轴向、下穿平台与手动跳跃输入
            float horizontal = 0f;
            bool down = false;
            bool jump = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) horizontal -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) horizontal += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) down = true;
                if (kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) jump = true;
            }
#endif

            // 旧版输入管理器兼容备用
            try
            {
                if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
                if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
                if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) down = true;
                if (UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.Space) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow)) jump = true;
            }
            catch
            {
                // Active input handler 为 New Input System 时，调用 UnityEngine.Input 会抛异常，安全忽略
            }

            EventBus.Publish(new PlayerInputAxisCommand(horizontal, down, jump));

            // 2. 鼠标右键交互：广播至全局事件管线 (直接收入背包)
            bool rightClicked = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
            }
#endif
            if (!rightClicked)
            {
                try
                {
                    rightClicked = UnityEngine.Input.GetMouseButtonDown(1);
                }
                catch
                {
                }
            }

            if (rightClicked)
            {
                // 右键在游戏中无任何 UI 按钮功能，始终直接派发世界交互，绝不被透明全屏 UI 拦截吞噬
                DispatchWorldInteract();
            }
        }

        private void DispatchWorldInteract()
        {
            var cam = Camera.main;
            if (cam == null) cam = FindAnyObjectByType<Camera>();
            if (cam == null) return;

            Vector2 mouseScreen = Vector2.zero;
            bool gotPos = false;

#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                mouseScreen = Mouse.current.position.ReadValue();
                gotPos = true;
            }
#endif
            if (!gotPos)
            {
                try
                {
                    mouseScreen = UnityEngine.Input.mousePosition;
                }
                catch
                {
                }
            }

            Vector3 worldPos3D = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -cam.transform.position.z));
            Vector2 worldPos = new Vector2(worldPos3D.x, worldPos3D.y);

            if (_cachedPlayerView == null)
            {
                _cachedPlayerView = FindAnyObjectByType<PlayerEntityView>();
            }

            Vector2 playerPos = (_cachedPlayerView != null) ? (Vector2)_cachedPlayerView.transform.position : worldPos;

            // 纯管线通讯广播：玩家在世界空间请求交互 (支持10米交互半径)
            EventBus.Publish(new PlayerWorldInteractEvent(worldPos, playerPos, 10.0f));
        }
    }
}
