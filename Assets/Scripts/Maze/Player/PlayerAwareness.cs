using UnityEngine;

namespace Maze.Player
{
    /// <summary>
    /// Counts how many AI agents currently have line-of-sight on the player.
    /// Agents call <see cref="AddObserver"/> when they spot the player and
    /// <see cref="RemoveObserver"/> when they lose sight. The HUD reads
    /// <see cref="IsBeingObserved"/> to flash a "DETECTED" indicator.
    /// </summary>
    public sealed class PlayerAwareness : MonoBehaviour
    {
        public int ObserverCount { get; private set; }
        public bool IsBeingObserved => ObserverCount > 0;

        public void AddObserver()
        {
            ObserverCount++;
        }

        public void RemoveObserver()
        {
            // Clamp at zero so an out-of-order RemoveObserver doesn't drive the
            // count negative and silently break the indicator.
            ObserverCount = Mathf.Max(0, ObserverCount - 1);
        }

        public void Reset()
        {
            ObserverCount = 0;
        }
    }
}
