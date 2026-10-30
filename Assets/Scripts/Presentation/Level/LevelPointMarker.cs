using UnityEngine;
namespace Portal2D.Presentation.Level
{
    public sealed class LevelPointMarker : MonoBehaviour
    {
        public Color Color = Color.green;
        private void OnDrawGizmos() { Gizmos.color = Color; Gizmos.DrawWireCube(transform.position, new Vector3(0.65f, 1.25f, 0)); }
    }
}
