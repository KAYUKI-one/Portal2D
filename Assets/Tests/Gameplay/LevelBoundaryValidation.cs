#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Portal2D.Core.Data;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Level;
using Portal2D.Presentation.Views;
using UnityEditor;
using UnityEngine;

namespace Portal2D.Development
{
    public sealed class LevelBoundaryValidation : MonoBehaviour
    {
        public string Status="Running";
        public readonly List<string> Results=new();
        private void Check(bool value,string message)
        { Results.Add((value?"PASS ":"FAIL ")+message); if(!value) Status="Failed"; }
        private IEnumerator Start()
        {
            var level=ScriptableObject.CreateInstance<LevelAsset>();
            var data=new SerializedObject(level);
            var rect=new Rect(3200,3200,16,12);
            data.FindProperty("confineCamera").boolValue=true;
            data.FindProperty("cameraBounds").rectValue=rect; data.ApplyModifiedPropertiesWithoutUndo();
            GameObject root=null, active=null;
            try
            {
                root=level.BuildLevelContent();
                var boundary=root.GetComponentInChildren<LevelBoundary2D>();
                var walls=boundary.GetComponents<BoxCollider2D>();
                Check(walls.Length==4 && boundary.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Static &&
                    System.Array.TrueForAll(walls,c=>c.enabled && !c.isTrigger),"Four solid static map boundary colliders generated");
                Physics2D.SyncTransforms();
                Check(!System.Array.Exists(walls,c=>c.OverlapPoint(rect.center)) &&
                    Mathf.Abs(walls[0].bounds.max.x-rect.xMin)<0.001f && Mathf.Abs(walls[1].bounds.min.x-rect.xMax)<0.001f &&
                    Mathf.Abs(walls[2].bounds.max.y-rect.yMin)<0.001f && Mathf.Abs(walls[3].bounds.min.y-rect.yMax)<0.001f,
                    "Walls lie outside the map with inner faces exactly on its bounds");
                var directions=new[]{Vector2.left,Vector2.right,Vector2.up,Vector2.down,
                    new Vector2(-1,-1),new Vector2(-1,1),new Vector2(1,-1),new Vector2(1,1)};
                foreach(string kind in new[]{"Player","Block","Circle"})
                {
                    Rigidbody2D body;
                    if(kind=="Player")
                    {
                        var player=Instantiate(Resources.Load<PlayerEntityView>("Gameplay/Player"),rect.center,Quaternion.identity);
                        active=player.gameObject; body=player.Body;
                        player.GetComponent<RigidbodyPlayerController>().enabled=false;
                        player.GetComponent<PlayerUpright>().enabled=false;
                    }
                    else if(kind=="Block") { var block=PushableBlockEntity.Spawn(rect.center); active=block.gameObject; body=block.Rigidbody; }
                    else
                    {
                        active=new GameObject("Unmodified boundary test circle"); active.transform.position=rect.center;
                        body=active.AddComponent<Rigidbody2D>(); active.AddComponent<CircleCollider2D>().radius=0.3f;
                        body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
                    }
                    body.gravityScale=0;
                    var shape=active.GetComponent<Collider2D>(); bool contained=true, allDirectionsContact=true;
                    float greatestOvershoot=0;
                    foreach(var direction in directions)
                    {
                        body.position=rect.center; body.rotation=0; body.angularVelocity=0; body.linearVelocity=direction.normalized*70;
                        Physics2D.SyncTransforms();
                        bool touched=false;
                        for(int i=0;i<30;i++)
                        {
                            yield return new WaitForFixedUpdate();
                            var bounds=shape.bounds;
                            contained &= bounds.min.x>=rect.xMin-0.03f && bounds.max.x<=rect.xMax+0.03f &&
                                bounds.min.y>=rect.yMin-0.03f && bounds.max.y<=rect.yMax+0.03f;
                            greatestOvershoot=Mathf.Max(greatestOvershoot,Mathf.Max(Mathf.Max(rect.xMin-bounds.min.x,bounds.max.x-rect.xMax),
                                Mathf.Max(rect.yMin-bounds.min.y,bounds.max.y-rect.yMax)));
                            touched |= System.Array.Exists(walls,wall=>shape.IsTouching(wall));
                        }
                        allDirectionsContact &= touched;
                    }
                    // A freely rotating circle may roll away after contact; zero speed is not a boundary requirement.
                    Check(contained && allDirectionsContact,$"{kind} contacts all four edges and corners at 70 units/s, remains inside (overshoot {greatestOvershoot:F4})");
                    Destroy(active); active=null; yield return null;
                }
                level.SaveFromScene(root.transform);
                Check(level.SavedItems.Count==0 && level.TilemapLayers.Count==0,"Derived boundary is not saved as a prefab placement");
                var resized=new Rect(3202,3203,12,8);
                data.Update(); data.FindProperty("cameraBounds").rectValue=resized; data.ApplyModifiedPropertiesWithoutUndo();
                boundary.Refresh(); Physics2D.SyncTransforms();
                Check(Mathf.Abs(walls[0].bounds.max.x-resized.xMin)<0.001f && Mathf.Abs(walls[3].bounds.min.y-resized.yMax)<0.001f,
                    "Changing the map rectangle updates physical walls");
                data.Update(); data.FindProperty("confineCamera").boolValue=false; data.ApplyModifiedPropertiesWithoutUndo(); boundary.Refresh();
                Check(System.Array.TrueForAll(walls,c=>!c.enabled),"Disabling bounds removes all four barriers");
                data.Update(); data.FindProperty("confineCamera").boolValue=true;
                data.FindProperty("cameraBounds").rectValue=new Rect(0,0,-1,8); data.ApplyModifiedPropertiesWithoutUndo(); boundary.Refresh();
                Check(System.Array.TrueForAll(walls,c=>!c.enabled),"Invalid rectangles do not create malformed colliders");
                Destroy(root); root=null; yield return null;
                Check(boundary==null,"Unloading the level removes its boundary colliders");
            }
            finally
            {
                if(active!=null) Destroy(active); if(root!=null) Destroy(root); Destroy(level);
                if(Status=="Running") Status="Passed";
            }
        }
    }
}
#endif
