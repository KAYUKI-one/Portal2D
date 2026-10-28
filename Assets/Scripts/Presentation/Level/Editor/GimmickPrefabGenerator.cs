using System.IO;
using Portal2D.Presentation.Gimmicks;
using UnityEditor;
using UnityEngine;

namespace Portal2D.Presentation.Level.Editor
{
    public static class GimmickPrefabGenerator
    {
        [MenuItem("Portal2D/Gimmicks/生成示例可交互预制体 (Sample Prefabs)", false, 50)]
        public static void GenerateSamplePrefabs()
        {
            string textureFolder = "Assets/Resource/Gimmicks";
            string prefabFolder = "Assets/Prefab/Gimmicks";

            if (!Directory.Exists(textureFolder)) Directory.CreateDirectory(textureFolder);
            if (!Directory.Exists(prefabFolder)) Directory.CreateDirectory(prefabFolder);

            // 1. 生成持久化 PNG 贴图资源 (避免运行时生成导致 Prefab 贴图为 null)
            string floorTexPath = $"{textureFolder}/Sprite_FloorButton.png";
            string switchTexPath = $"{textureFolder}/Sprite_WallSwitch.png";

            CreateAndSavePng(floorTexPath, 32, 16, new Color(0.95f, 0.65f, 0.15f), Color.white, 2);
            CreateAndSavePng(switchTexPath, 24, 32, new Color(0.35f, 0.75f, 1f), Color.white, 2);

            AssetDatabase.Refresh();

            // 确保纹理被正确导入为 Sprite (2D and UI)
            ConfigureTextureAsSprite(floorTexPath);
            ConfigureTextureAsSprite(switchTexPath);

            var floorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(floorTexPath);
            var switchSprite = AssetDatabase.LoadAssetAtPath<Sprite>(switchTexPath);

            // 2. 生成扁平化、无缩放失真的 踩踏式地砖按钮 Prefab
            CreateFloorButtonPrefab($"{prefabFolder}/Interactive_FloorButton.prefab", floorSprite);

            // 3. 生成 交互式墙面开关 Prefab (按 E 交互)
            CreateWallSwitchPrefab($"{prefabFolder}/Interactive_WallSwitch.prefab", switchSprite);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[GimmickPrefabGenerator] 示例机关预制体生成完毕！已存入 Assets/Prefab/Gimmicks，贴图已持久化！");
        }

        private static void CreateAndSavePng(string filePath, int width, int height, Color fill, Color border, int borderWidth)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isBorder = (x < borderWidth || x >= width - borderWidth ||
                                     y < borderWidth || y >= height - borderWidth);
                    tex.SetPixel(x, y, isBorder ? border : fill);
                }
            }
            tex.Apply();
            byte[] bytes = tex.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
            Object.DestroyImmediate(tex);
        }

        private static void ConfigureTextureAsSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 24;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static void CreateFloorButtonPrefab(string path, Sprite sprite)
        {
            var go = new GameObject("Interactive_FloorButton");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = 10;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = sprite != null ? sprite.bounds.size : new Vector2(1.33f, 0.67f);
            col.offset = Vector2.zero;
            col.isTrigger = true;

            var interactable = go.AddComponent<InteractableObject>();
            interactable.GimmickId = "FloorButton";

            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("triggerType").enumValueIndex = (int)InteractionTriggerType.PlayerStepOrTouch;
            serialized.FindProperty("behaviorMode").enumValueIndex = (int)Portal2D.Presentation.Gimmicks.InteractionMode.HoldWhileTouching;
            serialized.FindProperty("visualTransform").objectReferenceValue = go.transform;
            serialized.FindProperty("targetRenderer").objectReferenceValue = sr;
            serialized.FindProperty("normalSprite").objectReferenceValue = sprite;
            serialized.FindProperty("pressYOffset").floatValue = -0.06f;
            serialized.FindProperty("normalColor").colorValue = Color.white;
            serialized.FindProperty("activeColor").colorValue = new Color(0.2f, 1f, 0.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }

        private static void CreateWallSwitchPrefab(string path, Sprite sprite)
        {
            var go = new GameObject("Interactive_WallSwitch");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.white;
            sr.sortingOrder = 10;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = sprite != null ? sprite.bounds.size : new Vector2(1f, 1.33f);
            col.offset = Vector2.zero;
            col.isTrigger = true;

            var interactable = go.AddComponent<InteractableObject>();
            interactable.GimmickId = "WallSwitch";

            var serialized = new SerializedObject(interactable);
            serialized.FindProperty("triggerType").enumValueIndex = (int)InteractionTriggerType.PlayerInteractKey;
            serialized.FindProperty("behaviorMode").enumValueIndex = (int)Portal2D.Presentation.Gimmicks.InteractionMode.Toggle;
            serialized.FindProperty("visualTransform").objectReferenceValue = go.transform;
            serialized.FindProperty("targetRenderer").objectReferenceValue = sr;
            serialized.FindProperty("normalSprite").objectReferenceValue = sprite;
            serialized.FindProperty("pressYOffset").floatValue = 0f;
            serialized.FindProperty("normalColor").colorValue = Color.white;
            serialized.FindProperty("activeColor").colorValue = new Color(0.2f, 1f, 0.5f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
    }
}
