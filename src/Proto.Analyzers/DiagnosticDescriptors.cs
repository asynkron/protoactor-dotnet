using Microsoft.CodeAnalysis;

namespace Proto.Analyzers;

public static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor PoisonAsyncDeadlock =
        new(
            DiagnosticIds.PoisonAsyncDeadlockRuleId,
            title: "PoisonAsync on self",
            messageFormat: "Awaiting PoisonAsync on context.Self will deadlock",
            DiagnosticCategories.Logic,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Do not await PoisonAsync on self, as it will deadlock. To stop the current actor, use Poison(context.Self) instead.");
}