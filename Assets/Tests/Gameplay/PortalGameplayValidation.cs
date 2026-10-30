#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.CameraControl;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Level;
using Portal2D.Presentation.Views;
using SeamlessPortals2D;
using UnityEngine;
using PortalBody = SeamlessPortals2D.Portal2D;

namespace Portal2D.Development
{
    // Editor-only integration probe, invoked through MCP after entering Play Mode.
    public sealed class PortalGameplayValidation : MonoBehaviour
    {
        public string Status = "Running";
        public readonly List<string> Results = new();
        private readonly List<Object> created = new();
        private GameObject Make(string name) { var go = new GameObject(name); created.Add(go); return go; }
        private void Check(bool condition, string label)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + label);
            if (!condition) Status = "Failed";
        }
        private PortalBody Portal(Vector2 point, Vector2 normal, string resource)
        {
            var wall = Make("Probe wall"); wall.transform.position = point - normal * 0.5f;
            wall.transform.rotation = Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.up,normal));
            wall.AddComponent<BoxCollider2D>().size = new Vector2(8,1);
            var p = Instantiate(Resources.Load<PortalBody>("Gameplay/"+resource)); created.Add(p.gameObject);
            p.Place(point,normal,new[] {wall.GetComponent<Collider2D>()}); return p;
        }
        private IEnumerator Start()
        {
            Time.timeScale = 1;
            var inputs = FindObjectsByType<Presentation.Input.PlayerInputAdapter>();
            foreach (var input in inputs) input.enabled = false;
            Vector2 origin = new(1000,1000);
            var prefab = Resources.Load<PlayerEntityView>("Gameplay/Player");
            var walkingOrigin = origin + Vector2.left * 20;
            var ground = Make("Movement probe ground"); ground.transform.position = walkingOrigin + Vector2.down * 0.5f;
            ground.AddComponent<BoxCollider2D>().size = new Vector2(12, 1);
            var player = Instantiate(prefab, walkingOrigin + Vector2.up * 0.65f, Quaternion.identity); created.Add(player.gameObject);
            var pushedBlock = PushableBlockEntity.Spawn(walkingOrigin + new Vector2(2,0.5f)); created.Add(pushedBlock.gameObject);
            for(int i=0;i<15;i++) yield return new WaitForFixedUpdate();
            for(int i=0;i<45;i++) { EventBus.Publish(new PlayerInputAxisCommand(1,false)); yield return new WaitForFixedUpdate(); }
            Check(player.Body.position.x > walkingOrigin.x + 1,"Movement input drives the physical Player");
            Check(pushedBlock.Rigidbody.position.x > walkingOrigin.x + 2.15f,"Player pushes the standard physical block");
            EventBus.Publish(new PlayerInputAxisCommand(0,false,true));
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            Check(player.Body.linearVelocity.y > 5,"Grounded Player jumps through the input event pipeline");
            EventBus.Publish(new PlayerInputAxisCommand(0,false));
            player.ResetAt(origin + Vector2.up * 1.5f);
            player.Body.gravityScale = 0; player.Body.linearVelocity = Vector2.down * 12;
            var a = Portal(origin,Vector2.up,"PortalA"); var b = Portal(origin + Vector2.right * 20,Vector2.right,"PortalB"); a.Link(b);
            var camGo = Make("Probe camera"); camGo.AddComponent<Camera>();
            var follow = camGo.AddComponent<SmoothCameraController>(); follow.Initialize(player.transform,4);
            bool crossed = false; Vector2 outgoing = default; float rotation = 0;
            void Receive(PortalTransitEvent e) { if (e.Body == player.Body && e.Phase == PortalTransitPhase.Teleported) { crossed = true; outgoing = e.Velocity; rotation = player.Body.rotation; } }
            PortalEvents.Transit += Receive;
            for(int i=0;i<80 && !crossed;i++) yield return new WaitForFixedUpdate();
            PortalEvents.Transit -= Receive;
            Check(crossed,"Player prefab traverses floor to wall");
            Check(Vector2.Distance(outgoing,Vector2.right * 12)<0.05f,"Player momentum rotates without clamping");
            Check(Mathf.Abs(Mathf.DeltaAngle(rotation,90))<0.05f,"Player body rotates with portal");
            for(int i=0;i<45;i++) yield return new WaitForFixedUpdate();
            Check(Mathf.Abs(Mathf.DeltaAngle(player.Body.rotation,0))<0.1f,"Player automatically stands upright");
            Check(Quaternion.Angle(camGo.transform.rotation,Quaternion.identity)<0.01f,"Camera stays upright");
            Check(player.Body.linearVelocity.x>11.9f,"Air motion retains launch momentum");
            Check(player.SpriteRenderer.sharedMaterial.shader.name=="Sprites/Default","Sprite material restored after passage");
            Check(player.GetComponentsInChildren<Animator>().Length==0,"Geometric Player has no animator");

            var level = ScriptableObject.CreateInstance<LevelAsset>(); created.Add(level);
            var root = Make("Probe level");
            var orbPrefab = Resources.Load<ActivationOrb>("Gameplay/ActivationOrb");
            var orb1 = Instantiate(orbPrefab,origin,Quaternion.identity,root.transform); orb1.GimmickId="Probe_1";
            var orb2 = Instantiate(orbPrefab,origin+Vector2.right*3,Quaternion.identity,root.transform); orb2.GimmickId="Probe_2";
            var goal = Make("Probe goal").AddComponent<GoalExitView>(); goal.Setup(level);
            var rules = root.AddComponent<LevelRuleController>(); rules.Setup(level,goal);
            Check(!goal.IsUnlocked,"Exit starts locked with two orbs");
            EventBus.Publish(new ActivateOrbRequest(orb1.gameObject,origin+Vector2.up*5));
            Check(!orb1.IsActivated,"Orb rejects out-of-range interaction");
            EventBus.Publish(new ActivateOrbRequest(orb1.gameObject,origin));
            Check(orb1.IsActivated && !orb2.IsActivated && !goal.IsUnlocked,"One targeted interaction activates one orb");
            EventBus.Publish(new ActivateOrbRequest(orb2.gameObject,origin+Vector2.right*3));
            Check(goal.IsUnlocked && rules.IsWinConditionMet,"All orbs activate the exit");
            rules.ResetLevelRules(); Check(!goal.IsUnlocked && !orb1.IsActivated && !orb2.IsActivated,"Reset restores orb and exit state");
            var block = PushableBlockEntity.Spawn(origin+Vector2.up*5); created.Add(block.gameObject);
            Check(block.Rigidbody.bodyType==RigidbodyType2D.Dynamic && !block.Collider.isTrigger && block.Rigidbody.linearDamping==0,"Movable block prefab uses standard dynamic physics");
            block.Rigidbody.gravityScale=0; block.Rigidbody.linearVelocity=Vector2.right*10;
            for(int i=0;i<15;i++) yield return new WaitForFixedUpdate();
            Check(Mathf.Abs(block.Rigidbody.linearVelocity.x-10)<0.05f,"Block has no scripted air damping");
            foreach(var obj in created) if(obj!=null) Destroy(obj);
            foreach (var input in inputs) if (input != null) input.enabled = true;
            if(Status!="Failed") Status="Passed";
        }
    }
}
#endif
