namespace ArenaCombat.Core.AI.BT
{
    public enum BTStatus { Success, Failure, Running }

    public abstract class BTNode
    {
        public abstract BTStatus Tick();
    }

    public class BTSelector : BTNode
    {
        readonly BTNode[] _children;
        public BTSelector(params BTNode[] children) => _children = children;

        public override BTStatus Tick()
        {
            foreach (var c in _children)
            {
                var s = c.Tick();
                if (s != BTStatus.Failure) return s;
            }
            return BTStatus.Failure;
        }
    }

    public class BTSequence : BTNode
    {
        readonly BTNode[] _children;
        public BTSequence(params BTNode[] children) => _children = children;

        public override BTStatus Tick()
        {
            foreach (var c in _children)
            {
                var s = c.Tick();
                if (s != BTStatus.Success) return s;
            }
            return BTStatus.Success;
        }
    }

    public class BTCondition : BTNode
    {
        readonly System.Func<bool> _check;
        public BTCondition(System.Func<bool> check) => _check = check;
        public override BTStatus Tick() => _check() ? BTStatus.Success : BTStatus.Failure;
    }

    public class BTAction : BTNode
    {
        readonly System.Func<BTStatus> _action;
        public BTAction(System.Func<BTStatus> action) => _action = action;
        public override BTStatus Tick() => _action();
    }
}
