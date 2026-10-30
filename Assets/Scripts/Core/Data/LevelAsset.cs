using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
namespace Portal2D.Core.Data
{
    public enum LevelLoseCondition { FallBelowDeathLine, None }
    [System.Serializable] public struct SavedTile { public Vector3Int position; public TileBase tile; }
    [System.Serializable] public struct SavedTilemapLayer
    {
        public string layerName; public int sortingOrder, layer; public List<SavedTile> tiles;
    }
    [System.Serializable] public struct SavedItem
    {
        public GameObject prefab; public Vector3 position; public Quaternion rotation; public Vector3 scale;
        public string gimmickId; public Sprite customNormalSprite, customActiveSprite; public Color customNormalColor, customActiveColor;
        public List<SavedLevelObjectState> componentStates;
    }
    [CreateAssetMenu(fileName = "NewLevel", menuName = "Portal2D/Level Asset")]
    public sealed class LevelAsset : ScriptableObject
    {
        [SerializeField] private string levelId = "Level_01", levelDisplayName = "新关卡";
        [SerializeField] private Vector2 playerSpawnPosition, goalPosition = new(7, 0), goalTriggerSize = new(1.5f, 2.5f);
        [SerializeField] private LevelLoseCondition loseCondition;
        [SerializeField] private float fallDeathY = -12, initialCameraSize = 6;
        [SerializeField] private LevelAsset nextLevel;
        [Header("地图边界与背景")]
        [SerializeField] private bool confineCamera;
        [SerializeField] private Rect cameraBounds = new(-20, -10, 40, 25);
        [SerializeField] private Color backgroundColor = new(0.19215687f, 0.3019608f, 0.4745098f, 1);
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Color backgroundTint = Color.white;
        [SerializeField] private Rect backgroundRect = new(-20, -10, 40, 25);
        [SerializeField, HideInInspector] private List<SavedTilemapLayer> tilemapLayers = new();
        [SerializeField, HideInInspector] private List<SavedItem> savedItems = new();
        [SerializeField, HideInInspector] private GameObject levelContentPrefab;
        public string LevelId => levelId;
        public string LevelDisplayName => levelDisplayName;
        public Vector2 PlayerSpawnPosition => playerSpawnPosition;
        public Vector2 GoalPosition => goalPosition;
        public Vector2 GoalTriggerSize => goalTriggerSize;
        public LevelLoseCondition LoseCondition => loseCondition;
        public float FallDeathY => fallDeathY;
        public float InitialCameraSize => initialCameraSize;
        public LevelAsset NextLevel => nextLevel;
        // Keep the serialized field names so existing level bounds are preserved.
        public bool HasMapBounds => confineCamera && cameraBounds.width>0 && cameraBounds.height>0 &&
            float.IsFinite(cameraBounds.xMin) && float.IsFinite(cameraBounds.xMax) &&
            float.IsFinite(cameraBounds.yMin) && float.IsFinite(cameraBounds.yMax);
        public Rect MapBounds => cameraBounds;
        public bool ConfineCamera => HasMapBounds;
        public Rect CameraBounds => cameraBounds;
        public Color BackgroundColor => backgroundColor;
        public Sprite BackgroundSprite => backgroundSprite;
        public Color BackgroundTint => backgroundTint;
        public Rect BackgroundRect => confineCamera ? cameraBounds : backgroundRect;
        public List<SavedTilemapLayer> TilemapLayers => tilemapLayers;
        public List<SavedItem> SavedItems => savedItems;
        public GameObject LevelContentPrefab => levelContentPrefab;
    }
}
