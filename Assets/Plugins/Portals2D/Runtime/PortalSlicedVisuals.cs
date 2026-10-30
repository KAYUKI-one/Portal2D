using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    internal sealed class PortalSlicedVisuals
    {
        private sealed class Visual
        {
            public SpriteRenderer Source, Clone;
            public Material[] Materials;
            public MaterialPropertyBlock Saved = new();
        }
        private readonly List<Visual> visuals = new();
        private readonly MaterialPropertyBlock block = new();
        private static readonly int Plane = Shader.PropertyToID("_PortalPlane");
        public PortalSlicedVisuals(Rigidbody2D body, Material material)
        {
            if (material == null) return;
            foreach (var source in body.GetComponentsInChildren<SpriteRenderer>())
            {
                if (source.GetComponentInParent<Rigidbody2D>() != body) continue;
                var clone = new GameObject("[Portal visual]", typeof(SpriteRenderer)).GetComponent<SpriteRenderer>();
                var visual = new Visual { Source = source, Clone = clone, Materials = source.sharedMaterials };
                source.GetPropertyBlock(visual.Saved);
                source.sharedMaterial = material;
                clone.sharedMaterial = material;
                visuals.Add(visual);
            }
        }
        public void Update(Portal2D portal)
        {
            if (portal == null || !portal.IsLinked) return;
            foreach (var v in visuals)
            {
                if (v.Source == null || v.Clone == null) continue;
                var source = v.Source; var clone = v.Clone;
                clone.enabled = source.enabled && source.gameObject.activeInHierarchy;
                clone.sprite = source.sprite; clone.color = source.color;
                clone.flipX = source.flipX; clone.flipY = source.flipY;
                clone.sortingLayerID = source.sortingLayerID; clone.sortingOrder = source.sortingOrder;
                clone.drawMode = source.drawMode; clone.size = source.size;
                clone.transform.SetPositionAndRotation(portal.MapPoint(source.transform.position, portal.Partner),
                    Quaternion.Euler(0, 0, portal.RotationTo(portal.Partner)) * source.transform.rotation);
                clone.transform.localScale = source.transform.lossyScale;
                SetPlane(source, portal); SetPlane(clone, portal.Partner);
            }
        }
        private void SetPlane(SpriteRenderer renderer, Portal2D portal)
        {
            renderer.GetPropertyBlock(block);
            var n = portal.Normal; var pos = (Vector2)portal.transform.position;
            block.SetVector(Plane, new Vector4(n.x, n.y, -Vector2.Dot(n, pos), 0));
            renderer.SetPropertyBlock(block);
        }
        public void Dispose()
        {
            foreach (var v in visuals)
            {
                if (v.Source != null) { v.Source.sharedMaterials = v.Materials; v.Source.SetPropertyBlock(v.Saved); }
                if (v.Clone != null) { v.Clone.enabled = false; Object.Destroy(v.Clone.gameObject); }
            }
        }
    }
}
