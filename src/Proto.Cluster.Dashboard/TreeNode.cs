namespace Proto.Cluster.Dashboard;

public class TreeNodeComparer : IEqualityComparer<TreeNode>
{
    public static readonly TreeNodeComparer Instance = new();

    public bool Equals(TreeNode? one, TreeNode? two) =>
        // Adjust according to requirements.
        StringComparer.InvariantCultureIgnoreCase
            .Equals(one?.Name, two?.Name);

    public int GetHashCode(TreeNode item) =>
        StringComparer.InvariantCultureIgnoreCase
            .GetHashCode(item.Name!);
}

public record TreeNode
{
    public string? Name { get; init; }
    public HashSet<TreeNode> Children { get; } = new(TreeNodeComparer.Instance);
    public PID? Pid { get; set; }

    public TreeNode GetChild(string name)
    {
        var child = Children.FirstOrDefault(c => c.Name == name);

        if (child == null)
        {
            child = new TreeNode
            {
                Name = name
            };

            Children.Add(child);
        }

        return child;
    }

    public TreeNode GetChildFor(PID pid)
    {
        var id = $"{pid.Address}/{pid.Id}";
        var parts = id.Split("/");
        var node = parts.Aggregate(this, (current, part) => current.GetChild(part));
        node.Pid = pid;

        return node;
    }
}