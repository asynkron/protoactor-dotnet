using System.Linq;

namespace Proto.Metrics
{
    public static class Tags
    {
        public static string[] FormTags(string[] labelNames, string[] defaultLabels, string[] labels)
        {
            return labelNames.Select(FormTag).ToArray();

            string FormTag(string tag, int position)
            {
                var combinedLabels = defaultLabels.EmptyIfNull().Union(labels.EmptyIfNull()).ToArray();

                var label = combinedLabels.Length <= position || string.IsNullOrEmpty(combinedLabels[position])
                    ? null
                    : $":{combinedLabels[position]}";

                return $"{tag}{label}";
            }
        }
    }
}