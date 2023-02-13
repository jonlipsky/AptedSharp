using System.Collections.Generic;

namespace AptedSharp
{
    public class LoggingOperationExecutor<T> : IOperationExecutor<T>
    {
        public List<Operation<T>> Operations { get; } = new();
    
        public void Delete(INode<T> n)
        {
            Operations.Add(new Operation<T>(nameof(Delete), n));
        }

        public void Insert(INode<T> n)
        {
            Operations.Add(new Operation<T>(nameof(Insert), n));
        }

        public void Update(
            INode<T> n1, 
            INode<T> n2)
        {
            Operations.Add(new Operation<T>(nameof(Update), n1, n2));
        }
    
        public class Operation<TItem> 
        {
            public Operation(string name, INode<TItem> node1, INode<TItem>? node2 = null)
            {
                Name = name;
                Node1 = node1;
                Node2 = node2;
            }
    
            public string Name { get; init; }
            public INode<TItem> Node1 { get; init; }
            public INode<TItem>? Node2 { get; init; }

            public override string ToString()
            {
                return $"{nameof(Name)}: {Name}, {nameof(Node1)}: {Node1.NodeData}, {nameof(Node2)}: {Node2?.NodeData?.ToString() ?? string.Empty}";
            }
        }
    }
}

