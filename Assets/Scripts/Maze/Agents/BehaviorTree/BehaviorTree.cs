using System;

namespace Maze.Agents.BehaviorTree
{
    /// <summary>Result of ticking a BT node.</summary>
    public enum BtStatus
    {
        Success,
        Failure,
        Running
    }

    /// <summary>Base class for any behavior tree node.</summary>
    public abstract class BtNode
    {
        public abstract BtStatus Tick();
    }

    /// <summary>
    /// Composite that ticks children in order. Returns Failure or Running on the
    /// first non-Success child, otherwise Success when all succeed.
    /// </summary>
    public sealed class Sequence : BtNode
    {
        private readonly BtNode[] _children;
        public Sequence(params BtNode[] children) => _children = children;

        public override BtStatus Tick()
        {
            for (int i = 0; i < _children.Length; i++)
            {
                BtStatus s = _children[i].Tick();
                if (s != BtStatus.Success) return s;
            }
            return BtStatus.Success;
        }
    }

    /// <summary>
    /// Composite that ticks children in priority order. Returns Success or Running
    /// on the first non-Failure child, otherwise Failure when all fail.
    /// </summary>
    public sealed class Selector : BtNode
    {
        private readonly BtNode[] _children;
        public Selector(params BtNode[] children) => _children = children;

        public override BtStatus Tick()
        {
            for (int i = 0; i < _children.Length; i++)
            {
                BtStatus s = _children[i].Tick();
                if (s != BtStatus.Failure) return s;
            }
            return BtStatus.Failure;
        }
    }

    /// <summary>Leaf wrapping a boolean predicate. Maps true → Success, false → Failure.</summary>
    public sealed class ConditionLeaf : BtNode
    {
        private readonly Func<bool> _predicate;
        public ConditionLeaf(Func<bool> predicate) => _predicate = predicate;
        public override BtStatus Tick() => _predicate() ? BtStatus.Success : BtStatus.Failure;
    }

    /// <summary>Leaf wrapping an action delegate that returns its own status.</summary>
    public sealed class ActionLeaf : BtNode
    {
        private readonly Func<BtStatus> _action;
        public ActionLeaf(Func<BtStatus> action) => _action = action;
        public override BtStatus Tick() => _action();
    }
}
