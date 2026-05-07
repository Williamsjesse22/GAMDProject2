using Shared;
using UnityEngine;

namespace Maze.Player
{
    /// <summary>
    /// Procedural audio cues for the maze player. Subscribes to
    /// <see cref="HealthComponent.OnHealthChanged"/> for hurt feedback,
    /// polls <see cref="PlayerAwareness.IsBeingObserved"/> for the
    /// detection rising edge, and plays a heartbeat at low HP.
    /// </summary>
    public sealed class MazeAudio : MonoBehaviour
    {
        [SerializeField] private HealthComponent _health;
        [SerializeField] private PlayerAwareness _awareness;
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.55f;
        [Tooltip("HP fraction below which the heartbeat starts playing.")]
        [Range(0f, 1f)]
        [SerializeField] private float _heartbeatThreshold = 0.3f;
        [Tooltip("Seconds between heartbeat thumps when below threshold.")]
        [SerializeField] private float _heartbeatInterval = 0.7f;

        private AudioSource _audio;
        private AudioClip _detectClip;
        private AudioClip _hurtClip;
        private AudioClip _heartbeatClip;

        private bool _wasObserved;
        private int _prevHp;
        private float _heartbeatTimer;

        private void Awake()
        {
            if (_health == null) _health = GetComponent<HealthComponent>();
            if (_awareness == null) _awareness = GetComponent<PlayerAwareness>();

            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // 2D — same volume regardless of camera
            _audio.volume = _volume;

            // Sharp high beep for "you've been spotted"
            _detectClip = SoundSynth.Beep("maze_detect", 1320f, 0.12f, 0.5f);
            // Low thud for taking damage
            _hurtClip = SoundSynth.Beep("maze_hurt", 180f, 0.18f, 0.55f);
            // Bass heartbeat thump
            _heartbeatClip = SoundSynth.Beep("maze_heartbeat", 110f, 0.14f, 0.5f);

            if (_health != null) _prevHp = _health.CurrentHp;
        }

        private void OnEnable()
        {
            if (_health != null) _health.OnHealthChanged += HandleHpChanged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.OnHealthChanged -= HandleHpChanged;
        }

        private void Update()
        {
            // Detection: rising edge of IsBeingObserved.
            if (_awareness != null)
            {
                bool obs = _awareness.IsBeingObserved;
                if (obs && !_wasObserved) PlayCue(_detectClip);
                _wasObserved = obs;
            }

            // Heartbeat: only while alive and below threshold.
            if (_health != null && !_health.IsDead && _health.HpFraction < _heartbeatThreshold)
            {
                _heartbeatTimer -= Time.deltaTime;
                if (_heartbeatTimer <= 0f)
                {
                    _heartbeatTimer = _heartbeatInterval;
                    PlayCue(_heartbeatClip);
                }
            }
            else
            {
                _heartbeatTimer = 0f; // re-trigger immediately on next dip below threshold
            }
        }

        private void HandleHpChanged(int current, int max)
        {
            // Only on damage, not heal/reset.
            if (current < _prevHp) PlayCue(_hurtClip);
            _prevHp = current;
        }

        private void PlayCue(AudioClip clip)
        {
            if (clip == null || _audio == null) return;
            _audio.PlayOneShot(clip, _volume);
        }
    }
}
