using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestFixtures;

public class TestMessageWithTaskCompletionSource : SystemMessage
{
    public TaskCompletionSource<int> TaskCompletionSource { get; set; } = new();
    public string Message { get; set; }
}