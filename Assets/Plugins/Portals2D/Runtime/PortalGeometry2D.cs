using System.Collections.Generic;
using UnityEngine;

namespace SeamlessPortals2D
{
    internal static class PortalGeometry2D
    {
        internal const float MinEdge = 0.0026f;
        internal static List<Vector2> ClipFront(List<Vector2> polygon, Portal2D portal)
        {
            var output = new List<Vector2>();
            for (int i=0;i<polygon.Count;i++)
            {
                var a = polygon[i]; var b = polygon[(i+1)%polygon.Count];
                float da = portal.SignedDistance(a), db = portal.SignedDistance(b);
                if (da >= 0) output.Add(a);
                if ((da >= 0) != (db >= 0)) output.Add(Vector2.LerpUnclamped(a,b,da/(da-db)));
            }
            return output;
        }
        internal static void AddPolygon(PhysicsShapeGroup2D group, List<Vector2> polygon)
        {
            const float sqr = MinEdge * MinEdge;
            var clean = new List<Vector2>(polygon.Count);
            foreach (var v in polygon)
                if (clean.Count == 0 || (v-clean[clean.Count-1]).sqrMagnitude > sqr) clean.Add(v);
            while (clean.Count > 1 && (clean[0]-clean[clean.Count-1]).sqrMagnitude <= sqr) clean.RemoveAt(clean.Count-1);
            for (int i=1;i<clean.Count-1;i++)
            {
                var a=clean[0]; var b=clean[i]; var c=clean[i+1];
                if ((b-a).sqrMagnitude <= sqr || (c-a).sqrMagnitude <= sqr || (c-b).sqrMagnitude <= sqr) continue;
                float cross=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
                if (Mathf.Abs(cross)<0.00001f) continue;
                group.AddPolygon(cross>0 ? new List<Vector2>{a,b,c} : new List<Vector2>{a,c,b});
            }
        }

        // Copy just the part of the remote world visible through the exit, into entry space.
        internal static PhysicsShapeGroup2D Shadow(Collider2D source, Portal2D entry, Transform shadow)
        {
            var input=new PhysicsShapeGroup2D(); source.GetShapes(input);
            var output=new PhysicsShapeGroup2D(); var vertices=new List<Vector2>();
            var exit=entry.Partner;
            Vector2 Map(Vector2 v) => shadow != null ? (Vector2)shadow.InverseTransformPoint(exit.MapPoint(v,entry)) : exit.MapPoint(v,entry);
            for(int i=0;i<input.shapeCount;i++)
            {
                var shape=input.GetShape(i); vertices.Clear(); input.GetShapeVertices(i,vertices);
                for(int j=0;j<vertices.Count;j++) vertices[j]=input.localToWorldMatrix.MultiplyPoint3x4(vertices[j]);
                if(shape.shapeType==PhysicsShapeType2D.Edges)
                {
                    for(int j=1;j<vertices.Count;j++)
                    {
                        var a=vertices[j-1]; var b=vertices[j]; float da=exit.SignedDistance(a), db=exit.SignedDistance(b);
                        if(da<0 && db<0) continue;
                        if(da<0) a=Vector2.LerpUnclamped(a,b,da/(da-db));
                        else if(db<0) b=Vector2.LerpUnclamped(a,b,da/(da-db));
                        a=Map(a); b=Map(b);
                        if((a-b).sqrMagnitude>MinEdge*MinEdge) output.AddEdges(new List<Vector2>{a,b},shape.radius);
                    }
                    continue;
                }
                if(shape.shapeType==PhysicsShapeType2D.Circle || shape.shapeType==PhysicsShapeType2D.Capsule)
                {
                    var a=vertices[0]; var b=vertices[vertices.Count-1]; vertices=new List<Vector2>();
                    float angle=Mathf.Atan2(b.y-a.y,b.x-a.x);
                    const int halfSegments=16;
                    float radius=shape.radius/Mathf.Cos(Mathf.PI/(halfSegments*2));
                    for(int j=0;j<=halfSegments;j++) { float t=angle-Mathf.PI*0.5f+j*Mathf.PI/halfSegments; vertices.Add(b+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*radius); }
                    for(int j=0;j<=halfSegments;j++) { float t=angle+Mathf.PI*0.5f+j*Mathf.PI/halfSegments; vertices.Add(a+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*radius); }
                }
                var clipped=ClipFront(vertices,exit);
                for(int j=0;j<clipped.Count;j++) clipped[j]=Map(clipped[j]);
                AddPolygon(output,clipped);
            }
            return output;
        }
    }
}
