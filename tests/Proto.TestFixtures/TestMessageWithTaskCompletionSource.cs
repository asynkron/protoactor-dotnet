using System.Threading.Tasks;

namespace Proto.TestFixtures
{
    public class TestMessageWithTaskCompletionSource
    {
        public TaskCompletionSource<int> TaskCompletionSource { get; set; } = new();
        public string Message { get; set; }
    }
}