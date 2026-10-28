using UnityEngine;

namespace Portal2D.Presentation.Level
{
    /// <summary>
    /// 关卡过关终点可视化标记 (Level Goal Exit Marker)
    /// 仅在关卡编辑时使用。关卡设计师可在 Scene 视口中自由拖拽本物体以调整通关触发区域。
    /// 保存到资产时将自动提取其 Transform 坐标并清理本标记。
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class LevelGoalMarker : MonoBehaviour
    {
        [Header("过关触发感应范围 (宽, 高)")]
        [SerializeField] private Vector2 triggerSize = new Vector2(1.5f, 2.5f);

        public Vector2 TriggerSize
        {
            get => triggerSize;
            set => triggerSize = value;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 绘制绿色发光过关检测区域线框与发光半透明体
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireCube(transform.position, new Vector3(triggerSize.x, triggerSize.y, 0));

            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.15f);
            Gizmos.DrawCube(transform.position, new Vector3(triggerSize.x, triggerSize.y, 0));

            // 地面基准线
            Gizmos.color = new Color(0.2f, 1f, 0.5f, 0.6f);
            float bottomY = transform.position.y - triggerSize.y * 0.5f;
            Gizmos.DrawLine(new Vector3(transform.position.x - triggerSize.x * 0.5f, bottomY, 0),
                             new Vector3(transform.position.x + triggerSize.x * 0.5f, bottomY, 0));

            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 1f, 0.5f) }
            };

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (triggerSize.y * 0.5f + 0.35f),
                $"🚪 【终点 / Goal Exit】\n({transform.position.x:F2}, {transform.position.y:F2})",
                style
            );
        }
#endif
    }
}
