using Microsoft.CodeAnalysis;

namespace Proto.Analyzers;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor Deadlock =
        new(
            DiagnosticIds.DeadlockRuleId,
            title: "PoisonAsync on self",
            messageFormat: "Awaiting {0} on context.Self will deadlock",
            DiagnosticCategories.Logic,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Do not await Poison/StopAsync on self, as it will deadlock. To stop the current actor, use Poison/Stop(context.Self) instead.");
}