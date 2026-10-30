using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    // A private copy of each supporting surface keeps the rest of a shared tilemap solid.
    // Only the travelling body sees this copy, with the two apertures cut out of it.
    internal sealed class PortalCollisionScope
    {
        // PhysicsShapeGroup2D rejects polygon edges at or below 0.0025 units.
        // Clipping at a tile corner can produce duplicate vertices or very short edges.
        private const float VertexSeparation = 0.0026f;
        private const float VertexSeparationSquared = VertexSeparation * VertexSeparation;
        private readonly List<(Collider2D body, Collider2D surface, bool ignored)> pairs = new();
        private readonly List<Collider2D> proxies = new();
        private readonly Rigidbody2D owner;
        private PortalCollisionScope(Rigidbody2D body) => owner = body;

        public static bool TryCreate(Rigidbody2D body, Collider2D[] colliders, Portal2D entry,
            out PortalCollisionScope scope, out string failure)
        {
            scope = new PortalCollisionScope(body);
            try
            {
                scope.Build(colliders, entry);
                failure = null;
                return true;
            }
            catch (System.Exception exception)
            {
                // Includes partially built proxies and any collision pairs already switched.
                scope.Dispose(); scope = null;
                failure = exception.Message;
                return false;
            }
        }

        private void Build(Collider2D[] colliders, Portal2D entry)
        {
            var merged = new Dictionary<(int,PhysicsMaterial2D), List<Vector2[]>>();
            var surfaces = new HashSet<Collider2D>();
            foreach (var p in new[] { entry, entry.Partner })
                foreach (var c in p.Supports) if (c != null) surfaces.Add(c);
            if (surfaces.Count == 0) throw new System.InvalidOperationException("Portal has no supporting collider.");
            foreach (var surface in surfaces)
            {
                if (!surface.enabled || !surface.gameObject.activeInHierarchy || surface.shapeCount == 0)
                    throw new System.InvalidOperationException($"Supporting collider {surface.name} has no active physics shapes.");
                var source = new PhysicsShapeGroup2D();
                surface.GetShapes(source);
                if (source.shapeCount == 0)
                    throw new System.InvalidOperationException($"Supporting collider {surface.name} has no active physics shapes.");
                var output = new PhysicsShapeGroup2D();
                var vertices = new List<Vector2>();
                var cuts = new List<Portal2D>();
                foreach (var p in new[] { entry, entry.Partner })
                    foreach (var support in p.Supports)
                        if (support == surface) { cuts.Add(p); break; }
                for (int i = 0; i < source.shapeCount; i++)
                {
                    var shape = source.GetShape(i);
                    vertices.Clear();
                    source.GetShapeVertices(i, vertices);
                    for (int j = 0; j < vertices.Count; j++)
                        vertices[j] = source.localToWorldMatrix.MultiplyPoint3x4(vertices[j]);
                    if (shape.shapeType == PhysicsShapeType2D.Polygon)
                    {
                        var pieces = new List<List<Vector2>> { new(vertices) };
                        foreach (var p in cuts) pieces = Subtract(pieces, p, owner);
                        foreach (var polygon in pieces) AddPolygon(output, polygon);
                    }
                    else if (shape.shapeType == PhysicsShapeType2D.Edges)
                    {
                        for (int j = 1; j < vertices.Count; j++)
                        {
                            var segments = new List<List<Vector2>> { new() { vertices[j - 1], vertices[j] } };
                            foreach (var p in cuts) segments = SubtractSegments(segments, p, owner);
                            foreach (var segment in segments)
                                if ((segment[1] - segment[0]).sqrMagnitude > VertexSeparationSquared)
                                    output.AddEdges(segment, shape.radius);
                        }
                    }
                    else if (shape.shapeType == PhysicsShapeType2D.Circle)
                        output.AddCircle(vertices[0], shape.radius);
                    else if (shape.shapeType == PhysicsShapeType2D.Capsule)
                        output.AddCapsule(vertices[0], vertices[1], shape.radius);
                }
                CollectGeometry(surface,output,merged);
            }
            // Union the remote static world with the local terrain. In particular, two coplanar
            // floors must have ONE exterior boundary, not two colliders with an artificial end cap.
            float radius=entry.HalfSpan+Depth(owner);
            foreach(var surface in Physics2D.OverlapCircleAll(entry.Partner.transform.position,radius))
            {
                if(surface.isTrigger || !surface.enabled || surface.shapeCount==0 ||
                    surface.GetComponent<PortalCollisionProxy2D>()!=null || surface.usedByEffector ||
                    (surface.attachedRigidbody!=null && surface.attachedRigidbody.bodyType!=RigidbodyType2D.Static)) continue;
                CollectGeometry(surface,PortalGeometry2D.Shadow(surface,entry,null),merged);
            }
            foreach(var group in merged)
            {
                if(group.Value.Count==0) continue;
                var go=new GameObject("[Portal surface aperture]"); go.layer=group.Key.Item1;
                go.AddComponent<PortalCollisionProxy2D>(); go.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
                var composite=go.AddComponent<CompositeCollider2D>(); proxies.Add(composite);
                composite.geometryType=CompositeCollider2D.GeometryType.Polygons;
                composite.generationType=CompositeCollider2D.GenerationType.Manual;
                composite.sharedMaterial=group.Key.Item2;
                // Paths on ONE PolygonCollider2D can describe holes: overlapping local and
                // mapped floors must be separate merge operands, not overlapping paths.
                foreach(var path in group.Value)
                {
                    var polygon=go.AddComponent<PolygonCollider2D>();
                    polygon.compositeOperation=Collider2D.CompositeOperation.Merge;
                    polygon.points=path;
                }
                composite.GenerateGeometry();
                if(composite.shapeCount==0)
                    throw new System.InvalidOperationException("Failed to merge portal terrain geometry.");
            }
            // Commit only after EVERY supporting surface has been rebuilt successfully.
            IgnoreOtherBodies();
            foreach (var surface in surfaces)
                foreach (var c in colliders)
                {
                    pairs.Add((c, surface, Physics2D.GetIgnoreCollision(c, surface)));
                    Physics2D.IgnoreCollision(c, surface, true);
                }
        }
        private void CollectGeometry(Collider2D source, PhysicsShapeGroup2D geometry,
            Dictionary<(int,PhysicsMaterial2D),List<Vector2[]>> merged)
        {
            var key=(source.gameObject.layer,source.sharedMaterial);
            if(!merged.TryGetValue(key,out var paths)) { paths=new List<Vector2[]>(); merged.Add(key,paths); }
            var fallback=new PhysicsShapeGroup2D(); var vertices=new List<Vector2>();
            for(int i=0;i<geometry.shapeCount;i++)
            {
                var shape=geometry.GetShape(i); vertices.Clear(); geometry.GetShapeVertices(i,vertices);
                if(shape.shapeType==PhysicsShapeType2D.Polygon) paths.Add(vertices.ToArray());
                else if(shape.shapeType==PhysicsShapeType2D.Edges) fallback.AddEdges(vertices,shape.radius);
                else if(shape.shapeType==PhysicsShapeType2D.Circle) fallback.AddCircle(vertices[0],shape.radius);
                else if(shape.shapeType==PhysicsShapeType2D.Capsule) fallback.AddCapsule(vertices[0],vertices[1],shape.radius);
            }
            if(fallback.shapeCount==0) return;
            var go=new GameObject("[Portal surface aperture]"); go.layer=source.gameObject.layer;
            go.AddComponent<PortalCollisionProxy2D>(); var proxy=go.AddComponent<CustomCollider2D>(); proxies.Add(proxy);
            proxy.sharedMaterial=source.sharedMaterial; proxy.SetCustomShapes(fallback);
        }

        private static float Depth(Rigidbody2D body) => Mathf.Max(4, body.linearVelocity.magnitude * Time.fixedDeltaTime * 3 + 3);
        private static List<List<Vector2>> Subtract(List<List<Vector2>> pieces, Portal2D portal, Rigidbody2D body)
        {
            var result = new List<List<Vector2>>();
            var origin = (Vector2)portal.transform.position;
            var axes = new[] { portal.Tangent, -portal.Tangent, portal.Normal, -portal.Normal };
            var bounds = new[] { portal.HalfSpan, portal.HalfSpan, 0.025f, Depth(body) };
            foreach (var piece in pieces)
            {
                var remaining = piece;
                for (int k = 0; k < 4 && remaining.Count >= 3; k++)
                {
                    var outside = Clip(remaining, origin, axes[k], bounds[k], false);
                    if (outside.Count >= 3) result.Add(outside);
                    remaining = Clip(remaining, origin, axes[k], bounds[k], true);
                }
            }
            return result;
        }
        private static List<Vector2> Clip(List<Vector2> input, Vector2 origin, Vector2 axis, float limit, bool inside)
        {
            var output = new List<Vector2>();
            for (int i = 0; i < input.Count; i++)
            {
                var a = input[i]; var b = input[(i + 1) % input.Count];
                float da = Vector2.Dot(a - origin, axis) - limit;
                float db = Vector2.Dot(b - origin, axis) - limit;
                bool aIn = inside ? da <= 0 : da >= 0;
                bool bIn = inside ? db <= 0 : db >= 0;
                if (aIn) output.Add(a);
                if (aIn != bIn) output.Add(Vector2.LerpUnclamped(a, b, da / (da - db)));
            }
            return output;
        }
        private static void AddPolygon(PhysicsShapeGroup2D group, List<Vector2> polygon)
        {
            var clean = new List<Vector2>(polygon.Count);
            foreach (var vertex in polygon)
                if (clean.Count == 0 || (vertex - clean[clean.Count - 1]).sqrMagnitude > VertexSeparationSquared)
                    clean.Add(vertex);
            while (clean.Count > 1 && (clean[0] - clean[clean.Count - 1]).sqrMagnitude <= VertexSeparationSquared)
                clean.RemoveAt(clean.Count - 1);
            // Triangles also avoid Box2D's maximum vertex count after clipping.
            for (int i = 1; i < clean.Count - 1; i++)
            {
                var a = clean[0]; var b = clean[i]; var c = clean[i + 1];
                if ((b - a).sqrMagnitude <= VertexSeparationSquared ||
                    (c - a).sqrMagnitude <= VertexSeparationSquared ||
                    (c - b).sqrMagnitude <= VertexSeparationSquared) continue;
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (Mathf.Abs(cross) < 0.00001f) continue;
                group.AddPolygon(cross > 0 ? new List<Vector2> { a, b, c } : new List<Vector2> { a, c, b });
            }
        }
        private static List<List<Vector2>> SubtractSegments(List<List<Vector2>> segments, Portal2D p, Rigidbody2D body)
        {
            var output = new List<List<Vector2>>();
            foreach (var segment in segments)
            {
                Vector2 a = p.transform.InverseTransformPoint(segment[0]);
                Vector2 b = p.transform.InverseTransformPoint(segment[1]);
                float lo = 0, hi = 1;
                Vector2 d = b - a;
                bool hit = Slab(a.x, d.x, -p.HalfSpan, p.HalfSpan, ref lo, ref hi) &&
                    Slab(a.y, d.y, -Depth(body), 0.025f, ref lo, ref hi);
                if (!hit) { output.Add(segment); continue; }
                if (lo > 0.0001f) output.Add(new() { segment[0], Vector2.Lerp(segment[0], segment[1], lo) });
                if (hi < 0.9999f) output.Add(new() { Vector2.Lerp(segment[0], segment[1], hi), segment[1] });
            }
            return output;
        }
        private static bool Slab(float a, float d, float min, float max, ref float lo, ref float hi)
        {
            if (Mathf.Abs(d) < 0.000001f) return a >= min && a <= max;
            float x = (min - a) / d, y = (max - a) / d;
            lo = Mathf.Max(lo, Mathf.Min(x, y)); hi = Mathf.Min(hi, Mathf.Max(x, y));
            return lo <= hi;
        }
        public void IgnoreOtherBodies()
        {
            foreach (var collider in Object.FindObjectsByType<Collider2D>())
            {
                if (collider.attachedRigidbody == owner || collider.GetComponent<PortalCollisionProxy2D>() != null) continue;
                foreach (var proxy in proxies)
                    if (proxy != null && proxy.enabled && collider.enabled) Physics2D.IgnoreCollision(proxy, collider, true);
            }
        }
        public void Dispose()
        {
            foreach (var pair in pairs)
                if (pair.body != null && pair.surface != null)
                    Physics2D.IgnoreCollision(pair.body, pair.surface, pair.ignored);
            foreach (var proxy in proxies)
                if (proxy != null) { proxy.enabled = false; Object.Destroy(proxy.gameObject); }
            pairs.Clear(); proxies.Clear();
        }
    }
}
