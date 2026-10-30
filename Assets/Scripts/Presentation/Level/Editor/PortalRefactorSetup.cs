using System.IO;
using Portal2D.Core.Data;
using Portal2D.Presentation.Bootstrap;
using Portal2D.Presentation.Gimmicks;
using Portal2D.Presentation.Views;
using SeamlessPortals2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using PortalBody = SeamlessPortals2D.Portal2D;

namespace Portal2D.Presentation.Level.Editor
{
    public static class PortalRefactorSetup
    {
        private const string Gameplay = "Assets/Resources/Gameplay";
        private const string Portable = "Assets/Plugins/Portals2D";
        public static string CreateAssetsAndMigrate()
        {
            Directory.CreateDirectory(Gameplay); Directory.CreateDirectory(Portable + "/Resources/Gameplay");
            Directory.CreateDirectory(Portable + "/Art"); AssetDatabase.Refresh();
            Sprite square = Texture(Portable + "/Art/WhiteSquare.png", false, false);
            Sprite circle = Texture(Gameplay + "/Circle.png", true, false);
            Sprite glow = Texture(Gameplay + "/Glow.png", true, true);
            var slice = Material(Portable + "/Art/PortalSlice.mat", "Portals2D/Sliced Sprite");
            var unlit = Material(Portable + "/Art/UnlitSprite.mat", "Sprites/Default");
            var playerPhysics = PhysicsMaterial(Gameplay + "/PlayerPhysics.physicsMaterial2D", 0);
            var blockPhysics = PhysicsMaterial(Gameplay + "/BlockPhysics.physicsMaterial2D", 0.35f);
            var a = PortalPrefab("PortalA", new Color(0.05f, 0.6f, 1), square, unlit, slice);
            var b = PortalPrefab("PortalB", new Color(1, 0.35f, 0.04f), square, unlit, slice);
            var playerGo = new GameObject("Player"); playerGo.tag = "Player";
            playerGo.layer = 0;
            var rb = playerGo.AddComponent<Rigidbody2D>(); rb.gravityScale = 3; rb.mass = 1;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; rb.linearDamping = 0; rb.angularDamping = 0;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            var box = playerGo.AddComponent<BoxCollider2D>(); box.size = new Vector2(0.65f, 1.25f); box.sharedMaterial = playerPhysics;
            var sr = playerGo.AddComponent<SpriteRenderer>(); sr.sprite = square; sr.color = new Color(0.2f, 0.85f, 0.25f);
            sr.drawMode = SpriteDrawMode.Sliced; sr.size = box.size; sr.sharedMaterial = unlit; sr.sortingLayerName = "Player"; sr.sortingOrder = 10;
            playerGo.AddComponent<PlayerEntityView>(); playerGo.AddComponent<RigidbodyPlayerController>(); playerGo.AddComponent<PlayerUpright>();
            var playerPrefab = PrefabUtility.SaveAsPrefabAsset(playerGo, Gameplay + "/Player.prefab"); Object.DestroyImmediate(playerGo);

            var oldBlock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Gimmicks/Pushable_StoneBlock.prefab");
            var blockGo = oldBlock != null ? (GameObject)PrefabUtility.InstantiatePrefab(oldBlock) : new GameObject("Pushable_StoneBlock");
            if (PrefabUtility.IsPartOfPrefabInstance(blockGo)) PrefabUtility.UnpackPrefabInstance(blockGo, PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
            var block = blockGo.GetComponent<PushableBlockEntity>() ?? blockGo.AddComponent<PushableBlockEntity>();
            block.InitializeComponents(); block.Collider.sharedMaterial = blockPhysics;
            if (block.BlockSprite == null) block.BlockSprite = square;
            blockGo.name = "Pushable_StoneBlock";
            var blockPrefab = PrefabUtility.SaveAsPrefabAsset(blockGo, Gameplay + "/Pushable_StoneBlock.prefab");
            if (oldBlock != null) PrefabUtility.SaveAsPrefabAsset(blockGo, AssetDatabase.GetAssetPath(oldBlock));
            Object.DestroyImmediate(blockGo);

            var orbGo = new GameObject("ActivationOrb", typeof(SpriteRenderer));
            var orbSr = orbGo.GetComponent<SpriteRenderer>(); orbSr.sprite = circle; orbSr.sharedMaterial = unlit;
            orbSr.sortingLayerName = "Item"; orbSr.sortingOrder = 15;
            orbGo.transform.localScale = Vector3.one * 0.45f;
            var haloGo = new GameObject("Glow", typeof(SpriteRenderer)); haloGo.transform.SetParent(orbGo.transform, false); haloGo.transform.localScale = Vector3.one * 2.8f;
            var halo = haloGo.GetComponent<SpriteRenderer>(); halo.sprite = glow; halo.sharedMaterial = unlit; halo.sortingLayerName = "Item"; halo.sortingOrder = 14;
            var orb = orbGo.AddComponent<ActivationOrb>();
            var orbData = new SerializedObject(orb); orbData.FindProperty("halo").objectReferenceValue = halo; orbData.ApplyModifiedPropertiesWithoutUndo(); orb.ResetGimmick();
            var orbPrefab = PrefabUtility.SaveAsPrefabAsset(orbGo, Gameplay + "/ActivationOrb.prefab"); Object.DestroyImmediate(orbGo);

            int migrated = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:LevelAsset"))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelAsset>(AssetDatabase.GUIDToAssetPath(guid));
                for (int i = 0; i < level.SavedItems.Count; i++)
                {
                    var item = level.SavedItems[i]; string name = item.prefab != null ? item.prefab.name : item.gimmickId ?? "";
                    if (name.Contains("Switch") || name.Contains("FloorButton"))
                    {
                        item.prefab = orbPrefab; item.scale = Vector3.one * 0.45f; item.rotation = Quaternion.identity;
                        item.customNormalSprite = null; item.customActiveSprite = null; migrated++;
                    }
                    else if (name.Contains("StoneBlock") || name.Contains("Pushable")) item.prefab = blockPrefab;
                    level.SavedItems[i] = item;
                }
                EditorUtility.SetDirty(level);
            }
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects()) if (root.name == "Amiya") { root.SetActive(false); EditorUtility.SetDirty(root); }
            var existing = GameObject.Find("Player"); if (existing != null) Object.DestroyImmediate(existing);
            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            var spawn = GameObject.Find("PlayerSpawn_Point"); playerInstance.transform.position = spawn != null ? spawn.transform.position : Vector3.zero;
            foreach (var blockInstance in Object.FindObjectsByType<PushableBlockEntity>())
            { blockInstance.InitializeComponents(); blockInstance.Collider.sharedMaterial = blockPhysics; PrefabUtility.RecordPrefabInstancePropertyModifications(blockInstance.Rigidbody); }
            foreach (var old in Object.FindObjectsByType<InteractableObject>())
            {
                if (!old.name.Contains("Switch") && !old.name.Contains("FloorButton")) continue;
                var newOrb = (GameObject)PrefabUtility.InstantiatePrefab(orbPrefab, old.transform.parent);
                newOrb.transform.position = old.transform.position; newOrb.GetComponent<ActivationOrb>().GimmickId = old.GimmickId;
                newOrb.name = "ActivationOrb_" + old.GimmickId; Object.DestroyImmediate(old.gameObject);
            }
            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap != null)
            {
                var data = new SerializedObject(bootstrap); data.FindProperty("playerPrefab").objectReferenceValue = playerPrefab;
                data.FindProperty("scenePlayerCharacter").objectReferenceValue = playerInstance.GetComponent<PlayerEntityView>(); data.ApplyModifiedPropertiesWithoutUndo();
            }
            var manager = Object.FindAnyObjectByType<Portal.PortalManager>();
            if (manager != null)
            {
                var data = new SerializedObject(manager); data.FindProperty("bluePrefab").objectReferenceValue = a.GetComponent<PortalBody>();
                data.FindProperty("redPrefab").objectReferenceValue = b.GetComponent<PortalBody>(); data.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            return $"Created Player, Portal A/B, ActivationOrb and physical block prefabs; migrated {migrated} saved switches. Scene saved.";
        }
        private static GameObject PortalPrefab(string name, Color color, Sprite sprite, Material unlit, Material slice)
        {
            var go = new GameObject(name); go.AddComponent<PortalBody>().Configure(2.2f, slice);
            var rim = new GameObject("Aperture", typeof(SpriteRenderer)); rim.transform.SetParent(go.transform, false);
            rim.transform.localPosition = new Vector3(0, 0.025f, 0); rim.transform.localScale = new Vector3(2.2f, 0.09f, 1);
            var sr = rim.GetComponent<SpriteRenderer>(); sr.sprite = sprite; sr.color = color; sr.sharedMaterial = unlit; sr.sortingLayerName = "Item"; sr.sortingOrder = 30;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Portable + "/Resources/Gameplay/" + name + ".prefab"); Object.DestroyImmediate(go); return prefab;
        }
        private static Material Material(string path, string shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(mat, path); } return mat;
        }
        private static PhysicsMaterial2D PhysicsMaterial(string path, float friction)
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (mat == null) { mat = new PhysicsMaterial2D(); AssetDatabase.CreateAsset(mat, path); }
            mat.friction = friction; mat.bounciness = 0; EditorUtility.SetDirty(mat); return mat;
        }
        private static Sprite Texture(string path, bool circle, bool soft)
        {
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x + 0.5f - 32) / 32, (y + 0.5f - 32) / 32).magnitude;
                float alpha = !circle ? 1 : soft ? Mathf.Pow(Mathf.Clamp01(1 - radius), 2) : Mathf.Clamp01((1 - radius) * 32);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite; importer.spritePixelsPerUnit = 64;
            importer.spriteImportMode = SpriteImportMode.Single; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport(); return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
