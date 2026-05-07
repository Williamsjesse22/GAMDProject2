using Maze.Agents.BehaviorTree;
using UnityEngine;
using UnityEngine.AI;

namespace Maze.Agents
{
    /// <summary>
    /// "The cautious one" — uses a hand-built behavior tree (Selector at root,
    /// Sequence-driven branches) instead of a hard-coded FSM. Two visible
    /// differences from <see cref="FsmAgent"/>:
    ///   1. Engages only after confirming sight for <see cref="_confirmDelaySeconds"/>.
    ///   2. Maintains a standoff distance — kites the player at <see cref="_standoffDistance"/>
    ///      instead of charging into melee, applying steady but lower DPS.
    /// </summary>
    public sealed class BehaviorTreeAgent : AgentBase
    {
        public enum BtBranch { Patrol, Investigate, Combat }

        [Header("Engagement")]
        [Tooltip("Seconds the agent must continuously see the player before committing to chase.")]
        [SerializeField] private float _confirmDelaySeconds = 0.4f;
        [Tooltip("Preferred distance to the player while engaging — agent kites toward this gap.")]
        [SerializeField] private float _standoffDistance = 4.5f;
        [Tooltip("Maximum distance at which damage applies. Should be ≥ standoff so damage triggers at preferred range.")]
        [SerializeField] private float _attackRange = 5.5f;
        [Tooltip("Damage per second while in attack range. Lower than the FSM brute on purpose — sustained, not burst.")]
        [SerializeField] private int _damagePerSecond = 12;

        [Header("Patrol")]
        [SerializeField] private float _patrolRadius = 12f;
        [SerializeField] private float _patrolWaitSeconds = 1.6f;

        [Header("Investigation")]
        [Tooltip("How close to the last-known position counts as 'arrived' before falling back to patrol.")]
        [SerializeField] private float _investigateArrivalRadius = 1.5f;

        [Header("Speeds")]
        [SerializeField] private float _patrolSpeed = 2f;
        [SerializeField] private float _engageSpeed = 3.5f;

        public BtBranch CurrentBranch { get; private set; } = BtBranch.Patrol;

        private BtNode _root;
        private float _seenSinceTime = -1f;
        private bool _hasLastKnownPos;
        private Vector3 _lastKnownPos;
        private Vector3 _patrolTarget;
        private float _patrolWaitTimer;
        private float _damageAccumulator;
        private bool _hasObservedPlayer;

        protected override void Awake()
        {
            base.Awake();
            _patrolTarget = transform.position;
            BuildTree();
        }

        private void Start()
        {
            _agent.speed = _patrolSpeed;
            PickNewPatrolTarget(immediate: true);
        }

        private void BuildTree()
        {
            // Root selector: try Combat → Investigate → Patrol, in priority order.
            _root = new Selector(
                new Sequence(
                    new ConditionLeaf(() => _seenSinceTime >= 0f
                                            && Time.time - _seenSinceTime >= _confirmDelaySeconds),
                    new ActionLeaf(CombatTick)
                ),
                new Sequence(
                    // Only investigate when the player is NOT currently in sight.
                    // Otherwise the agent would rush toward the player during the
                    // confirm-sight delay and arrive in melee — defeating the
                    // whole "cautious standoff" point of this agent.
                    new ConditionLeaf(() => _hasLastKnownPos && _seenSinceTime < 0f),
                    new ActionLeaf(InvestigateTick)
                ),
                new ActionLeaf(PatrolTick)
            );
        }

        private void Update()
        {
            // Pre-tick housekeeping: maintain seen-since timer + last-known position
            // so the BT leaves are pure decisions over current state.
            bool seen = CanSeePlayer();
            if (seen)
            {
                if (_seenSinceTime < 0f) _seenSinceTime = Time.time;
                _lastKnownPos = _player.position;
                _hasLastKnownPos = true;
            }
            else
            {
                _seenSinceTime = -1f;
            }
            UpdateAwareness(seen);

            _root?.Tick();
        }

        // ---- BT actions ----

        private BtStatus CombatTick()
        {
            CurrentBranch = BtBranch.Combat;
            _agent.speed = _engageSpeed;

            if (_player == null) return BtStatus.Failure;

            // Compute desired position: stand off from the player along the
            // horizontal agent→player ray. We ignore Y so the standoff target
            // never floats above the floor when the agent is right on top of
            // the player (NavMesh.SamplePosition would then fail and the agent
            // would freeze in place — exactly the bug we're avoiding).
            Vector3 toAgent = transform.position - _player.position;
            toAgent.y = 0f;
            float horizontal = toAgent.magnitude;
            float distance = Vector3.Distance(transform.position, _player.position);
            Vector3 dir = horizontal > 0.05f
                ? toAgent / horizontal
                : new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            Vector3 desired = _player.position + dir * _standoffDistance;
            desired.y = transform.position.y;

            if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);

            // Attack while in range.
            if (distance <= _attackRange && _playerHealth != null && !_playerHealth.IsDead)
            {
                _damageAccumulator += _damagePerSecond * Time.deltaTime;
                if (_damageAccumulator >= 1f)
                {
                    int dmg = Mathf.FloorToInt(_damageAccumulator);
                    _damageAccumulator -= dmg;
                    _playerHealth.TakeDamage(dmg);
                }
            }
            else
            {
                _damageAccumulator = 0f;
            }

            return BtStatus.Running;
        }

        private BtStatus InvestigateTick()
        {
            CurrentBranch = BtBranch.Investigate;
            _agent.speed = _engageSpeed;

            _agent.SetDestination(_lastKnownPos);

            // Reached last-known? Drop the breadcrumb and let the selector fall through to Patrol next tick.
            if (!_agent.pathPending
                && Vector3.Distance(transform.position, _lastKnownPos) <= _investigateArrivalRadius)
            {
                _hasLastKnownPos = false;
                return BtStatus.Failure;
            }
            return BtStatus.Running;
        }

        private BtStatus PatrolTick()
        {
            CurrentBranch = BtBranch.Patrol;
            _agent.speed = _patrolSpeed;
            _damageAccumulator = 0f;

            if (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance + 0.25f)
            {
                _patrolWaitTimer -= Time.deltaTime;
                if (_patrolWaitTimer <= 0f) PickNewPatrolTarget(immediate: false);
            }
            return BtStatus.Running;
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
            if (_hasObservedPlayer && _playerAwareness != null)
            {
                _playerAwareness.RemoveObserver();
                _hasObservedPlayer = false;
            }
        }

        private void OnDrawGizmos()
        {
            // Vision cone color-coded by current branch.
            Color stateColor = CurrentBranch switch
            {
                BtBranch.Combat => new Color(0.3f, 0.7f, 1f, 0.7f),
                BtBranch.Investigate => new Color(0.6f, 0.6f, 1f, 0.5f),
                _ => new Color(0.3f, 1f, 0.7f, 0.5f)
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

            // Standoff ring (where this agent prefers to stand vs the player).
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _standoffDistance);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.6f, $"BT: {CurrentBranch}");
        }
#endif
    }
}
