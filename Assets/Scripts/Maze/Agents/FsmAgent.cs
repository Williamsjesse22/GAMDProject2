using Maze.World;
using Shared;
using UnityEngine;
using UnityEngine.AI;

namespace Maze.Agents
{
    /// <summary>
    /// "The brute" — finite-state-machine agent. Hard-coded transitions:
    /// Patrol → Chase (sees player) → AttackAura (in range) → Chase (player escapes)
    /// → LostSight (loses player mid-chase) → Patrol (gives up).
    /// Visualizes its current state + vision cone + aura range via Gizmos.
    /// </summary>
    public sealed class FsmAgent : AgentBase
    {
        public enum FsmState { Patrol, Chase, AttackAura, LostSight }

        [Header("Patrol")]
        [Tooltip("Random patrol points are sampled within this radius of the agent's current position.")]
        [SerializeField] private float _patrolRadius = 12f;
        [SerializeField] private float _patrolWaitSeconds = 1.2f;

        [Header("Combat")]
        [Tooltip("Distance at which the aura starts dealing damage to the player.")]
        [SerializeField] private float _auraRange = 2.5f;
        [Tooltip("Damage per second applied while the player is inside the aura.")]
        [SerializeField] private int _damagePerSecond = 25;
        [Tooltip("Seconds the agent will hunt the player's last known position before giving up.")]
        [SerializeField] private float _lostSightSeconds = 3f;

        [Header("Speeds")]
        [SerializeField] private float _patrolSpeed = 2.5f;
        [SerializeField] private float _chaseSpeed = 4.5f;

        [Header("Ranged attack (bonus)")]
        [Tooltip("Disable to fall back to aura-only behavior.")]
        [SerializeField] private bool _enableRangedAttack = true;
        [Tooltip("Minimum chase-distance at which the agent will start firing.")]
        [SerializeField] private float _shootMinRange = 4f;
        [Tooltip("Maximum chase-distance at which the agent will fire (capped by visionRange).")]
        [SerializeField] private float _shootMaxRange = 11f;
        [SerializeField] private float _shootCooldownSeconds = 1.4f;
        [SerializeField] private float _projectileSpeed = 14f;
        [SerializeField] private int _projectileDamage = 12;

        public FsmState CurrentState => _state;

        private FsmState _state = FsmState.Patrol;
        private Vector3 _patrolTarget;
        private float _patrolWaitTimer;
        private float _lostSightTimer;
        private float _damageAccumulator;
        private Vector3 _lastKnownPlayerPos;
        private bool _hasObservedPlayer;
        private float _nextShotTime;

        protected override void Awake()
        {
            base.Awake();
            _patrolTarget = transform.position;

            // Per-level scaling: each level past 1 makes the brute 20% faster
            // and 20% more damaging (rounded). Level 1 is unchanged.
            int level = GameState.MazeLevel;
            if (level > 1)
            {
                float scale = 1f + 0.2f * (level - 1);
                _chaseSpeed *= scale;
                _damagePerSecond = Mathf.RoundToInt(_damagePerSecond * scale);
                _projectileDamage = Mathf.RoundToInt(_projectileDamage * scale);
            }
        }

        private void Start()
        {
            _agent.speed = _patrolSpeed;
            PickNewPatrolTarget(immediate: true);
        }

        private void Update()
        {
            bool seen = CanSeePlayer();
            UpdateAwareness(seen);

            switch (_state)
            {
                case FsmState.Patrol: TickPatrol(seen); break;
                case FsmState.Chase: TickChase(seen); break;
                case FsmState.AttackAura: TickAttackAura(seen); break;
                case FsmState.LostSight: TickLostSight(seen); break;
            }
        }

        private void TickPatrol(bool seen)
        {
            _agent.speed = _patrolSpeed;
            if (seen) { TransitionTo(FsmState.Chase); return; }

            // Reached current waypoint — wait, then pick another.
            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.25f)
            {
                _patrolWaitTimer -= Time.deltaTime;
                if (_patrolWaitTimer <= 0f) PickNewPatrolTarget(immediate: false);
            }
        }

        private void TickChase(bool seen)
        {
            _agent.speed = _chaseSpeed;
            if (seen)
            {
                _lastKnownPlayerPos = _player.position;
                _agent.SetDestination(_lastKnownPlayerPos);

                float dist = DistanceToPlayer();
                if (dist <= _auraRange)
                {
                    TransitionTo(FsmState.AttackAura);
                    return;
                }

                // Ranged attack: fire while chasing if within shoot range and
                // cooldown elapsed. Aura still kicks in on close approach.
                if (_enableRangedAttack
                    && dist >= _shootMinRange
                    && dist <= _shootMaxRange
                    && Time.time >= _nextShotTime)
                {
                    FireProjectile();
                    _nextShotTime = Time.time + _shootCooldownSeconds;
                }
            }
            else
            {
                _lostSightTimer = _lostSightSeconds;
                _agent.SetDestination(_lastKnownPlayerPos);
                TransitionTo(FsmState.LostSight);
            }
        }

        private void FireProjectile()
        {
            if (_player == null) return;

            // Spawn outside the agent's own collider, lead a tiny bit by aiming
            // at the player's eye line.
            Vector3 origin = transform.position + Vector3.up * _eyeHeight + transform.forward * 0.7f;
            Vector3 target = _player.position + Vector3.up * _eyeHeight;
            Vector3 dir = (target - origin).normalized;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "FsmProjectile";
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.28f;

            // Tracer color so it reads visually as "shot fired".
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(renderer.sharedMaterial);
                mat.color = new Color(1f, 0.6f, 0.2f);
                renderer.sharedMaterial = mat;
            }

            // Rigidbody so the Projectile can drive it through OnCollisionEnter.
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var proj = go.AddComponent<Projectile>();
            proj.Initialize(dir * _projectileSpeed, gameObject, _projectileDamage);
        }

        private void TickAttackAura(bool seen)
        {
            // Stand still and burn the player; the aura *is* the attack.
            _agent.ResetPath();

            float dist = DistanceToPlayer();
            if (dist > _auraRange)
            {
                TransitionTo(seen ? FsmState.Chase : FsmState.LostSight);
                if (!seen) _lostSightTimer = _lostSightSeconds;
                return;
            }

            if (_playerHealth == null || _playerHealth.IsDead) return;
            _damageAccumulator += _damagePerSecond * Time.deltaTime;
            if (_damageAccumulator >= 1f)
            {
                int dmg = Mathf.FloorToInt(_damageAccumulator);
                _damageAccumulator -= dmg;
                _playerHealth.TakeDamage(dmg);
            }
        }

        private void TickLostSight(bool seen)
        {
            _agent.speed = _chaseSpeed;
            if (seen) { TransitionTo(FsmState.Chase); return; }

            _lostSightTimer -= Time.deltaTime;
            bool reached = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.25f;
            if (_lostSightTimer <= 0f || reached)
            {
                TransitionTo(FsmState.Patrol);
                PickNewPatrolTarget(immediate: true);
            }
        }

        private void TransitionTo(FsmState next)
        {
            _state = next;
            _damageAccumulator = 0f;
        }

        private void PickNewPatrolTarget(bool immediate)
        {
            for (int i = 0; i < 16; i++)
            {
                Vector2 r = Random.insideUnitCircle * _patrolRadius;
                Vector3 candidate = transform.position + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    _patrolTarget = hit.position;
                    _agent.SetDestination(_patrolTarget);
                    _patrolWaitTimer = _patrolWaitSeconds;
                    return;
                }
            }
            // No valid sample — wait at current spot.
            _patrolTarget = transform.position;
            _patrolWaitTimer = _patrolWaitSeconds;
            if (immediate) _agent.SetDestination(_patrolTarget);
        }

        private void UpdateAwareness(bool seen)
        {
            if (_playerAwareness == null) return;
            if (seen && !_hasObservedPlayer)
            {
                _playerAwareness.AddObserver();
                _hasObservedPlayer = true;
            }
            else if (!seen && _hasObservedPlayer)
            {
                _playerAwareness.RemoveObserver();
                _hasObservedPlayer = false;
            }
        }

        private void OnDisable()
        {
            // Don't leave a dangling observer count if we get disabled while observing.
            if (_hasObservedPlayer && _playerAwareness != null)
            {
                _playerAwareness.RemoveObserver();
                _hasObservedPlayer = false;
            }
        }

        private void OnDrawGizmos()
        {
            // Vision cone (color-coded by current state) and aura range.
            Color stateColor = _state switch
            {
                FsmState.Chase => new Color(1f, 0.7f, 0.2f, 0.6f),
                FsmState.AttackAura => new Color(1f, 0.2f, 0.2f, 0.7f),
                FsmState.LostSight => new Color(0.6f, 0.6f, 1f, 0.5f),
                _ => new Color(0.3f, 1f, 0.3f, 0.5f)
            };

            Vector3 eye = transform.position + Vector3.up * _eyeHeight;
            float halfAngle = _visionAngleDegrees * 0.5f;
            Vector3 left = Quaternion.AngleAxis(-halfAngle, Vector3.up) * transform.forward * _visionRange;
            Vector3 right = Quaternion.AngleAxis(halfAngle, Vector3.up) * transform.forward * _visionRange;

            Gizmos.color = stateColor;
            Gizmos.DrawLine(eye, eye + left);
            Gizmos.DrawLine(eye, eye + right);
            Gizmos.DrawLine(eye + left, eye + right);
            Gizmos.DrawWireSphere(eye, 0.18f);

            // Aura range — always visible in red.
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _auraRange);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f, $"FSM: {_state}");
        }
#endif
    }
}
