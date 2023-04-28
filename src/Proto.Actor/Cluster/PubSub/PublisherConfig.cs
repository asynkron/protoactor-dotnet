using System;

namespace Proto.Cluster.PubSub;

public record PublisherConfig
{
  /// <summary>
  ///     Optional idle timeout which will specify to the `IPublisher` how long it should wait before invoking clean
  ///     up code to recover resources.
  /// </summary>
  public TimeSpan? IdleTimeout { get; init; }
}
