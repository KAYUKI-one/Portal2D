using Portal2D.Presentation.Utils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Portal2D.Presentation.Gimmicks
{
    /// <summary>
    /// 物理机关方块实体 (Puzzle Block / Cube)
    /// 1. 真实 2D 物理碰撞与刚体，可用来压住压力板、阻挡射线或搭建垫脚台；
    /// 2. 玩家手持该方块时左键或按快捷键放置在面前；
    /// 3. 鼠标右键指向该方块时可随时回收回背包；
    /// 4. 纯数据驱动，无需外部 Prefab 文件依赖。
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PuzzleBlockEntity : MonoBehaviour
    {
        [SerializeField] private float maxRecallDistance = 6.0f;

        private Rigidbody2D _rb;
        private BoxCollider2D _col;
        private SpriteRenderer _sr;

        public static PuzzleBlockEntity Spawn(Vector3 worldPos, Transform parent = null)
        {
            var go = new GameObject("PuzzleBlock");
            go.transform.position = worldPos;
            if (parent != null) go.transform.SetParent(parent, true);

            var entity = go.AddComponent<PuzzleBlockEntity>();
            entity.Initialize();
            return entity;
        }

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_sr == null)
            {
                _sr = GetComponent<SpriteRenderer>();
                if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
                _sr.sprite = SpriteFactory.CreateForestPuzzleBlockSprite(48);
                _sr.sortingOrder = 10;
            }

            if (_col == null)
            {
                _col = GetComponent<BoxCollider2D>();
                _col.size = new Vector2(0.9f, 0.9f);
                _col.isTrigger = false;
            }

            if (_rb == null)
            {
                _rb = GetComponent<Rigidbody2D>();
                _rb.bodyType = RigidbodyType2D.Dynamic;
                _rb.mass = 2.5f;
                _rb.gravityScale = 3.0f;
                _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rb.freezeRotation = true; // 保持方块平稳下落，不倾斜卡住
            }

            // 确保处于默认物理层 (不是 ObstaclesLayer，以供压力板检测其刚体重量)
            gameObject.layer = 0;
        }

        private void Update()
        {
            // 鼠标右键收回检测
            bool rightClicked = false;
            if (Mouse.current != null)
            {
                rightClicked = Mouse.current.rightButton.wasPressedThisFrame;
            }
            else
            {
                rightClicked = UnityEngine.Input.GetMouseButtonDown(1);
            }

            if (rightClicked)
            {
                CheckRightClickRecall();
            }
        }

        private void CheckRightClickRecall()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Vector2 mouseScreen = Mouse.current != null ? Mouse.current.position.ReadValue() : (Vector2)UnityEngine.Input.mousePosition;
            Vector2 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);

            if (_col != null && _col.OverlapPoint(mouseWorld))
            {
                // 检查玩家距离
                var player = FindAnyObjectByType<Views.PlayerEntityView>();
                if (player != null)
                {
                    float dist = Vector2.Distance(player.transform.position, transform.position);
                    if (dist > maxRecallDistance) return;
                }

                RecallToInventory();
            }
        }

        public void RecallToInventory()
        {
            var inv = Inventory.InventoryManager.Instance;
            if (inv != null)
            {
                inv.AddItem("PuzzleBlock", 1);
            }

            // 播放收回轻鸣音
            PlayRecallSound();

            Destroy(gameObject);
        }

        private void PlayRecallSound()
        {
            var tempAudio = new GameObject("RecallAudio");
            tempAudio.transform.position = transform.position;
            var a = tempAudio.AddComponent<AudioSource>();
            a.spatialBlend = 0f;
            a.playOnAwake = false;

            int sampleRate = 44100;
            float dur = 0.2f;
            int count = (int)(sampleRate * dur);
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 15f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * env * 0.25f;
            }
            var clip = AudioClip.Create("RecallChime", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            a.PlayOneShot(clip);
            Destroy(tempAudio, 0.3f);
        }
    }
}
