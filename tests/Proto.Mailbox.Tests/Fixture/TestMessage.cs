using System.Threading.Tasks;

namespace Proto.Mailbox.Tests
{
    public class TestMessage
    {
        public TaskCompletionSource<int> TaskCompletionSource { get; set; } = new TaskCompletionSource<int>();
        public string Message { get; set; }
    }
}