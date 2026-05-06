using System;
using UnityEngine;

namespace Maze.Player
{
    /// <summary>
    /// Generic hit-point container. Damage clamps at zero (no negatives) and
    /// raises <see cref="OnDied"/> exactly once on the transition to dead.
    /// </summary>
    public sealed class HealthComponent : MonoBehaviour
    {
        [SerializeField] private int _maxHp = 100;
        [Tooltip("Initial HP. Defaults to MaxHp at runtime.")]
        [SerializeField] private int _startingHp = -1;

        public int MaxHp => _maxHp;
        public int CurrentHp { get; private set; }
        public bool IsDead => CurrentHp <= 0;
        public float HpFraction => _maxHp > 0 ? (float)CurrentHp / _maxHp : 0f;

        /// <summary>Fires whenever <see cref="CurrentHp"/> changes (including damage clamped to zero, healing, and reset).</summary>
        public event Action<int, int> OnHealthChanged;

        /// <summary>Fires once on the frame HP transitions from positive to zero.</summary>
        public event Action OnDied;

        private void Awake()
        {
            CurrentHp = _startingHp > 0 ? Mathf.Min(_startingHp, _maxHp) : _maxHp;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return;
            int previous = CurrentHp;
            CurrentHp = Mathf.Max(0, CurrentHp - amount);
            if (CurrentHp != previous) OnHealthChanged?.Invoke(CurrentHp, _maxHp);
            if (previous > 0 && CurrentHp == 0) OnDied?.Invoke();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || IsDead) return;
            int previous = CurrentHp;
            CurrentHp = Mathf.Min(_maxHp, CurrentHp + amount);
            if (CurrentHp != previous) OnHealthChanged?.Invoke(CurrentHp, _maxHp);
        }

        /// <summary>Restore to <see cref="MaxHp"/>. Does not fire <see cref="OnDied"/> reverse.</summary>
        public void ResetToFull()
        {
            CurrentHp = _maxHp;
            OnHealthChanged?.Invoke(CurrentHp, _maxHp);
        }
    }
}
