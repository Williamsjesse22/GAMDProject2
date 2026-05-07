using UnityEngine;

namespace Maze.World
{
    /// <summary>
    /// The maze exit. Spawned inactive (small, dim, no trigger). When the lock
    /// minigame is won, <see cref="MazeGameController"/> calls <see cref="Activate"/>
    /// to make it large, bright, and walkable; the player then steps inside to win.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class Portal : MonoBehaviour
    {
        [Header("Sizing")]
        [SerializeField] private float _activeRadius = 1.4f;
        [SerializeField] private float _inactiveRadius = 0.5f;

        [Header("Pulsing (active only)")]
        [SerializeField] private float _pulseSpeed = 2.5f;
        [SerializeField] private float _pulseAmplitude = 0.08f;

        [Header("Colors")]
        [SerializeField] private Color _activeColor = new Color(0.25f, 0.85f, 1f);
        [SerializeField] private Color _inactiveColor = new Color(0.3f, 0.3f, 0.35f);

        [SerializeField] private string _playerTag = "Player";

        public bool IsActive { get; private set; }

        private SphereCollider _collider;
        private MeshRenderer _renderer;
        private Material _material;

        private void Awake()
        {
            _collider = GetComponent<SphereCollider>();
            _collider.isTrigger = true;
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer != null)
            {
                _material = new Material(_renderer.sharedMaterial);
                _renderer.sharedMaterial = _material;
            }
            // Start inactive.
            ApplyState(active: false);
        }

        public void Activate()
        {
            if (IsActive) return;
            ApplyState(active: true);
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            ApplyState(active: false);
        }

        private void ApplyState(bool active)
        {
            IsActive = active;
            if (_collider != null) _collider.enabled = active;
            if (_material != null) _material.color = active ? _activeColor : _inactiveColor;
            float r = active ? _activeRadius : _inactiveRadius;
            transform.localScale = Vector3.one * r * 2f; // primitive sphere is unit-diameter
        }

        private void Update()
        {
            if (!IsActive) return;
            float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmplitude;
            transform.localScale = Vector3.one * _activeRadius * 2f * pulse;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsActive) return;
            if (!other.CompareTag(_playerTag)) return;
            if (MazeGameController.Instance == null) return;
            MazeGameController.Instance.HandlePortalEntered();
        }
    }
}
