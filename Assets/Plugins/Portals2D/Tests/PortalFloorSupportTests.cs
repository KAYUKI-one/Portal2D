using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace SeamlessPortals2D.Tests
{
    public partial class PortalPhysicsTests
    {
        [UnityTest] public IEnumerator SlowBoxKeepsFullFloorSupportWhileStraddlingWallPortal()
        {
            var grid=Make("Slow crossing grid"); grid.AddComponent<Grid>(); grid.transform.position=origin;
            var go=Make("Shared floor and walls"); go.transform.SetParent(grid.transform,false);
            var map=go.AddComponent<Tilemap>(); var tile=ScriptableObject.CreateInstance<Tile>(); tile.colliderType=Tile.ColliderType.Grid;
            for(int x=-8;x<4;x++) for(int y=-3;y<0;y++) map.SetTile(new Vector3Int(x,y),tile);
            for(int y=0;y<3;y++) for(int x=0;x<2;x++) map.SetTile(new Vector3Int(x,y),tile);
            for(int y=0;y<3;y++) for(int x=-8;x<-6;x++) map.SetTile(new Vector3Int(x,y),tile);
            go.AddComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;
            var surface=go.AddComponent<CompositeCollider2D>(); surface.geometryType=CompositeCollider2D.GeometryType.Polygons;
            var tiles=go.AddComponent<TilemapCollider2D>(); tiles.compositeOperation=Collider2D.CompositeOperation.Merge; tiles.ProcessTilemapChanges(); surface.GenerateGeometry();
            var material=new PhysicsMaterial2D("Frictionless support test"){friction=0,bounciness=0}; surface.sharedMaterial=material;
            var a=Portal(origin+new Vector2(0,1.1f),Vector2.left,surface);
            var b=Portal(origin+new Vector2(-6,1.1f),Vector2.right,surface); a.Link(b);
            var body=Body(origin+new Vector2(-0.6f,0.49f),Vector2.right*0.6f);
            body.GetComponent<BoxCollider2D>().size=Vector2.one*0.95f; body.GetComponent<Collider2D>().sharedMaterial=material; body.gravityScale=3;
            bool crossed=false; float minBottom=10, maxTilt=0;
            void Receive(PortalTransitEvent e) { if(e.Body==body && e.Phase==PortalTransitPhase.Teleported) crossed=true; }
            PortalEvents.Transit+=Receive;
            try
            {
                for(int i=0;i<200 && !crossed;i++)
                {
                    yield return new WaitForFixedUpdate();
                    if(crossed) break;
                    float angle=body.rotation*Mathf.Deg2Rad;
                    float bottom=body.position.y-origin.y-0.475f*(Mathf.Abs(Mathf.Cos(angle))+Mathf.Abs(Mathf.Sin(angle)));
                    minBottom=Mathf.Min(minBottom,bottom); maxTilt=Mathf.Max(maxTilt,Mathf.Abs(Mathf.DeltaAngle(body.rotation,0)));
                }
                Assert.IsTrue(crossed,"Slow box stopped in aperture");
                Assert.Greater(minBottom,-0.025f,$"The collider sank into the floor; maximum tilt {maxTilt:F2}");
                Assert.Less(maxTilt,2f,"A level floor produced a spurious rotation");
            }
            finally { PortalEvents.Transit-=Receive; Object.Destroy(tile); Object.Destroy(material); }
        }
    }
}
