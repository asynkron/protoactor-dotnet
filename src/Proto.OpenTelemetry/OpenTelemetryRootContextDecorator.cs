using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.OpenTelemetry;

internal class OpenTelemetryRootContextDecorator : RootContextDecorator
{
    private readonly ActivitySetup _sendActivitySetup;
    private readonly ActivitySetup _spawnActivitySetup;

    public OpenTelemetryRootContextDecorator(IRootContext context, ActivitySetup sendActivitySetup) : base(context)
    {
        _sendActivitySetup = (activity, message)
            =>
        {
            activity?.SetTag(ProtoTags.ActorType, "<None>");

            if (activity != null)
            {
                sendActivitySetup(activity, message);
            }
        };
        
        _spawnActivitySetup = (activity, message)
            =>
        {
           
        };
    }

    private static string Source => "Root";

    public override void Send(PID target, object message) =>
        OpenTelemetryMethodsDecorators.Send(Source, target, message, _sendActivitySetup,
            () => base.Send(target, message));

    public override void Request(PID target, object message) =>
        OpenTelemetryMethodsDecorators.Request(Source, target, message, _sendActivitySetup,
            () => base.Request(target, message));

    public override void Request(PID target, object message, PID? sender) =>
        OpenTelemetryMethodsDecorators.Request(Source, target, message, sender, _sendActivitySetup,
            () => base.Request(target, message, sender));

    public override Task<T> RequestAsync<T>(PID target, object message, CancellationToken cancellationToken) =>
        OpenTelemetryMethodsDecorators.RequestAsync(Source, target, message, _sendActivitySetup,
            () => base.RequestAsync<T>(target, message, cancellationToken)
        );
    
    public override PID SpawnNamed(Props props, string name, Action<IContext>? callback = null) =>
        OpenTelemetryMethodsDecorators.SpawnNamed(Source,_spawnActivitySetup, () => base.SpawnNamed(props, name, callback),name, "<None>");
}