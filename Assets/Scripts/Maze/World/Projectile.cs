using Maze.Player;
using UnityEngine;

namespace Maze.World
{
    /// <summary>
    /// Bonus: hostile projectile fired by ranged agents. Initialized with a
    /// world-space velocity; flies in a straight line under physics until it
    /// hits something (wall, obstacle, player) or lifetime expires.
    /// On player hit it applies <see cref="_damage"/> via
    /// <see cref="HealthComponent"/> then self-destructs.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private int _damage = 10;
        [SerializeField] private float _lifetime = 2.5f;
        [SerializeField] private string _playerTag = "Player";

        private float _bornAt;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Launch the projectile with the given velocity. <paramref name="shooter"/>
        /// is the GameObject that fired this round — the projectile will not
        /// collide with the shooter or any of its children (so the agent's own
        /// capsule doesn't immediately stop the round).
        /// </summary>
        public void Initialize(Vector3 velocity, GameObject shooter, int damage)
        {
            _damage = damage;
            _bornAt = Time.time;
            _rb.useGravity = false;
            _rb.linearVelocity = velocity;

            if (shooter != null)
            {
                Collider myCol = GetComponent<Collider>();
                foreach (Collider col in shooter.GetComponentsInChildren<Collider>())
                    Physics.IgnoreCollision(myCol, col, true);
            }
        }

        private void Update()
        {
            if (Time.time - _bornAt > _lifetime) Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // Damage the player on contact, ignore other hits (they just stop the round).
            Collider other = collision.collider;
            if (other.CompareTag(_playerTag))
            {
                HealthComponent hp = other.GetComponent<HealthComponent>();
                if (hp != null && !hp.IsDead) hp.TakeDamage(_damage);
            }
            Destroy(gameObject);
        }

        // Fallback: also damage on trigger overlap. The player has a
        // CharacterController whose collisions don't always generate Collision
        // events for kinematic-rigidbody-style projectiles in every Unity
        // configuration, so this catches that path too.
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(_playerTag))
            {
                HealthComponent hp = other.GetComponent<HealthComponent>();
                if (hp != null && !hp.IsDead) hp.TakeDamage(_damage);
                Destroy(gameObject);
            }
        }
    }
}
