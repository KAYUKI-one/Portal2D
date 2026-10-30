using Portal2D.Core.Events;
using UnityEngine;

namespace Portal2D.Presentation.Gimmicks
{
    // Inventory adapter only. Motion, contacts and pushing belong entirely to Rigidbody2D.
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(SpriteRenderer))]
    public sealed class PushableBlockEntity : MonoBehaviour
    {
        [SerializeField] private Sprite blockSprite;
        [SerializeField] private Color blockColor = Color.white;
        [SerializeField] private string itemId = "StoneBlock", itemName = "可移动物块", itemDescription = "F 放置，鼠标指向后按 R 收回。";
        [SerializeField] private float maxRecallDistance = 6;
        public string ItemId => itemId;
        public string ItemName => itemName;
        public Rigidbody2D Rigidbody => GetComponent<Rigidbody2D>();
        public BoxCollider2D Collider => GetComponent<BoxCollider2D>();
        public Sprite BlockSprite { get => GetComponent<SpriteRenderer>().sprite; set { blockSprite = value; ApplyVisuals(); } }
        private void Awake() => ApplyVisuals();
        private void OnEnable() => EventBus.Subscribe<PlayerWorldInteractEvent>(Recall);
        private void OnDisable() => EventBus.Unsubscribe<PlayerWorldInteractEvent>(Recall);
        public void InitializeComponents()
        {
            Rigidbody.bodyType = RigidbodyType2D.Dynamic;
            Rigidbody.mass = 3; Rigidbody.gravityScale = 3;
            Rigidbody.linearDamping = 0; Rigidbody.angularDamping = 0;
            Rigidbody.constraints = RigidbodyConstraints2D.None;
            Rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            Rigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            Collider.isTrigger = false; Collider.size = Vector2.one * 0.95f;
            gameObject.layer = Core.Constants.GameLayers.FrontGroundLayer;
            ApplyVisuals();
        }
        public void ApplyVisuals()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (blockSprite != null) sr.sprite = blockSprite;
            sr.color = blockColor; sr.sortingLayerName = "Item"; sr.sortingOrder = 5;
        }
        private void Recall(PlayerWorldInteractEvent evt)
        {
            if (!Collider.OverlapPoint(evt.WorldPosition) || Vector2.Distance(evt.PlayerPosition, transform.position) > Mathf.Min(maxRecallDistance, evt.MaxDistance)) return;
            EventBus.Publish(new InventoryAddItemRequestEvent(itemId, 1, itemName, BlockSprite, itemDescription, success =>
            {
                if (!success || this == null) return;
                EventBus.Publish(new PushableBlockRecalledEvent(transform.position, itemId));
                gameObject.SetActive(false); Destroy(gameObject);
            }));
        }
        public static PushableBlockEntity Spawn(Vector3 worldPos, Transform parent = null, Sprite customSprite = null)
        {
            var prefab = Resources.Load<PushableBlockEntity>("Gameplay/Pushable_StoneBlock");
            if (prefab == null) { Debug.LogError("Missing Gameplay/Pushable_StoneBlock prefab"); return null; }
            var instance = Instantiate(prefab, worldPos, Quaternion.identity, parent);
            if (customSprite != null) instance.BlockSprite = customSprite;
            return instance;
        }
    }
}
