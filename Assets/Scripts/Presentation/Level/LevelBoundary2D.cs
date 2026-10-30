using Portal2D.Core.Constants;
using Portal2D.Core.Data;
using UnityEngine;

namespace Portal2D.Presentation.Level
{
    // Derived from map data, like the background. All physical bodies use the same solid walls.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class LevelBoundary2D : MonoBehaviour
    {
        private const float Thickness = 2f;
        [SerializeField] private LevelAsset level;
        [SerializeField, HideInInspector] private BoxCollider2D[] walls;
        private Rect appliedRect;
        private bool appliedEnabled, initialized;

        public static LevelBoundary2D Ensure(GameObject root, LevelAsset source)
        {
            var boundary=root.GetComponentInChildren<LevelBoundary2D>(true);
            if(boundary==null)
            {
                var go=new GameObject("[Level boundary]"); go.transform.SetParent(root.transform,false);
                boundary=go.AddComponent<LevelBoundary2D>();
            }
            boundary.level=source; boundary.Refresh(); return boundary;
        }

        private void OnEnable() { initialized=false; Refresh(); }
        private void OnDisable()
        {
            if(walls!=null) foreach(var wall in walls) if(wall!=null) wall.enabled=false;
        }
        private void LateUpdate() => Refresh();
        public void Refresh()
        {
            if(walls==null || walls.Length!=4) walls=new BoxCollider2D[4];
            for(int i=0;i<walls.Length;i++)
                if(walls[i]==null) { walls[i]=gameObject.AddComponent<BoxCollider2D>(); initialized=false; }
            bool solid=level!=null && level.HasMapBounds;
            var rect=level!=null ? level.MapBounds : default;
            if(initialized && solid==appliedEnabled && rect==appliedRect) return;
            initialized=true; appliedEnabled=solid; appliedRect=rect;
            gameObject.layer=GameLayers.ObstaclesLayer;
            var body=GetComponent<Rigidbody2D>(); body.bodyType=RigidbodyType2D.Static;
            foreach(var wall in walls) { wall.isTrigger=false; wall.enabled=solid; }
            if(!solid) return;
            transform.SetPositionAndRotation(rect.center,Quaternion.identity);
            transform.localScale=Vector3.one;
            // Inner faces coincide with the rectangle; thickness lies outside the playable area.
            walls[0].offset=new Vector2(-(rect.width+Thickness)*0.5f,0);
            walls[1].offset=new Vector2((rect.width+Thickness)*0.5f,0);
            walls[0].size=walls[1].size=new Vector2(Thickness,rect.height+2*Thickness);
            walls[2].offset=new Vector2(0,-(rect.height+Thickness)*0.5f);
            walls[3].offset=new Vector2(0,(rect.height+Thickness)*0.5f);
            walls[2].size=walls[3].size=new Vector2(rect.width+2*Thickness,Thickness);
        }
    }
}
