#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Portal2D.Core.Events;
using Portal2D.Presentation.CameraControl;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Views;
using SeamlessPortals2D;
using UnityEngine;

namespace Portal2D.Development
{
    [DefaultExecutionOrder(9999)]
    public sealed class PortalExperienceValidation : MonoBehaviour
    {
        public string Status="Running";
        public readonly List<string> Results=new();
        private Transform monitoredCamera;
        private Vector3 cameraBeforeSimulation;
        private void FixedUpdate() { if(monitoredCamera!=null) cameraBeforeSimulation=monitoredCamera.position; }
        private void Check(bool value,string message)
        { Results.Add((value?"PASS ":"FAIL ")+message); if(!value) Status="Failed"; }
        private IEnumerator Start()
        {
            Time.timeScale=1;
            var inputs=FindObjectsByType<Presentation.Input.PlayerInputAdapter>();
            foreach(var input in inputs) input.enabled=false;
            var players=FindObjectsByType<PlayerEntityView>();
            foreach(var other in players) other.gameObject.SetActive(false);
            var created=new List<GameObject>();
            var origin=new Vector2(2400,2400);
            try
            {
                var floor=new GameObject("Stack push floor"); created.Add(floor);
                floor.transform.position=origin+new Vector2(3,-0.5f);
                floor.AddComponent<BoxCollider2D>().size=new Vector2(30,1);
                var player=Instantiate(Resources.Load<PlayerEntityView>("Gameplay/Player"),origin+new Vector2(-1.2f,0.65f),Quaternion.identity);
                created.Add(player.gameObject);
                var lower=PushableBlockEntity.Spawn(origin+new Vector2(0,0.49f)); created.Add(lower.gameObject);
                var upper=PushableBlockEntity.Spawn(origin+new Vector2(0,1.46f)); created.Add(upper.gameObject);
                for(int i=0;i<30;i++) yield return new WaitForFixedUpdate();
                float lowerStart=lower.Rigidbody.position.x, upperStart=upper.Rigidbody.position.x;
                for(int i=0;i<180;i++)
                { EventBus.Publish(new PlayerInputAxisCommand(1,false)); yield return new WaitForFixedUpdate(); }
                EventBus.Publish(new PlayerInputAxisCommand(0,false));
                Check(lower.Rigidbody.position.x-lowerStart>1 && upper.Rigidbody.position.x-upperStart>1,
                    $"Push two stacked standard blocks: lower {lower.Rigidbody.position.x-lowerStart:F2}, upper {upper.Rigidbody.position.x-upperStart:F2}");
                Check(upper.Rigidbody.position.y-lower.Rigidbody.position.y>0.7f,"Stack remains standing while pushed");

                var cameraObject=new GameObject("Camera experience probe",typeof(Camera)); created.Add(cameraObject);
                var camera=cameraObject.GetComponent<Camera>(); camera.enabled=false; camera.orthographic=true; camera.aspect=16f/9;
                var follow=cameraObject.AddComponent<SmoothCameraController>(); follow.Initialize(player.transform,4);
                monitoredCamera=cameraObject.transform;
                player.GetComponent<RigidbodyPlayerController>().enabled=false;
                player.GetComponent<PlayerUpright>().enabled=false;
                player.Body.gravityScale=0;
                SeamlessPortals2D.Portal2D PortalAt(Vector2 position)
                {
                    var wall=new GameObject("Camera test support"); created.Add(wall);
                    wall.transform.position=position+Vector2.down*0.5f;
                    var shape=wall.AddComponent<BoxCollider2D>(); shape.size=new Vector2(5,1);
                    var portal=new GameObject("Camera test portal").AddComponent<SeamlessPortals2D.Portal2D>(); created.Add(portal.gameObject);
                    portal.Place(position,Vector2.up,new[]{shape}); return portal;
                }
                var a=PortalAt(origin+new Vector2(0,5)); var b=PortalAt(origin+new Vector2(15,5)); a.Link(b);
                float biggestEventJump=0;
                int crossings=0;
                void OnTransit(PortalTransitEvent e)
                {
                    if(e.Body!=player.Body || e.Phase!=PortalTransitPhase.Teleported) return;
                    crossings++; biggestEventJump=Mathf.Max(biggestEventJump,Vector3.Distance(cameraBeforeSimulation,cameraObject.transform.position));
                }
                PortalEvents.Transit+=OnTransit;
                try
                {
                for(int i=0;i<6;i++)
                {
                    var entry=i%2==0?a:b;
                    player.Body.position=(Vector2)entry.transform.position+Vector2.up;
                    player.Body.rotation=0; player.Body.linearVelocity=Vector2.down*12;
                    int count=crossings;
                    for(int tick=0;tick<30 && crossings==count;tick++)
                    { yield return new WaitForFixedUpdate(); }
                }
                }
                finally { PortalEvents.Transit-=OnTransit; a.Unlink(); }
                Check(crossings==6,"Six rapid physical portal crossings exercised");
                Check(biggestEventJump<0.001f,$"Repeated teleport events do not snap the camera ({biggestEventJump:F3})");
                Check(Quaternion.Angle(cameraObject.transform.rotation,Quaternion.identity)<0.001f,"Camera remains upright");
                player.Body.simulated=false;
                var bounds=new Rect(origin.x-4,origin.y-3,18,10);
                follow.ConfigureEnvironment(true,bounds,Color.magenta);
                follow.Initialize(player.transform,20);
                bool inside=true;
                foreach(float aspect in new[]{16f/9,9f/16,3f})
                {
                    camera.aspect=aspect;
                    foreach(var corner in new[]{bounds.min-Vector2.one*5,bounds.max+Vector2.one*5})
                    {
                        player.transform.position=corner;
                        for(int frame=0;frame<12;frame++)
                        {
                            yield return null;
                            float h=camera.orthographicSize,w=h*camera.aspect;
                            var p=cameraObject.transform.position;
                            inside &= p.x-w>=bounds.xMin-0.002f && p.x+w<=bounds.xMax+0.002f && p.y-h>=bounds.yMin-0.002f && p.y+h<=bounds.yMax+0.002f;
                        }
                    }
                }
                Check(inside,"Entire viewport stays within map bounds at wide, tall and ultrawide aspect ratios");
                Check(camera.backgroundColor==Color.magenta,"Level background color is applied");
                follow.ConfigureEnvironment(false,bounds,Color.blue);
                follow.Initialize(player.transform,4);
                Check(camera.orthographicSize==4,"Next unbounded level releases previous camera limits");
            }
            finally
            {
                EventBus.Publish(new PlayerInputAxisCommand(0,false));
                foreach(var go in created) if(go!=null) Destroy(go);
                foreach(var other in players) if(other!=null) other.gameObject.SetActive(true);
                foreach(var input in inputs) if(input!=null) input.enabled=true;
                if(Status=="Running") Status="Passed";
            }
        }
    }
}
#endif
