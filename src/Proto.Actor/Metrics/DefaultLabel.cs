// ReSharper disable ParameterTypeCanBeEnumerable.Global

using System.Linq;

namespace Proto.Metrics
{
    public class DefaultLabel
    {
        public DefaultLabel(string labelName, string labelValue)
        {
            LabelName = labelName;
            LabelValue = labelValue;
        }

        public string LabelName { get; }
        public string LabelValue { get; }
    }

    public static class DefaultLabelExtensions
    {
        public static string[] GetLabels(this DefaultLabel[] defaultLabels) => defaultLabels.Select(x => x.LabelValue).ToArray();

        public static string[] GetLabelNames(this DefaultLabel[] defaultLabels) => defaultLabels.Select(x => x.LabelName).ToArray();
    }
}