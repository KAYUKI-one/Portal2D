#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Portal2D.Core.Events;
using Portal2D.Presentation.Views;
using SeamlessPortals2D;
using UnityEngine;
using PortalBody=SeamlessPortals2D.Portal2D;
namespace Portal2D.Development
{
    public sealed class PortalInputValidation : MonoBehaviour
    {
        public string Status="Running";
        public readonly List<string> Results=new();
        private void Check(bool value,string label) { Results.Add((value?"PASS ":"FAIL ")+label); if(!value) Status="Failed"; }
        private IEnumerator Start()
        {
            Time.timeScale=1;
            var inputs=FindObjectsByType<Presentation.Input.PlayerInputAdapter>(); foreach(var input in inputs) input.enabled=false;
            var players=FindObjectsByType<PlayerEntityView>(); foreach(var player in players) player.gameObject.SetActive(false);
            var created=new List<GameObject>(); Vector2 origin=new(2000,2000);
            var floor=new GameObject("Two floor portal regression"); created.Add(floor); floor.transform.position=origin+new Vector2(3,-0.5f);
            var surface=floor.AddComponent<BoxCollider2D>(); surface.size=new Vector2(14,1);
            var a=new GameObject("Input portal A").AddComponent<PortalBody>(); created.Add(a.gameObject); a.Place(origin,Vector2.up,new[]{surface});
            var b=new GameObject("Input portal B").AddComponent<PortalBody>(); created.Add(b.gameObject); b.Place(origin+Vector2.right*5,Vector2.up,new[]{surface}); a.Link(b);
            var actor=Instantiate(Resources.Load<PlayerEntityView>("Gameplay/Player"),origin+Vector2.up*0.2f,Quaternion.identity); created.Add(actor.gameObject);
            actor.Body.gravityScale=0;
            try
            {
                for(int i=0;i<3;i++) yield return new WaitForFixedUpdate();
                Check(PortalSimulation2D.IsTravelling(actor.Body),"Player is straddling the floor portal");
                EventBus.Publish(new PlayerInputAxisCommand(1,false));
                for(int i=0;i<8;i++) yield return new WaitForFixedUpdate();
                Check(actor.Body.linearVelocity.x>1,"Right input still works while straddling");
                EventBus.Publish(new PlayerInputAxisCommand(-1,false));
                for(int i=0;i<18;i++) yield return new WaitForFixedUpdate();
                Check(actor.Body.linearVelocity.x < -0.5f,"Left input can reverse movement while straddling");
                Check(actor.Body.position.x<origin.x+2 && actor.Body.position.y>origin.y,"Authority stays at the entrance while part of the body remains there");
                Check(Mathf.Abs(actor.Body.linearVelocity.y)<0.01f,"Portal adds no vertical force to the floating rigidbody");
            }
            finally
            {
                EventBus.Publish(new PlayerInputAxisCommand(0,false)); a.Unlink();
                foreach(var go in created) if(go!=null) Destroy(go);
                foreach(var player in players) if(player!=null) player.gameObject.SetActive(true);
                foreach(var input in inputs) if(input!=null) input.enabled=true;
                if(Status=="Running") Status="Passed";
            }
        }
    }
}
#endif
