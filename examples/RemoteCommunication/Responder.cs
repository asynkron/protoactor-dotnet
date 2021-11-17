using Microsoft.Extensions.Logging;
using Proto;

namespace RemoteCommunication
{
    public class Responder : IActor
    {
        private ILogger _logger = Log.CreateLogger<Responder>();
        private readonly List<PID> _terminatedPids = new();
        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Ping => Respond(context),
            PID pid => OnWatch(context, pid),
            Terminated t => OnTerminated(t),
            object => LogMessage(context),
            _ => Task.CompletedTask
        };
        private Task OnTerminated(Terminated t)
        {
            _terminatedPids.Add(t.Who);
            return Task.CompletedTask;
        }

        private Task OnWatch(IContext context, PID pidToWatch)
        {
            if (_terminatedPids.Contains(pidToWatch))
            {
                context.Respond($"{context.Self}: {pidToWatch} was stopped");
            }
            else
            {
                context.Watch(pidToWatch);
                _logger.LogInformation($"[{context.Self}] is watching {pidToWatch}");
                context.Respond($"{context.Self}: I'm watching {pidToWatch}");
            }
            return Task.CompletedTask;
        }

        private Task LogMessage(IContext context)
        {
            _logger.LogInformation($"[{context.Self}] -> {context.Message}");
            return Task.CompletedTask;
        }
        private Task Respond(IContext context)
        {
            _logger.LogInformation($"{context.Self} Responding to {context.Sender}");
            context.Respond(new Pong());
            if (context.Sender is not null && context.Sender.Id.Contains("ClientActor", StringComparison.InvariantCulture))
                context.Stop(context.Sender);
            return Task.CompletedTask;
        }
    }
}