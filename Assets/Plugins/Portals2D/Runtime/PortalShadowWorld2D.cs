using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    internal sealed class PortalShadowWorld2D
    {
        private readonly Dictionary<(Portal2D, Object),PortalShadowBody2D> shadows=new();
        internal void Refresh(Dictionary<Rigidbody2D,Portal2D> travellers)
        {
            foreach(var shadow in shadows.Values) shadow.Owners.Clear();
            var order=new Dictionary<Rigidbody2D,int>();
            foreach(var body in travellers.Keys) order.Add(body,order.Count);
            var all=Object.FindObjectsByType<Collider2D>();
            foreach(var traveller in travellers)
            {
                var owner=traveller.Key; var entry=traveller.Value;
                if(owner==null || entry==null || !entry.IsLinked) continue;
                var ownColliders=owner.GetComponentsInChildren<Collider2D>();
                var region=new Bounds(entry.MapPoint(owner.worldCenterOfMass,entry.Partner),Vector3.zero);
                foreach(var c in ownColliders)
                    if(c.attachedRigidbody==owner && c.enabled && !c.isTrigger)
                    {
                        var bounds=c.bounds;
                        for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2)
                            region.Encapsulate(entry.MapPoint((Vector2)bounds.center+Vector2.Scale(bounds.extents,new Vector2(x,y)),entry.Partner));
                    }
                region.Expand(0.3f+owner.linearVelocity.magnitude*Time.fixedDeltaTime*2);
                foreach(var source in all)
                {
                    if(!source.enabled || source.isTrigger || source.shapeCount==0 ||
                        source.GetComponent<PortalCollisionProxy2D>()!=null || source.attachedRigidbody==owner ||
                        !region.Intersects(source.bounds)) continue;
                    var target=source.attachedRigidbody;
                    // Static geometry is unioned with the local support by PortalCollisionScope.
                    // A separate clipped static copy would introduce a false ledge at the seam.
                    if(target==null || target.bodyType==RigidbodyType2D.Static || !target.simulated) continue;
                    if(target!=null && travellers.TryGetValue(target,out var otherEntry))
                    {
                        // Same-chart bodies already collide directly. Opposite-chart pairs get one solver contact.
                        if(otherEntry==entry || (otherEntry==entry.Partner && order[owner]>order[target])) continue;
                    }
                    bool canCollide=false;
                    foreach(var c in ownColliders)
                        if(c.attachedRigidbody==owner && c.enabled && !c.isTrigger &&
                            !Physics2D.GetIgnoreLayerCollision(c.gameObject.layer,source.gameObject.layer) &&
                            !Physics2D.GetIgnoreCollision(c,source)) { canCollide=true; break; }
                    if(!canCollide) continue;
                    Object keyTarget=target!=null && target.bodyType!=RigidbodyType2D.Static ? target : source;
                    var key=(entry,keyTarget);
                    if(!shadows.TryGetValue(key,out var shadow))
                    {
                        var go=new GameObject("[Portal shadow collision]"); go.layer=source.gameObject.layer;
                        go.AddComponent<PortalCollisionProxy2D>();
                        shadow=go.AddComponent<PortalShadowBody2D>(); shadow.Entry=entry;
                        shadow.Original=target;
                        shadow.Body=go.AddComponent<Rigidbody2D>(); shadow.Body.bodyType=target!=null ? target.bodyType : RigidbodyType2D.Static;
                        shadow.Body.gravityScale=0; shadow.Body.linearDamping=0; shadow.Body.angularDamping=0;
                        shadow.Body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
                        shadows.Add(key,shadow);
                    }
                    shadow.Owners.Add(owner);
                    if(!shadow.Shapes.ContainsKey(source))
                    {
                        var collider=shadow.gameObject.AddComponent<CustomCollider2D>(); collider.enabled=false;
                        collider.sharedMaterial=source.sharedMaterial; shadow.Shapes.Add(source,collider);
                    }
                }
            }
            var remove=new List<(Portal2D,Object)>();
            foreach(var pair in shadows)
            {
                var shadow=pair.Value;
                if(shadow.Owners.Count==0 || shadow.Entry==null || !shadow.Entry.IsLinked)
                { shadow.Stop(); remove.Add(pair.Key); continue; }
                shadow.Prepare();
                foreach(var shape in shadow.Shapes)
                {
                    var source=shape.Key; var copy=shape.Value;
                    if(source==null || !source.enabled || source.shapeCount==0) { copy.enabled=false; continue; }
                    var geometry=PortalGeometry2D.Shadow(source,shadow.Entry,shadow.transform);
                    copy.enabled=geometry.shapeCount>0;
                    if(copy.enabled) copy.SetCustomShapes(geometry);
                }
                // Shape changes can recalculate mass data, so restore the original mass/inertia afterwards.
                shadow.Prepare();
            }
            foreach(var key in remove) shadows.Remove(key);
            all=Object.FindObjectsByType<Collider2D>();
            foreach(var shadow in shadows.Values) foreach(var shape in shadow.Shapes)
                if(shape.Value.enabled) foreach(var c in all)
                {
                    var copy=shape.Value;
                    if(!c.enabled || c.attachedRigidbody==shadow.Body) continue;
                    bool allowed=c.attachedRigidbody!=null && shadow.Owners.Contains(c.attachedRigidbody) &&
                        !Physics2D.GetIgnoreLayerCollision(copy.gameObject.layer,c.gameObject.layer) && shape.Key!=null &&
                        !Physics2D.GetIgnoreCollision(c,shape.Key);
                    Physics2D.IgnoreCollision(copy,c,!allowed);
                }
        }
        internal void Release(Rigidbody2D body)
        {
            var remove=new List<(Portal2D,Object)>();
            foreach(var pair in shadows)
            {
                var shadow=pair.Value;
                shadow.Owners.Remove(body);
                if(shadow.Owners.Count==0) { shadow.Stop(); remove.Add(pair.Key); }
            }
            foreach(var key in remove) shadows.Remove(key);
        }
        internal void Dispose()
        {
            foreach(var shadow in shadows.Values) shadow.Stop(); shadows.Clear();
        }
    }
}
