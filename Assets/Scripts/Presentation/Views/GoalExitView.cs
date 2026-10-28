using System.Collections;
using Portal2D.Core.Data;
using Portal2D.Core.Events;
using Portal2D.Presentation.Utils;
using UnityEngine;

namespace Portal2D.Presentation.Views
{
    /// <summary>
    /// 关卡终点与过关门视觉表现 (Goal Exit Trigger)
    /// 1. 关卡未解开时：终点处于微暗/锁死状态，无法触发；
    /// 2. 解开全部机关时：终点触发强光爆发、光柱冲天、呼吸浮动、音效鸣响，并处于耀眼高亮状态；
    /// 3. 玩家触碰已解锁终点：派发通关事件，平滑流转进入下一关。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class GoalExitView : MonoBehaviour
    {
        [Header("--- 视觉与动画微调 ---")]
        [SerializeField] private float bobAmplitude = 0.25f;
        [SerializeField] private float bobSpeed = 3.5f;

        private LevelAsset _boundLevel;
        private BoxCollider2D _collider;
        private Rigidbody2D _rb;
        private AudioSource _audioSource;
        private AudioClip _unlockClip;

        private Transform _arrowVisual;
        private SpriteRenderer _arrowSr;
        private Transform _beamTransform;
        private SpriteRenderer _beamSr;

        private Vector3 _basePosition;
        private bool _hasTriggered = false;
        private bool _isUnlocked = true;

        // 颜色调色板
        private readonly Color _lockedArrowColor = new Color(0.45f, 0.5f, 0.55f, 0.35f);
        private readonly Color _lockedBeamColor = new Color(0.2f, 0.25f, 0.3f, 0.04f);
        private readonly Color _unlockedArrowColor = new Color(0.2f, 1f, 0.55f, 1f);
        private readonly Color _unlockedBeamColor = new Color(0.2f, 1f, 0.55f, 0.35f);

        public bool IsUnlocked => _isUnlocked;

        public void Setup(LevelAsset level)
        {
            _boundLevel = level;
            _hasTriggered = false;
            _basePosition = new Vector3(level.GoalPosition.x, level.GoalPosition.y, 0f);
            transform.position = _basePosition;

            _collider = GetComponent<BoxCollider2D>();
            _collider.isTrigger = true;
            _collider.size = level.GoalTriggerSize;

            _rb = GetComponent<Rigidbody2D>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Kinematic;
            _rb.useFullKinematicContacts = true;

            SetupVisuals();

            // 根据通关规则决定初始锁闭状态
            if (level.WinCondition == LevelWinCondition.ActivateAllSwitches)
            {
                LockExit(immediate: true);
            }
            else
            {
                UnlockExit(immediate: true);
            }
        }

        public void LockExit(bool immediate = false)
        {
            _isUnlocked = false;
            if (_arrowSr != null) _arrowSr.color = _lockedArrowColor;
            if (_beamSr != null) _beamSr.color = _lockedBeamColor;
            if (_beamTransform != null) _beamTransform.localScale = new Vector3(0.6f, 1.2f, 1f);
        }

        public void UnlockExit(bool immediate = false)
        {
            if (_isUnlocked && !immediate) return;
            _isUnlocked = true;

            if (gameObject.activeInHierarchy && !immediate && Application.isPlaying)
            {
                StopAllCoroutines();
                StartCoroutine(PlayUnlockSequence());
            }
            else
            {
                if (_arrowSr != null) _arrowSr.color = _unlockedArrowColor;
                if (_beamSr != null) _beamSr.color = _unlockedBeamColor;
                if (_beamTransform != null) _beamTransform.localScale = new Vector3(1.2f, 3.2f, 1f);
            }
        }

        private IEnumerator PlayUnlockSequence()
        {
            // 1. 播放通关提示音
            PlayUnlockChime();

            // 2. 瞬间强光闪白爆破
            if (_arrowSr != null) _arrowSr.color = Color.white;
            if (_beamSr != null) _beamSr.color = new Color(0.8f, 1f, 0.9f, 0.85f);

            // 3. 视觉向上冲击与膨胀
            float elapsed = 0f;
            float duration = 0.5f;

            Vector3 startScale = new Vector3(1.0f, 1.0f, 1f);
            Vector3 peakScale = new Vector3(1.7f, 1.7f, 1f);
            Vector3 finalScale = new Vector3(1.25f, 1.25f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (t < 0.4f)
                {
                    float factor = t / 0.4f;
                    _arrowVisual.localScale = Vector3.Lerp(startScale, peakScale, factor);
                    _beamTransform.localScale = Vector3.Lerp(new Vector3(0.6f, 1.2f, 1f), new Vector3(1.5f, 4.0f, 1f), factor);
                }
                else
                {
                    float factor = (t - 0.4f) / 0.6f;
                    _arrowVisual.localScale = Vector3.Lerp(peakScale, finalScale, factor);
                    _beamTransform.localScale = Vector3.Lerp(new Vector3(1.5f, 4.0f, 1f), new Vector3(1.2f, 3.2f, 1f), factor);
                }

                yield return null;
            }

            if (_arrowSr != null) _arrowSr.color = _unlockedArrowColor;
            if (_beamSr != null) _beamSr.color = _unlockedBeamColor;
        }

        private void PlayUnlockChime()
        {
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 0f;
                _audioSource.playOnAwake = false;
            }

            if (_unlockClip == null)
            {
                _unlockClip = CreateChimeClip();
            }

            _audioSource.PlayOneShot(_unlockClip, 0.85f);
        }

        private static AudioClip CreateChimeClip()
        {
            int sampleRate = 44100;
            float duration = 0.6f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            // 经典的通关解锁水晶和弦：C6 (1046.5Hz) -> E6 (1318.5Hz) -> G6 (1567.98Hz)
            float[] freqs = new float[] { 1046.5f, 1318.51f, 1567.98f };

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;
                for (int note = 0; note < freqs.Length; note++)
                {
                    float noteStart = note * 0.11f;
                    if (t >= noteStart)
                    {
                        float noteTime = t - noteStart;
                        float envelope = Mathf.Exp(-noteTime * 7.5f);
                        sample += Mathf.Sin(2f * Mathf.PI * freqs[note] * noteTime) * envelope;
                    }
                }
                data[i] = sample * 0.32f;
            }

            var clip = AudioClip.Create("GoalUnlockChime", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void SetupVisuals()
        {
            if (_arrowVisual != null) Destroy(_arrowVisual.gameObject);

            var visualObj = new GameObject("ArrowVisual");
            visualObj.transform.SetParent(transform, false);
            _arrowVisual = visualObj.transform;

            _arrowSr = visualObj.AddComponent<SpriteRenderer>();
            _arrowSr.sprite = CreateArrowSprite();
            _arrowSr.color = _unlockedArrowColor;
            _arrowSr.sortingOrder = 15;
            visualObj.transform.localScale = new Vector3(1.25f, 1.25f, 1f);

            var beamObj = new GameObject("ExitLightBeam");
            beamObj.transform.SetParent(visualObj.transform, false);
            _beamTransform = beamObj.transform;

            _beamSr = beamObj.AddComponent<SpriteRenderer>();
            _beamSr.sprite = SpriteFactory.CreateBorderedBoxSprite(16, new Color(0.2f, 1f, 0.55f, 0.18f), new Color(0.2f, 1f, 0.55f, 0.65f), 1);
            _beamSr.sortingOrder = 5;
            _beamTransform.localScale = new Vector3(1.2f, 3.2f, 1f);
        }

        private void Update()
        {
            if (_arrowVisual != null)
            {
                // 未解锁：缓慢微动；已解锁：充满生机的快速浮动与发光呼吸！
                float currentSpeed = _isUnlocked ? bobSpeed : bobSpeed * 0.5f;
                float currentAmp = _isUnlocked ? bobAmplitude : bobAmplitude * 0.4f;

                float offsetX = Mathf.Sin(Time.time * currentSpeed) * currentAmp;
                _arrowVisual.localPosition = new Vector3(offsetX, 0f, 0f);

                // 已解锁状态下的耀眼光柱呼吸脉冲
                if (_isUnlocked)
                {
                    float pulse = Mathf.PingPong(Time.time * 2.8f, 0.35f);
                    if (_arrowSr != null)
                    {
                        _arrowSr.color = new Color(0.2f + pulse * 0.4f, 1f, 0.55f + pulse * 0.45f, 1f);
                    }
                    if (_beamSr != null)
                    {
                        _beamSr.color = new Color(0.2f, 1f, 0.55f, 0.25f + pulse * 0.25f);
                    }
                }
            }

            // 主动触发检测 (直接支持坐标范围与物理重叠双重判定，仅在已解锁时触发通关)
            if (!_hasTriggered && _isUnlocked)
            {
                var player = FindAnyObjectByType<PlayerEntityView>();
                if (player != null)
                {
                    Vector3 playerCenter = player.transform.position + (Vector3)player.ColliderOffset;
                    Vector2 playerSize = (player.CollisionSize != Vector2.zero) ? player.CollisionSize : new Vector2(0.75f, 0.95f);
                    Bounds pBounds = new Bounds(playerCenter, playerSize);

                    Vector2 triggerSize = _collider != null ? _collider.size : new Vector2(1.5f, 2.5f);
                    Bounds goalBounds = new Bounds(transform.position, triggerSize);
                    goalBounds.Expand(new Vector3(0.2f, 0.2f, 0f));

                    if (pBounds.Intersects(goalBounds))
                    {
                        TriggerGoal();
                        return;
                    }
                }

                if (_collider != null)
                {
                    var hits = Physics2D.OverlapBoxAll(transform.position, _collider.size, 0f);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        if (hits[i] != null && (hits[i].CompareTag("Player") || hits[i].GetComponentInParent<PlayerEntityView>() != null))
                        {
                            TriggerGoal();
                            break;
                        }
                    }
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_hasTriggered || !_isUnlocked) return;

            if (other.CompareTag("Player") || other.GetComponentInParent<PlayerEntityView>() != null)
            {
                TriggerGoal();
            }
        }

        private void TriggerGoal()
        {
            if (_hasTriggered || !_isUnlocked) return;
            _hasTriggered = true;

            Debug.Log($"<color=cyan>[GoalExitView] 玩家成功触达已解锁的关卡终点！流转进入下一关: {_boundLevel.NextLevel?.LevelDisplayName ?? "通关"}</color>");
            EventBus.Publish(new LevelGoalReachedEvent(_boundLevel, _boundLevel.NextLevel));
        }

        private Sprite CreateArrowSprite()
        {
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }

            int midY = size / 2;
            for (int x = 6; x < 26; x++)
            {
                int distFromHead = 25 - x;
                int halfHeight = distFromHead;
                for (int y = midY - halfHeight; y <= midY + halfHeight; y++)
                {
                    if (y >= 4 && y < size - 4)
                    {
                        bool isStem = (x < 18 && Mathf.Abs(y - midY) <= 3);
                        bool isHead = (x >= 18);
                        if (isStem || isHead)
                        {
                            tex.SetPixel(x, y, Color.white);
                        }
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
