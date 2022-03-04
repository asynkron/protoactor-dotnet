namespace Proto.Cluster.Dashboard;

public class TreeNodeComparer : IEqualityComparer<TreeNode>
{
    public static readonly TreeNodeComparer Instance = new();
    public bool Equals(TreeNode? one, TreeNode? two)
    {
        // Adjust according to requirements.
        return StringComparer.InvariantCultureIgnoreCase
            .Equals(one?.Name, two?.Name);

    }

    public int GetHashCode(TreeNode item)
    {
        return StringComparer.InvariantCultureIgnoreCase
            .GetHashCode(item.Name);

    }
}

public record TreeNode
{
    public string? Name { get; init; }
    public HashSet<TreeNode> Children { get; } = new(TreeNodeComparer.Instance);

    public TreeNode GetChild(string name)
    {
        var child = Children.FirstOrDefault(c => c.Name == name);
        if (child == null)
        {
            child = new TreeNode()
            {
                Name = name,
            };
            Children.Add(child);
        }

        return child;
    }
}