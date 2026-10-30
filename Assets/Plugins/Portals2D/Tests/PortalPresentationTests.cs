using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SeamlessPortals2D.Tests
{
    public partial class PortalPhysicsTests
    {
        [UnityTest] public IEnumerator InterpolatedBodyPublishesTheDestinationRenderPoseAtHandoff()
        {
            var a=Portal(origin,Vector2.left,Wall(origin,Vector2.left));
            var exit=origin+new Vector2(15,6);
            var b=Portal(exit,Vector2.down,Wall(exit,Vector2.down)); a.Link(b);
            var body=Body(origin+Vector2.left,Vector2.right*3);
            body.interpolation=RigidbodyInterpolation2D.Interpolate;
            bool crossed=false; float renderError=0, rotationError=0;
            void Receive(PortalTransitEvent e)
            {
                if(e.Body!=body || e.Phase!=PortalTransitPhase.Teleported) return;
                crossed=true;
                renderError=Vector2.Distance(body.transform.position,body.position);
                rotationError=Mathf.Abs(Mathf.DeltaAngle(body.transform.eulerAngles.z,body.rotation));
            }
            PortalEvents.Transit+=Receive;
            try
            {
                for(int i=0;i<70 && !crossed;i++) yield return new WaitForFixedUpdate();
                Assert.IsTrue(crossed);
                Assert.Less(renderError,0.001f,"Rendering still uses the old chart when the clone is removed");
                Assert.Less(rotationError,0.001f,"Rendering still uses the source rotation");
                for(int frame=0;frame<12;frame++)
                {
                    yield return null;
                    Assert.Less(Vector2.Distance(body.transform.position,body.position),0.15f,"An interpolation frame traversed the space between portals");
                }
                for(int i=0;i<3;i++) yield return new WaitForFixedUpdate();
                Assert.That(body.interpolation,Is.EqualTo(RigidbodyInterpolation2D.Interpolate));
            }
            finally { PortalEvents.Transit-=Receive; }
        }
    }
}
