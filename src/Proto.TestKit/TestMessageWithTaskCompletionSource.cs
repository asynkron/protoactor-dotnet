using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <summary>
/// Test message carrying a <see cref="TaskCompletionSource{TResult}"/> used to control asynchronous flows.
/// </summary>
public class TestMessageWithTaskCompletionSource : SystemMessage
{
    public TaskCompletionSource<int> TaskCompletionSource { get; set; } = new();
    public string? Message { get; set; }
}
