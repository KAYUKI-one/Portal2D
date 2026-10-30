using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace SeamlessPortals2D.Tests
{
    public partial class PortalPhysicsTests
    {
        private readonly List<GameObject> created = new();
        private readonly Vector2 origin = new(1000, 1000);
        private float originalTimeScale;
        [SetUp] public void Setup() { originalTimeScale = Time.timeScale; Time.timeScale = 1; }
        private GameObject Make(string name)
        {
            var go = new GameObject(name); created.Add(go); return go;
        }
        private BoxCollider2D Wall(Vector2 position, Vector2 normal, float length = 8)
        {
            var go = Make("Test wall"); go.transform.position = position - normal * 0.5f;
            go.transform.rotation = Quaternion.Euler(0, 0, Vector2.SignedAngle(Vector2.up, normal));
            var col = go.AddComponent<BoxCollider2D>(); col.size = new Vector2(length, 1); return col;
        }
        private Portal2D Portal(Vector2 position, Vector2 normal, Collider2D support)
        {
            var p = Make("Test portal").AddComponent<Portal2D>();
            p.Place(position, normal, new[] { support }); return p;
        }
        private Rigidbody2D Body(Vector2 position, Vector2 velocity, bool circle = false)
        {
            var go = Make("Unmodified physics body"); go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
            body.angularDamping = 0; body.linearDamping = 0;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (circle) go.AddComponent<CircleCollider2D>().radius = 0.3f;
            else go.AddComponent<BoxCollider2D>().size = new Vector2(0.65f, 0.9f);
            body.linearVelocity = velocity; return body;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach (var go in created) if (go != null) Object.Destroy(go);
            created.Clear(); yield return null; Time.timeScale = originalTimeScale;
        }
        [UnityTest] public IEnumerator AllSixteenCardinalMappingsPreserveMomentum()
        {
            var normals = new[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
            foreach (var n1 in normals) foreach (var n2 in normals)
            {
                Vector2 entry = origin, exit = origin + Vector2.right * 15;
                var a = Portal(entry, n1, Wall(entry, n1)); var b = Portal(exit, n2, Wall(exit, n2)); a.Link(b);
                Vector2 incoming = -n1 * 10;
                var body = Body(entry + n1 * 1.2f, incoming);
                bool crossed = false; Vector2 outgoing = default;
                void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) { crossed = true; outgoing = e.Velocity; } }
                PortalEvents.Transit += Receive;
                for (int i = 0; i < 20 && !crossed; i++) yield return new WaitForFixedUpdate();
                PortalEvents.Transit -= Receive;
                Assert.IsTrue(crossed, $"Failed {n1} -> {n2}, body {body.position}, velocity {body.linearVelocity}");
                Assert.That(Vector2.Distance(outgoing, n2 * 10), Is.LessThan(0.03f));
                foreach (var go in created) if (go != null) Object.Destroy(go);
                created.Clear(); yield return null;
            }
        }
        [UnityTest] public IEnumerator HighSpeedCircleNeedsNoTravellerComponent()
        {
            var a = Portal(origin, Vector2.up, Wall(origin, Vector2.up));
            var exit = origin + Vector2.right * 15; var b = Portal(exit, Vector2.left, Wall(exit, Vector2.left)); a.Link(b);
            var body = Body(origin + Vector2.up * 2, Vector2.down * 70, true);
            bool crossed = false;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) { crossed = true; Assert.That(e.Velocity.x, Is.EqualTo(-70).Within(0.05)); } }
            PortalEvents.Transit += Receive;
            for (int i = 0; i < 10 && !crossed; i++) yield return new WaitForFixedUpdate();
            PortalEvents.Transit -= Receive;
            Assert.IsTrue(crossed);
        }
        [UnityTest] public IEnumerator ReversingInsideApertureDoesNotNeedCooldown()
        {
            var a = Portal(origin, Vector2.up, Wall(origin, Vector2.up));
            var exit = origin + Vector2.right * 15; var b = Portal(exit, Vector2.up, Wall(exit, Vector2.up)); a.Link(b);
            var body = Body(origin + Vector2.up, Vector2.down * 6);
            int crossings = 0;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) crossings++; }
            PortalEvents.Transit += Receive;
            for (int i = 0; i < 20 && crossings == 0; i++) yield return new WaitForFixedUpdate();
            body.linearVelocity = Vector2.down * 6;
            for (int i = 0; i < 20 && crossings < 2; i++) yield return new WaitForFixedUpdate();
            PortalEvents.Transit -= Receive;
            Assert.That(crossings, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator UnlinkRestoresCollisionAndRemovesProxies()
        {
            var surface = Wall(origin, Vector2.up); var a = Portal(origin, Vector2.up, surface);
            var exit = origin + Vector2.right * 15; var b = Portal(exit, Vector2.up, Wall(exit, Vector2.up)); a.Link(b);
            var body = Body(origin + Vector2.up * 0.5f, Vector2.down);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(Physics2D.GetIgnoreCollision(body.GetComponent<Collider2D>(), surface));
            a.Unlink(); yield return null;
            Assert.IsFalse(Physics2D.GetIgnoreCollision(body.GetComponent<Collider2D>(), surface));
            Assert.IsFalse(PortalSimulation2D.IsTravelling(body));
            Assert.That(Object.FindObjectsByType<PortalCollisionProxy2D>().Length, Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator OversizedBodyCannotEnter()
        {
            var a = Portal(origin, Vector2.up, Wall(origin, Vector2.up));
            var exit = origin + Vector2.right * 15; var b = Portal(exit, Vector2.up, Wall(exit, Vector2.up)); a.Link(b);
            var body = Body(origin + Vector2.up, Vector2.down * 8); body.GetComponent<BoxCollider2D>().size = new Vector2(3, 0.8f);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.Less(Vector2.Distance(body.position, origin), 2); Assert.Greater(body.position.y, origin.y);
        }
        [UnityTest] public IEnumerator SharedPolygonKeepsCornerFloorSolid()
        {
            var go = Make("Single L shaped wall and floor"); go.transform.position = origin;
            var surface = go.AddComponent<PolygonCollider2D>();
            surface.points = new[] { new Vector2(-6,-1), new Vector2(1,-1), new Vector2(1,5), new Vector2(0,5), Vector2.zero, new Vector2(-6,0) };
            var a = Portal(origin + Vector2.up * 1.1f, Vector2.left, surface);
            var exit = origin + Vector2.right * 15; var b = Portal(exit, Vector2.left, Wall(exit, Vector2.left)); a.Link(b);
            var body = Body(origin + new Vector2(-1.1f,0.48f), Vector2.right * 3); body.gravityScale = 3;
            float lowest = body.position.y; bool crossed = false;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) crossed = true; }
            PortalEvents.Transit += Receive;
            for (int i = 0; i < 60 && !crossed; i++) { body.linearVelocity = new Vector2(3, body.linearVelocity.y); yield return new WaitForFixedUpdate(); if (!crossed) lowest = Mathf.Min(lowest,body.position.y); }
            PortalEvents.Transit -= Receive;
            Assert.Greater(lowest, origin.y + 0.35f, "Traveller fell through the shared floor"); Assert.IsTrue(crossed);
        }
        [Test] public void PlacementRejectsShortSurfacesAndSnapsToCorner()
        {
            Wall(origin, Vector2.up, 1.5f); Physics2D.SyncTransforms();
            Assert.IsFalse(PortalPlacement2D.TryFit(origin, Vector2.up, 2.2f, ~0, out _));
            var position = origin + Vector2.right * 15; Wall(position, Vector2.up, 4); Physics2D.SyncTransforms();
            Assert.IsTrue(PortalPlacement2D.TryFit(position + Vector2.left * 0.5f, Vector2.up, 2.2f, ~0, out var placement));
            Assert.That(placement.Position.x, Is.EqualTo(position.x - 0.9f).Within(0.015f));
        }

        [UnityTest] public IEnumerator CompoundBodyPreservesTangentialAndAngularVelocity()
        {
            var a = Portal(origin, Vector2.up, Wall(origin, Vector2.up));
            var exit = origin + Vector2.right * 15; var b = Portal(exit, Vector2.right, Wall(exit, Vector2.right)); a.Link(b);
            var body = Body(origin + Vector2.up, new Vector2(2,-9)); body.angularVelocity = 25;
            var child = Make("Second collider"); child.transform.SetParent(body.transform, false); child.transform.localPosition = Vector2.left * 0.35f;
            child.AddComponent<CircleCollider2D>().radius = 0.15f;
            bool crossed = false; Vector2 velocity = default; float spin = 0;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) { crossed = true; velocity = e.Velocity; spin = body.angularVelocity; } }
            PortalEvents.Transit += Receive;
            for (int i=0;i<30 && !crossed;i++) yield return new WaitForFixedUpdate();
            PortalEvents.Transit -= Receive;
            Assert.IsTrue(crossed); Assert.That(Vector2.Distance(velocity,new Vector2(9,2)),Is.LessThan(0.05)); Assert.That(spin,Is.EqualTo(25).Within(0.05));
        }
        [UnityTest] public IEnumerator TransitSubscriberCanRemovePortalSafely()
        {
            var a = Portal(origin, Vector2.up, Wall(origin, Vector2.up));
            var exit = origin + Vector2.right * 15; var b = Portal(exit, Vector2.up, Wall(exit, Vector2.up)); a.Link(b);
            var body = Body(origin + Vector2.up, Vector2.down * 6); bool crossed = false;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) { crossed = true; a.Unlink(); } }
            PortalEvents.Transit += Receive;
            for (int i=0;i<30 && !crossed;i++) yield return new WaitForFixedUpdate();
            PortalEvents.Transit -= Receive;
            Assert.IsTrue(crossed); Assert.IsFalse(PortalSimulation2D.IsTravelling(body));
        }
        [Test] public void PlacementRejectsGapAcrossSeparateWallPieces()
        {
            Wall(origin + Vector2.left * 0.9f,Vector2.up,1.4f);
            Wall(origin + Vector2.right * 0.9f,Vector2.up,1.4f);
            Physics2D.SyncTransforms();
            Assert.IsFalse(PortalPlacement2D.TryFit(origin + Vector2.left * 0.7f,Vector2.up,2.2f,~0,out _));
        }

        [UnityTest] public IEnumerator TilemapCornerToCeilingKeepsFloorSolid()
        {
            var grid = Make("Corner regression grid"); grid.AddComponent<Grid>();
            grid.transform.position = origin;
            var go = Make("Shared tilemap floor pillar and ceiling"); go.transform.SetParent(grid.transform, false);
            go.layer = 8;
            var map = go.AddComponent<Tilemap>();
            var tile = ScriptableObject.CreateInstance<Tile>(); tile.colliderType = Tile.ColliderType.Grid;
            for (int x = -6; x < 12; x++) for (int y = -4; y < 0; y++) map.SetTile(new Vector3Int(x,y), tile);
            for (int x = 0; x < 2; x++) for (int y = 0; y < 3; y++) map.SetTile(new Vector3Int(x,y), tile);
            for (int x = 4; x < 9; x++) for (int y = 5; y < 7; y++) map.SetTile(new Vector3Int(x,y), tile);
            go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var composite = go.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            var tiles = go.AddComponent<TilemapCollider2D>(); tiles.compositeOperation = Collider2D.CompositeOperation.Merge;
            tiles.ProcessTilemapChanges(); composite.GenerateGeometry(); Physics2D.SyncTransforms();
            Assert.Greater(composite.shapeCount, 0);
            Assert.IsTrue(PortalPlacement2D.TryFit(origin + new Vector2(0,1), Vector2.left, 2.2f, ~0, out var entry));
            Assert.IsTrue(PortalPlacement2D.TryFit(origin + new Vector2(5,5), Vector2.down, 2.2f, ~0, out var exit));
            var a = Portal(entry.Position, entry.Normal, entry.Supports[0]);
            var b = Portal(exit.Position, exit.Normal, exit.Supports[0]); a.Link(b);
            var body = Body(origin + new Vector2(-1.1f,0.65f), Vector2.right * 3);
            body.GetComponent<BoxCollider2D>().size = new Vector2(0.65f,1.25f);
            body.constraints = RigidbodyConstraints2D.FreezeRotation; body.gravityScale = 3;
            float lowest = body.position.y; bool crossed = false;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) crossed = true; }
            PortalEvents.Transit += Receive;
            try
            {
                for (int i=0;i<80 && !crossed;i++)
                {
                    body.linearVelocity = new Vector2(3, body.linearVelocity.y);
                    yield return new WaitForFixedUpdate();
                    if (!crossed) lowest = Mathf.Min(lowest,body.position.y);
                }
                Assert.Greater(lowest, origin.y + 0.55f, $"Fell through shared Tilemap; body={body.position}, travelling={PortalSimulation2D.IsTravelling(body)}");
                Assert.IsTrue(crossed, $"Never crossed Tilemap portal; body={body.position}");
            }
            finally { PortalEvents.Transit -= Receive; Object.Destroy(tile); }
        }

        [UnityTest] public IEnumerator InvalidExitGeometryLeavesOriginalFloorSolid()
        {
            var floor = Wall(origin, Vector2.up);
            var a = Portal(origin, Vector2.up, floor);
            var empty = Make("Empty exit collider").AddComponent<CustomCollider2D>();
            var exit = origin + Vector2.right * 15;
            var b = Portal(exit, Vector2.up, empty); a.Link(b);
            var body = Body(origin + Vector2.up * 0.5f, Vector2.down);
            body.gravityScale = 3;
            LogAssert.Expect(LogType.Warning, "Portal entry kept solid: Supporting collider Empty exit collider has no active physics shapes.");
            for (int i=0;i<20;i++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(Physics2D.GetIgnoreCollision(body.GetComponent<Collider2D>(),floor));
            Assert.IsFalse(PortalSimulation2D.IsTravelling(body));
            Assert.Greater(body.position.y, origin.y + 0.4f);
            yield return null;
            Assert.That(Object.FindObjectsByType<PortalCollisionProxy2D>().Length, Is.Zero, "Failed builds leaked temporary colliders");
            // Repairing the support must allow a later entry, without restarting the simulation.
            var shapes = new PhysicsShapeGroup2D(); shapes.AddBox(exit + Vector2.down * 0.5f, new Vector2(8,1));
            empty.SetCustomShapes(shapes);
            bool crossed = false;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) crossed = true; }
            PortalEvents.Transit += Receive;
            try
            {
                for (int i=0;i<40 && !crossed;i++) yield return new WaitForFixedUpdate();
                Assert.IsTrue(crossed, "Portal did not recover after its support was repaired");
            }
            finally { PortalEvents.Transit -= Receive; }
        }
    }
}
