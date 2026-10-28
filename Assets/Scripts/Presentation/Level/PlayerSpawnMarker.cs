using UnityEngine;

namespace Portal2D.Presentation.Level
{
    /// <summary>
    /// 关卡玩家出生点可视化标记 (Player Spawn Marker)
    /// 仅在关卡编辑时使用。关卡设计师可在 Scene 视口中自由拖拽本物体以调整角色出生点。
    /// 保存到资产时将自动提取其 Transform 坐标并清理本标记。
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    [DisallowMultipleComponent]
    public class PlayerSpawnMarker : MonoBehaviour
    {
        [Header("玩家体型碰撞框参考 (用于可视化对齐地面)")]
        [SerializeField] private Vector2 previewSize = new Vector2(0.75f, 0.95f);

        public Vector2 PreviewSize
        {
            get => previewSize;
            set => previewSize = value;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Vector3 center = transform.position + new Vector3(0, previewSize.y * 0.5f, 0);

            // 绘制角色线框与站立地面基准线
            Gizmos.color = new Color(0.18f, 0.65f, 0.95f, 0.9f);
            Gizmos.DrawWireCube(center, new Vector3(previewSize.x, previewSize.y, 0));

            Gizmos.color = new Color(0.18f, 0.65f, 0.95f, 0.2f);
            Gizmos.DrawCube(center, new Vector3(previewSize.x, previewSize.y, 0));

            // 地面脚底接触十字线
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.left * 0.4f, transform.position + Vector3.right * 0.4f);

            var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.2f, 0.85f, 1f) }
            };

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * (previewSize.y + 0.35f),
                $"🚩 【起点 / Player Spawn】\n({transform.position.x:F2}, {transform.position.y:F2})",
                style
            );
        }
#endif
    }
}
