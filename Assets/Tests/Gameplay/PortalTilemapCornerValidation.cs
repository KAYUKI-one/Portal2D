#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Portal2D.Core.Events;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Portal;
using Portal2D.Presentation.Views;
using SeamlessPortals2D;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Portal2D.Development
{
    // Run through MCP in Play Mode; uses the real game prefabs, input events and portal placement.
    public sealed class PortalTilemapCornerValidation : MonoBehaviour
    {
        public string Status = "Running";
        public bool WallExit;
        public float MoveInput=1;
        public readonly List<string> Results = new();
        private readonly List<Object> created = new();
        private void Check(bool value, string message)
        {
            Results.Add((value ? "PASS " : "FAIL ") + message);
            if (!value) Status = "Failed";
        }
        private GameObject Make(string name) { var go = new GameObject(name); created.Add(go); return go; }
        private IEnumerator Start()
        {
            Time.timeScale = 1;
            var inputs = FindObjectsByType<Presentation.Input.PlayerInputAdapter>();
            foreach (var input in inputs) input.enabled = false;
            var existingPlayers = FindObjectsByType<PlayerEntityView>();
            foreach (var other in existingPlayers) other.gameObject.SetActive(false);
            var manager = PortalManager.Instance; manager.ClearAllPortals();
            var crossed = new HashSet<Rigidbody2D>();
            void Receive(PortalTransitEvent e) { if (e.Phase == PortalTransitPhase.Teleported) crossed.Add(e.Body); }
            PortalEvents.Transit += Receive;
            try
            {
                Vector2 origin = new(-1000, -1000);
                var grid = Make("Tilemap corner probe grid"); grid.AddComponent<Grid>(); grid.transform.position = origin;
                var go = Make("Floor pillar and ceiling"); go.transform.SetParent(grid.transform, false); go.layer = 8;
                var map = go.AddComponent<Tilemap>();
                var tile = ScriptableObject.CreateInstance<Tile>(); created.Add(tile); tile.colliderType = Tile.ColliderType.Grid;
                for (int x=-6;x<12;x++) for (int y=-4;y<0;y++) map.SetTile(new Vector3Int(x,y),tile);
                for (int x=0;x<2;x++) for (int y=0;y<3;y++) map.SetTile(new Vector3Int(x,y),tile);
                for (int x=4;x<9;x++) for (int y=5;y<7;y++) map.SetTile(new Vector3Int(x,y),tile);
                if (WallExit) for (int x=-6;x<-4;x++) for (int y=0;y<3;y++) map.SetTile(new Vector3Int(x,y),tile);
                go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                var surface = go.AddComponent<CompositeCollider2D>(); surface.geometryType = CompositeCollider2D.GeometryType.Polygons;
                var tiles = go.AddComponent<TilemapCollider2D>(); tiles.compositeOperation = Collider2D.CompositeOperation.Merge;
                tiles.ProcessTilemapChanges(); surface.GenerateGeometry(); Physics2D.SyncTransforms();
                var a = manager.DeployPortal(PortalType.Blue,origin+new Vector2(0,1),Vector2.left);
                var b = manager.DeployPortal(PortalType.Red,origin+(WallExit ? new Vector2(-4,1) : new Vector2(5,5)),WallExit ? Vector2.right : Vector2.down);
                Check(a != null && b != null && a.IsLinked,"Portal prefabs snap to the Tilemap corners / ceiling");
                if (a == null || b == null) yield break;
                var player = Instantiate(Resources.Load<PlayerEntityView>("Gameplay/Player"),origin+new Vector2(-2.2f,0.65f),Quaternion.identity);
                created.Add(player.gameObject);
                player.gameObject.AddComponent<PortalGunController>().EnsureDependencies();
                var block = PushableBlockEntity.Spawn(origin+new Vector2(-1.1f,0.5f)); created.Add(block.gameObject);
                var bystander = PushableBlockEntity.Spawn(origin+new Vector2(10,0.5f)); created.Add(bystander.gameObject);
                float playerMin = player.Body.position.y, blockMin = block.Rigidbody.position.y;
                float blockBottom=float.PositiveInfinity, maxTilt=0;
                bool simultaneous = false;
                // Walk clear of the exit before checking cleanup; stopping against its face
                // legitimately prepares another transit when the body is still in entry range.
                bool ClearOfExit() => crossed.Contains(player.Body) && (!WallExit || b.SignedDistance(player.Body.position)>0.65f);
                for (int i=0;i<600 && !(ClearOfExit() && crossed.Contains(block.Rigidbody));i++)
                {
                    EventBus.Publish(new PlayerInputAxisCommand(ClearOfExit() ? 0 : MoveInput,false));
                    yield return new WaitForFixedUpdate();
                    simultaneous |= PortalSimulation2D.IsTravelling(player.Body) && PortalSimulation2D.IsTravelling(block.Rigidbody);
                    if (!crossed.Contains(player.Body)) playerMin = Mathf.Min(playerMin,player.Body.position.y);
                    if (!crossed.Contains(block.Rigidbody))
                    {
                        blockMin = Mathf.Min(blockMin,block.Rigidbody.position.y);
                        float angle=block.Rigidbody.rotation*Mathf.Deg2Rad;
                        blockBottom=Mathf.Min(blockBottom,block.Rigidbody.position.y-origin.y-0.475f*(Mathf.Abs(Mathf.Cos(angle))+Mathf.Abs(Mathf.Sin(angle))));
                        maxTilt=Mathf.Max(maxTilt,Mathf.Abs(Mathf.DeltaAngle(0,block.Rigidbody.rotation)));
                    }
                }
                EventBus.Publish(new PlayerInputAxisCommand(0,false));
                Check(crossed.Contains(block.Rigidbody),"Player pushes the real movable block through the wall portal");
                Check(crossed.Contains(player.Body),"Player follows the block through the wall portal");
                Check(playerMin > origin.y+0.55f && blockMin > origin.y+0.40f,$"Both bodies keep floor support before crossing (Player {playerMin-origin.y:F3}, block {blockMin-origin.y:F3})");
                if(WallExit) Check(blockBottom>-0.025f && maxTilt<2f,$"No sinking or spurious rotation while straddling portals (bottom {blockBottom:F4}, tilt {maxTilt:F3})");
                Check(simultaneous,"Two bodies can occupy the shared Tilemap aperture concurrently");
                for (int i=0;i<100;i++) yield return new WaitForFixedUpdate();
                Check(player.Body.position.y > origin.y+0.55f && block.Rigidbody.position.y > origin.y+0.40f,"Player and block remain supported on the floor after exiting");
                Check(bystander.Rigidbody.position.y > origin.y+0.40f,"Unrelated block keeps its original Tilemap collision");
                Check(!Physics2D.GetIgnoreCollision(player.GetComponent<Collider2D>(),surface) && !Physics2D.GetIgnoreCollision(block.Collider,surface),"Original collision pairs restored after both transits");
                Check(FindObjectsByType<PortalCollisionProxy2D>().Length == 0,"All temporary surface colliders cleaned up");
                Check(player.transform.Find("HeldPortalGun") == null && GameObject.Find("HeldPortalGun") == null,"Player has no held portal gun model");
            }
            finally
            {
                PortalEvents.Transit -= Receive;
                EventBus.Publish(new PlayerInputAxisCommand(0,false));
                manager.ClearAllPortals();
                foreach (var obj in created) if (obj != null) Destroy(obj);
                foreach (var other in existingPlayers) if (other != null) other.gameObject.SetActive(true);
                foreach (var input in inputs) if (input != null) input.enabled = true;
                if (Status == "Running") Status = Results.Count > 1 ? "Passed" : "Failed";
            }
        }
    }
}
#endif
