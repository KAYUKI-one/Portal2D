using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeamlessPortals2D.Tests
{
    public partial class PortalPhysicsTests
    {
        [UnityTest] public IEnumerator OppositeTravellersDoNotDoubleTheirCollisionImpulse()
        {
            var a=Portal(origin,Vector2.left,Wall(origin,Vector2.left));
            var exit=origin+Vector2.right*15; var b=Portal(exit,Vector2.right,Wall(exit,Vector2.right)); a.Link(b);
            var left=Body(origin+Vector2.left*0.45f,Vector2.right*4);
            var right=Body(exit+Vector2.right*0.45f,Vector2.left*4);
            left.constraints=right.constraints=RigidbodyConstraints2D.FreezeRotation;
            float maxEnergy=0;
            for(int i=0;i<60;i++)
            {
                yield return new WaitForFixedUpdate();
                maxEnergy=Mathf.Max(maxEnergy,left.linearVelocity.sqrMagnitude+right.linearVelocity.sqrMagnitude);
            }
            Assert.LessOrEqual(maxEnergy,32.05f,"The two mirror contacts applied duplicate impulses");
            Assert.That(left.linearVelocity.x+right.linearVelocity.x,Is.EqualTo(0).Within(0.05));
            Assert.Less(left.linearVelocity.magnitude+right.linearVelocity.magnitude,0.1f);
            Assert.Less(left.position.x,origin.x+0.1f); Assert.Greater(right.position.x,exit.x-0.1f);
            a.Unlink(); yield return null;
            Assert.That(Object.FindObjectsByType<PortalCollisionProxy2D>().Length,Is.Zero);
        }

        [UnityTest] public IEnumerator CeilingExitDoesNotPullTheSourceBodyOffItsFloor()
        {
            var floor=Wall(origin,Vector2.up);
            var point=origin+Vector2.up*1.1f;
            var a=Portal(point,Vector2.left,Wall(point,Vector2.left));
            var exit=origin+new Vector2(15,8); var b=Portal(exit,Vector2.down,Wall(exit,Vector2.down)); a.Link(b);
            var material=new PhysicsMaterial2D("No friction regression") {friction=0,bounciness=0}; floor.sharedMaterial=material;
            var body=Body(origin+new Vector2(-0.3f,0.48f),Vector2.right); body.gravityScale=3;
            body.GetComponent<Collider2D>().sharedMaterial=material; body.constraints=RigidbodyConstraints2D.FreezeRotation;
            bool crossed=false, straddledPastCenter=false; Vector2 outgoing=default;
            void Receive(PortalTransitEvent e) { if(e.Body==body && e.Phase==PortalTransitPhase.Teleported) { crossed=true; outgoing=e.Velocity; } }
            PortalEvents.Transit+=Receive;
            try
            {
                for(int i=0;i<60 && !crossed;i++)
                {
                    yield return new WaitForFixedUpdate();
                    if(!crossed)
                    {
                        if(body.position.x>origin.x) straddledPastCenter=true;
                        Assert.That(body.linearVelocity.x,Is.EqualTo(1).Within(0.02),"Exit gravity accelerated the source body");
                        Assert.Greater(body.position.y,origin.y+0.4f);
                    }
                }
                Assert.IsTrue(straddledPastCenter); Assert.IsTrue(crossed);
                Assert.That(outgoing.magnitude,Is.EqualTo(1).Within(0.02));
            }
            finally { PortalEvents.Transit-=Receive; Object.Destroy(material); }
        }

        [UnityTest] public IEnumerator AuthorityChangesOnlyAfterTheWholeColliderCrosses()
        {
            var a = Portal(origin, Vector2.left, Wall(origin, Vector2.left));
            var exit = origin + Vector2.right * 15;
            var b = Portal(exit, Vector2.down, Wall(exit, Vector2.down)); a.Link(b);
            var body = Body(origin + Vector2.left * 0.8f, Vector2.right * 2);
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            bool crossed = false; float sourceDistance = 0; Vector2 velocity = default;
            void Receive(PortalTransitEvent e)
            {
                if (e.Body != body || e.Phase != PortalTransitPhase.Teleported) return;
                crossed = true; sourceDistance = a.SignedDistance(e.PreviousPosition); velocity = e.Velocity;
            }
            PortalEvents.Transit += Receive;
            try
            {
                for (int i=0;i<50 && !crossed;i++) yield return new WaitForFixedUpdate();
                Assert.IsTrue(crossed);
                Assert.LessOrEqual(sourceDistance, -0.325f + 0.003f, "Authority changed while the source-side collider was still visible");
                Assert.That(Vector2.Distance(velocity, Vector2.down * 2), Is.LessThan(0.02f), "Portal added velocity");
            }
            finally { PortalEvents.Transit -= Receive; }
        }

        [UnityTest] public IEnumerator RemoteStaticObstacleBlocksTheEmergingBody()
        {
            var a = Portal(origin, Vector2.left, Wall(origin, Vector2.left));
            var exit = origin + Vector2.right * 15;
            var b = Portal(exit, Vector2.right, Wall(exit, Vector2.right)); a.Link(b);
            var obstacle = Make("Exit obstruction"); obstacle.transform.position = exit + Vector2.right * 0.6f;
            obstacle.AddComponent<BoxCollider2D>().size = new Vector2(0.4f,2);
            var body = Body(origin + Vector2.left * 0.8f, Vector2.right * 4);
            bool crossed = false;
            void Receive(PortalTransitEvent e) { if (e.Body == body && e.Phase == PortalTransitPhase.Teleported) crossed = true; }
            PortalEvents.Transit += Receive;
            try
            {
                for (int i=0;i<40;i++) yield return new WaitForFixedUpdate();
                Assert.IsFalse(crossed, "Teleported into an obstructed exit");
                Assert.Less(body.position.x, origin.x+0.12f, "Emerging collider penetrated the remote obstacle");
                Assert.Less(body.linearVelocity.magnitude,0.05f);
            }
            finally { PortalEvents.Transit -= Receive; }
        }

        [UnityTest] public IEnumerator CrossPortalDynamicContactTransfersMomentumBeforeHandoff()
        {
            var a = Portal(origin, Vector2.left, Wall(origin, Vector2.left));
            var exit = origin + Vector2.right * 15;
            var b = Portal(exit, Vector2.right, Wall(exit, Vector2.right)); a.Link(b);
            var source = Body(origin + Vector2.left * 0.8f, Vector2.right * 4); source.mass = 2;
            var target = Body(exit + Vector2.right * 0.6f, Vector2.zero); target.mass = 2;
            source.constraints = target.constraints = RigidbodyConstraints2D.FreezeRotation;
            bool handedOff = false, receivedBeforeHandoff = false;
            void Receive(PortalTransitEvent e) { if (e.Body == source && e.Phase == PortalTransitPhase.Teleported) handedOff = true; }
            PortalEvents.Transit += Receive;
            try
            {
                for (int i=0;i<35;i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (!handedOff && target.linearVelocity.x > 0.5f) receivedBeforeHandoff = true;
                }
                Assert.IsTrue(receivedBeforeHandoff, "The remote body received no contact force until the main body teleported");
                Assert.That(source.mass * source.linearVelocity.x + target.mass * target.linearVelocity.x, Is.EqualTo(8).Within(0.1f));
                Assert.LessOrEqual(source.linearVelocity.sqrMagnitude + target.linearVelocity.sqrMagnitude,16.05f,"Contact created kinetic energy");
            }
            finally { PortalEvents.Transit -= Receive; }
        }
    }
}
