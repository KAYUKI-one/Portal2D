using Portal2D.Core.Data;
using UnityEngine;

namespace Portal2D.Presentation.Level
{
    // Derived presentation, rebuilt from the level asset rather than saved as a gameplay object.
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class LevelBackground2D : MonoBehaviour
    {
        [SerializeField] private LevelAsset level;
        private SpriteRenderer image;
        private Rect appliedRect;
        public static void Create(GameObject root, LevelAsset source)
        {
            var go=new GameObject("[Level background]"); go.transform.SetParent(root.transform,false);
            var background=go.AddComponent<LevelBackground2D>(); background.level=source; background.Refresh();
        }
        private void LateUpdate() => Refresh();
        private void Refresh()
        {
            if(image==null) image=GetComponent<SpriteRenderer>();
            bool visible=level!=null && level.BackgroundSprite!=null;
            if(image.enabled!=visible) image.enabled=visible;
            if(!visible) return;
            if(image.sprite==level.BackgroundSprite && image.color==level.BackgroundTint && appliedRect==level.BackgroundRect) return;
            image.sprite=level.BackgroundSprite; image.color=level.BackgroundTint;
            image.sortingLayerID=SortingLayer.layers[0].id; image.sortingOrder=-32768;
            var rect=level.BackgroundRect; appliedRect=rect; var sprite=image.sprite.bounds;
            var scale=new Vector3(rect.width/Mathf.Max(0.001f,sprite.size.x),rect.height/Mathf.Max(0.001f,sprite.size.y),1);
            transform.localScale=scale;
            transform.position=new Vector3(rect.center.x-sprite.center.x*scale.x,rect.center.y-sprite.center.y*scale.y,5);
        }
    }
}
