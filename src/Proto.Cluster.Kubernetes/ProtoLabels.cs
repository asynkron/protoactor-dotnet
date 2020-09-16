namespace Proto.Cluster.Kubernetes
{
    public class ProtoLabels
    {
        private const string LabelPrefix = "cluster.proto.actor/";
        public const string LabelPort = LabelPrefix + "port";
        public const string LabelKinds = LabelPrefix + "kinds";
        public const string LabelCluster = LabelPrefix + "cluster";
        public const string LabelStatusValue = LabelPrefix + "status-value";
        public const string LabelMemberId = LabelPrefix + "member-id";
    }
}