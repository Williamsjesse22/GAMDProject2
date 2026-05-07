using Maze.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Maze.Agents
{
    /// <summary>
    /// Shared scaffolding for the maze's hostile agents: holds the
    /// <see cref="NavMeshAgent"/>, finds the player on awake, and exposes a
    /// vision check (range + cone angle + line-of-sight raycast) usable by
    /// any concrete AI strategy (FSM, behavior tree, etc.).
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public abstract class AgentBase : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected NavMeshAgent _agent;
        [SerializeField] protected Transform _player;
        [SerializeField] protected HealthComponent _playerHealth;
        [SerializeField] protected PlayerAwareness _playerAwareness;

        [Header("Vision")]
        [Tooltip("Maximum distance from agent eye to player center at which the player can be perceived.")]
        [SerializeField] protected float _visionRange = 12f;
        [Tooltip("Total field-of-view angle in degrees. The cone half-angle is half of this.")]
        [Range(10f, 180f)]
        [SerializeField] protected float _visionAngleDegrees = 70f;
        [Tooltip("Height of the agent's 'eye' above its transform origin — where vision rays originate.")]
        [SerializeField] protected float _eyeHeight = 1.5f;
        [Tooltip("Layers that block line of sight (walls, terrain). Default = everything except triggers.")]
        [SerializeField] protected LayerMask _occluders = ~0;

        protected virtual void Awake()
        {
            if (_agent == null) _agent = GetComponent<NavMeshAgent>();
            if (_player == null || _playerHealth == null || _playerAwareness == null)
                ResolvePlayerReferences();
        }

        private void ResolvePlayerReferences()
        {
            // Find the player by locating its HealthComponent — single canonical
            // marker on the player root, no need to rely on a "Player" tag being set.
#if UNITY_2023_1_OR_NEWER
            HealthComponent playerHealth = Object.FindAnyObjectByType<HealthComponent>();
#else
            HealthComponent playerHealth = Object.FindObjectOfType<HealthComponent>();
#endif
            if (playerHealth == null) return;

            if (_player == null) _player = playerHealth.transform;
            if (_playerHealth == null) _playerHealth = playerHealth;
            if (_playerAwareness == null) _playerAwareness = playerHealth.GetComponent<PlayerAwareness>();
        }

        /// <summary>True iff the player is in vision range, within the cone, and unobstructed.</summary>
        protected bool CanSeePlayer()
        {
            if (_player == null) return false;

            Vector3 eye = transform.position + Vector3.up * _eyeHeight;
            Vector3 target = _player.position + Vector3.up * _eyeHeight;
            Vector3 toPlayer = target - eye;
            float distance = toPlayer.magnitude;
            if (distance < 0.001f) return true;
            if (distance > _visionRange) return false;

            // Cone angle check is on the horizontal plane only — agents look around
            // their forward axis but should still spot a player who's slightly higher
            // or lower (jumping, falling).
            Vector3 flatDir = new Vector3(toPlayer.x, 0f, toPlayer.z);
            if (flatDir.sqrMagnitude < 0.0001f) return true;
            flatDir.Normalize();
            float angle = Vector3.Angle(transform.forward, flatDir);
            if (angle > _visionAngleDegrees * 0.5f) return false;

            // Line-of-sight raycast to the player. We use RaycastAll + filter so
            // we can skip self-collisions: when agent and player overlap (an
            // edge that does happen — initial chase spike, kiting interactions),
            // the agent's own capsule would otherwise be the first hit and we'd
            // wrongly report sight as blocked. Sight is clear iff no collider
            // OTHER than the agent itself or the player is along the ray.
            Vector3 dir = toPlayer / distance;
            RaycastHit[] hits = Physics.RaycastAll(eye, dir, distance + 0.1f,
                                                   _occluders, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Transform t = hits[i].transform;
                if (t == transform || t.IsChildOf(transform)) continue;
                if (t == _player || t.IsChildOf(_player)) continue;
                return false;
            }
            return true;
        }

        protected float DistanceToPlayer()
        {
            if (_player == null) return float.PositiveInfinity;
            return Vector3.Distance(transform.position, _player.position);
        }
    }
}
