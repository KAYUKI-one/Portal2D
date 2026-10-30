#if UNITY_EDITOR
using Portal2D.Core.Data;
using UnityEngine;
namespace Portal2D.Development
{
    public sealed class LevelObjectConfigProbe : MonoBehaviour, ILevelObjectState
    {
        public float Speed=2;
        public Vector2 Endpoint=new(3,0);
        public string CaptureLevelState() => JsonUtility.ToJson(this);
        public void RestoreLevelState(string state) => JsonUtility.FromJsonOverwrite(state,this);
    }
}
#endif
