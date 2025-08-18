using System;
using System.Threading.Tasks;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
/// Convenience overloads for <see cref="IContext.ReenterAfter"/> to avoid boilerplate when the
/// continuation does not require the completed task or when it is synchronous.
/// </summary>
[PublicAPI]
public static class ReenterAfterExtensions
{
    public static void ReenterAfter(this IContext context, Task target, Action action)
        => context.ReenterAfter(target, _ =>
        {
            action();
            return Task.CompletedTask;
        });

    public static void ReenterAfter(this IContext context, Task target, Func<Task> action)
        => context.ReenterAfter(target, _ => action());

    public static void ReenterAfter(this IContext context, Task target, Action<Task> action)
        => context.ReenterAfter(target, t =>
        {
            action(t);
            return Task.CompletedTask;
        });

    public static void ReenterAfter<T>(this IContext context, Task<T> target, Action action)
        => context.ReenterAfter(target, _ =>
        {
            action();
            return Task.CompletedTask;
        });

    public static void ReenterAfter<T>(this IContext context, Task<T> target, Func<Task> action)
        => context.ReenterAfter(target, _ => action());

    public static void ReenterAfter<T>(this IContext context, Task<T> target, Action<Task<T>> action)
        => context.ReenterAfter(target, t =>
        {
            action(t);
            return Task.CompletedTask;
        });

    public static void ReenterAfter<T>(this IContext context, Task<T> target, Action<T> action)
        => context.ReenterAfter(target, async t =>
        {
            action(await t.ConfigureAwait(false));
        });

    public static void ReenterAfter<T>(this IContext context, Task<T> target, Func<T, Task> action)
        => context.ReenterAfter(target, async t =>
        {
            var res = await t.ConfigureAwait(false);
            await action(res).ConfigureAwait(false);
        });
}
