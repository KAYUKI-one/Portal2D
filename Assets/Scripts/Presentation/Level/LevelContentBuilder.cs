using UnityEngine;
using UnityEngine.Tilemaps;
using Portal2D.Core.Data;
using Portal2D.Presentation.Gimmicks;
namespace Portal2D.Presentation.Level
{
    public static class LevelContentBuilder
    {
        public static void SaveFromScene(this LevelAsset level, Transform root)
        {
            if (root == null) return;
            var layers = new System.Collections.Generic.List<SavedTilemapLayer>();
            var items = new System.Collections.Generic.List<SavedItem>();
            foreach (var map in root.GetComponentsInChildren<Tilemap>(true))
            {
                var data = new SavedTilemapLayer { layerName = map.name, layer = map.gameObject.layer,
                    sortingOrder = map.GetComponent<TilemapRenderer>().sortingOrder, tiles = new() };
                foreach (var pos in map.cellBounds.allPositionsWithin)
                    if (map.HasTile(pos)) data.tiles.Add(new SavedTile { position = pos, tile = map.GetTile(pos) });
                layers.Add(data);
            }
#if UNITY_EDITOR
            var ids = new System.Collections.Generic.HashSet<string>();
            foreach (Transform child in root)
            {
                if (child.GetComponent<Grid>() != null || child.GetComponent<LevelPointMarker>() != null ||
                    child.GetComponent<LevelBackground2D>() != null || child.GetComponent<LevelBoundary2D>() != null) continue;
                var source = UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                var gimmick = child.GetComponent<PuzzleGimmickBase>();
                if (source == null)
                {
                    string key = child.GetComponent<ActivationOrb>() != null ? "ActivationOrb" : child.GetComponent<PushableBlockEntity>() != null ? "Pushable_StoneBlock" : null;
                    if (key != null) source = Resources.Load<GameObject>("Gameplay/" + key);
                }
                if (source == null) throw new System.InvalidOperationException($"{child.name} 没有预制体来源。请先保存为预制体，再保存关卡。");
                if (gimmick != null)
                {
                    if (string.IsNullOrEmpty(gimmick.GimmickId) || gimmick.GimmickId == "Gimmick_01" || !ids.Add(gimmick.GimmickId))
                    {
                        UnityEditor.Undo.RecordObject(gimmick, "Assign unique level object ID");
                        gimmick.GimmickId = System.Guid.NewGuid().ToString("N"); ids.Add(gimmick.GimmickId);
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(gimmick);
                    }
                }
                var sr = child.GetComponent<SpriteRenderer>();
                items.Add(new SavedItem { prefab = source, position = child.position, rotation = child.rotation,
                    scale = child.localScale, gimmickId = gimmick != null ? gimmick.GimmickId : child.name,
                    componentStates = LevelObjectSerialization.Capture(child.gameObject),
                    customNormalSprite = sr != null ? sr.sprite : null, customNormalColor = sr != null ? sr.color : Color.white });
            }
#endif
            level.TilemapLayers.Clear(); level.TilemapLayers.AddRange(layers);
            level.SavedItems.Clear(); level.SavedItems.AddRange(items);
        }
        public static GameObject BuildLevelContent(this LevelAsset level)
        {
            if (level.TilemapLayers.Count == 0 && level.SavedItems.Count == 0 && level.LevelContentPrefab != null)
            {
                var legacy=Object.Instantiate(level.LevelContentPrefab);
                LevelBackground2D.Create(legacy,level); LevelBoundary2D.Ensure(legacy,level); return legacy;
            }
            var root = new GameObject("[LevelContent]_" + level.LevelId);
            LevelBackground2D.Create(root,level);
            LevelBoundary2D.Ensure(root,level);
            var grid = new GameObject("Grid", typeof(Grid)); grid.transform.SetParent(root.transform, false);
            foreach (var data in level.TilemapLayers)
            {
                var go = new GameObject(data.layerName, typeof(Tilemap), typeof(TilemapRenderer));
                go.transform.SetParent(grid.transform, false); go.layer = data.layer;
                var map = go.GetComponent<Tilemap>(); var renderer = go.GetComponent<TilemapRenderer>();
                renderer.sortingOrder = data.sortingOrder; renderer.sortingLayerName = "Tilemap";
                if (data.tiles != null) foreach (var tile in data.tiles) if (tile.tile != null) map.SetTile(tile.position, tile.tile);
                if (data.layerName == "Ground" || data.layerName == "Platforms" || data.layer == Core.Constants.GameLayers.ObstaclesLayer || data.layer == Core.Constants.GameLayers.OneWayPlatformLayer)
                {
                    go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                    var composite = go.AddComponent<CompositeCollider2D>(); composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                    var collider = go.AddComponent<TilemapCollider2D>(); collider.compositeOperation = Collider2D.CompositeOperation.Merge;
                    collider.ProcessTilemapChanges(); composite.GenerateGeometry();
                    if (data.layerName == "Platforms" || data.layer == Core.Constants.GameLayers.OneWayPlatformLayer)
                    {
                        var effector = go.AddComponent<PlatformEffector2D>(); effector.useOneWay = true; composite.usedByEffector = true;
                    }
                }
            }
            foreach (var data in level.SavedItems)
            {
                if (data.prefab == null) { Debug.LogWarning($"Missing prefab in {level.name}: {data.gimmickId}"); continue; }
                GameObject go;
#if UNITY_EDITOR
                if (!Application.isPlaying) { go = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(data.prefab); go.transform.SetParent(root.transform); }
                else
#endif
                    go = Object.Instantiate(data.prefab, root.transform);
                go.name = data.gimmickId;
                go.transform.SetPositionAndRotation(data.position, data.rotation);
                go.transform.localScale = data.scale == Vector3.zero ? Vector3.one : data.scale;
                var gimmick = go.GetComponent<PuzzleGimmickBase>();
                if (gimmick != null) { gimmick.GimmickId = data.gimmickId; gimmick.ResetGimmick(); }
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null && data.customNormalSprite != null && go.GetComponent<ActivationOrb>() == null)
                { sr.sprite = data.customNormalSprite; sr.color = data.customNormalColor; }
                LevelObjectSerialization.Restore(go,data.componentStates);
            }
            Physics2D.SyncTransforms();
            return root;
        }
    }
}
