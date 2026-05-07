using Maze.Player;
using Shared;
using UnityEngine;

namespace Maze.World
{
    /// <summary>
    /// Bonus feature: a pickup the player walks into. Currently supports a
    /// single type (health pack); structured so other types (speed boost,
    /// shield, etc.) can drop in later by extending <see cref="PowerUpType"/>.
    /// Visually it idles by slowly rotating + bobbing, and fades out + plays
    /// a chime when consumed.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class PowerUp : MonoBehaviour
    {
        public enum PowerUpType
        {
            Health
        }

        [SerializeField] private PowerUpType _type = PowerUpType.Health;
        [Tooltip("Amount applied by this pickup (HP for Health type).")]
        [SerializeField] private int _amount = 30;

        [Header("Idle motion")]
        [SerializeField] private float _rotationSpeed = 60f;
        [SerializeField] private float _bobAmplitude = 0.15f;
        [SerializeField] private float _bobSpeed = 1.5f;

        [Header("Pickup")]
        [SerializeField] private string _playerTag = "Player";
        [Tooltip("If true, the pickup is hidden + re-enabled after the respawn time. If false, it self-destroys.")]
        [SerializeField] private bool _respawn = false;
        [SerializeField] private float _respawnSeconds = 30f;

        [Header("Audio")]
        [Range(0f, 1f)]
        [SerializeField] private float _pickupVolume = 0.7f;

        private Vector3 _basePosition;
        private MeshRenderer _renderer;
        private Collider _collider;
        private AudioSource _audio;
        private AudioClip _pickupClip;
        private bool _consumed;

        private void Awake()
        {
            _basePosition = transform.position;
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
            if (_collider != null) _collider.isTrigger = true;

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = _pickupVolume;
            // Brief major-third chime: E5–G5–C6.
            _pickupClip = SoundSynth.Arp("powerup_pickup", new[] { 659f, 784f, 1047f }, 0.32f, 0.5f);
        }

        private void Update()
        {
            if (_consumed) return;
            transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f);
            float bob = Mathf.Sin(Time.time * _bobSpeed) * _bobAmplitude;
            transform.position = _basePosition + new Vector3(0f, bob, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_consumed) return;
            if (!other.CompareTag(_playerTag)) return;

            HealthComponent hp = other.GetComponent<HealthComponent>();
            if (hp == null || hp.IsDead) return;

            if (!ShouldGrant(hp)) return;

            ApplyEffect(hp);
            PlayPickupCue();

            _consumed = true;
            if (_respawn) StartHideAndRespawn();
            else Destroy(gameObject, _pickupClip != null ? _pickupClip.length : 0f);

            // Disable visual + collider immediately so the pickup feels consumed
            // even though we wait for the chime to finish before destroy/respawn.
            SetVisible(false);
        }

        private bool ShouldGrant(HealthComponent hp)
        {
            switch (_type)
            {
                case PowerUpType.Health:
                    // Skip if already full — don't waste the pickup.
                    return hp.CurrentHp < hp.MaxHp;
                default:
                    return true;
            }
        }

        private void ApplyEffect(HealthComponent hp)
        {
            switch (_type)
            {
                case PowerUpType.Health:
                    hp.Heal(_amount);
                    break;
            }
        }

        private void PlayPickupCue()
        {
            if (_audio != null && _pickupClip != null)
                _audio.PlayOneShot(_pickupClip, _pickupVolume);
        }

        private void SetVisible(bool visible)
        {
            if (_renderer != null) _renderer.enabled = visible;
            if (_collider != null) _collider.enabled = visible;
        }

        private void StartHideAndRespawn()
        {
            CancelInvoke(nameof(Respawn));
            Invoke(nameof(Respawn), _respawnSeconds);
        }

        private void Respawn()
        {
            _consumed = false;
            SetVisible(true);
            transform.position = _basePosition;
        }
    }
}
