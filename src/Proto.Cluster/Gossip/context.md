# Gossip Context

## Overview
Gossip protocol implementation for disseminating cluster topology and health information.

_Parent context: [Proto Cluster](../context.md)_

## Key Files
* `Consensus.cs` – C# source defining Consensus behavior.
* `ConsensusCheckBuilder.cs` – C# source defining Consensus Check Builder behavior.
* `ConsensusChecks.cs` – C# source defining Consensus Checks behavior.
* `ConsensusEvaluator.cs` – C# source defining Consensus Evaluator behavior.
* `Extensions.cs` – C# source defining Extensions behavior.
* `Gossip.cs` – C# source defining Gossip behavior.
* `GossipActor.cs` – C# source defining Gossip Actor behavior.
* `GossipDefaults.cs` – C# source defining Gossip Defaults behavior.
* `GossipKeyValue.cs` – C# source defining Gossip Key Value behavior.
* `GossipKeys.cs` – C# source defining Gossip Keys behavior.
* `GossipSender.cs` – C# source defining Gossip Sender behavior.
* `GossipStateManagement.cs` – C# source defining Gossip State Management behavior.
* `Gossiper.Consensus.cs` – C# source defining Gossiper Consensus behavior.
* `Gossiper.Dissemination.cs` – C# source defining Gossiper Dissemination behavior.
* `Gossiper.StateQueries.cs` – C# source defining Gossiper State Queries behavior.
* `Gossiper.cs` – C# source defining Gossiper behavior.
* `GossiperFactory.cs` – C# source defining Gossiper Factory behavior.
* `GossiperLogMessages.cs` – C# source defining Gossiper Log Messages behavior.
* `IConsensusCheckDefinition.cs` – C# source defining I Consensus Check Definition behavior.
* `IDeltaValue.cs` – C# source defining I Delta Value behavior.
* `IGossip.cs` – C# source defining I Gossip behavior.
* `IGossipTransport.cs` – C# source defining I Gossip Transport behavior.
* `IRandomProvider.cs` – C# source defining I Random Provider behavior.
* `MemberStateDelta.cs` – C# source defining Member State Delta behavior.
* `MemberStateDeltaBuilder.cs` – C# source defining Member State Delta Builder behavior.
* `Messages.cs` – C# source defining Messages behavior.
* `RandomOrderExtensions.cs` – C# source defining Random Order Extensions behavior.
* `SystemRandomProvider.cs` – C# source defining System Random Provider behavior.

## Primary Types and Contracts
* `Interface IConsensusHandle` defined in `Consensus.cs`
* `Class GossipConsensusHandle` defined in `Consensus.cs`
* `Class ConsensusCheckBuilder` defined in `ConsensusCheckBuilder.cs`
* `Record ConsensusCheck` defined in `ConsensusChecks.cs`
* `Class ConsensusChecks` defined in `ConsensusChecks.cs`
* `Class ConsensusEvaluator` defined in `ConsensusEvaluator.cs`
* `Class Extensions` defined in `Extensions.cs`
* `Class Gossip` defined in `Gossip.cs`
* `Class GossipActor` defined in `GossipActor.cs`
* `Class GossipDefaults` defined in `GossipDefaults.cs`
* `Class GossipKeyValue` defined in `GossipKeyValue.cs`
* `Class GossipKeys` defined in `GossipKeys.cs`
* `Class GossipSender` defined in `GossipSender.cs`
* `Class GossipStateManagement` defined in `GossipStateManagement.cs`
* `Class Gossiper` defined in `Gossiper.Consensus.cs`
* `Class Gossiper` defined in `Gossiper.Dissemination.cs`
* `Class Gossiper` defined in `Gossiper.StateQueries.cs`
* `Record GossiperOptions` defined in `Gossiper.cs`
* `Class Gossiper` defined in `Gossiper.cs`
* `Class Gossiper` defined in `GossiperFactory.cs`
* `Class GossiperLogMessages` defined in `GossiperLogMessages.cs`
* `Interface IConsensusCheckDefinition` defined in `IConsensusCheckDefinition.cs`
* `Interface IDeltaValue` defined in `IDeltaValue.cs`
* `Interface IGossip` defined in `IGossip.cs`
* `Interface IGossipCore` defined in `IGossip.cs`
* `Interface IGossipConsensusChecker` defined in `IGossip.cs`
* `Interface IGossipStateStore` defined in `IGossip.cs`
* `Interface IGossipTransport` defined in `IGossipTransport.cs`
* `Class GossipTransport` defined in `IGossipTransport.cs`
* `Interface IRandomProvider` defined in `IRandomProvider.cs`
* `Record MemberStateDelta` defined in `MemberStateDelta.cs`
* `Class MemberStateDeltaBuilder` defined in `MemberStateDeltaBuilder.cs`
* `Record MemberStateDeltaBuildResult` defined in `MemberStateDeltaBuilder.cs`
* `Class GossipRequest` defined in `Messages.cs`
* `Class GossipResponse` defined in `Messages.cs`
* `Record GossipUpdate` defined in `Messages.cs`
* `Record GetGossipStateRequest` defined in `Messages.cs`
* `Record GetGossipStateResponse` defined in `Messages.cs`
* `Record GetGossipStateEntryRequest` defined in `Messages.cs`
* `Record GetGossipStateEntryResponse` defined in `Messages.cs`
* `Record SetGossipStateKey` defined in `Messages.cs`
* `Record SetGossipStateResponse` defined in `Messages.cs`
* `Record SendGossipStateRequest` defined in `Messages.cs`
* `Record SendGossipStateResponse` defined in `Messages.cs`
* `Record AddConsensusCheck` defined in `Messages.cs`
* `Record GetGossipStateSnapshot` defined in `Messages.cs`
* `Class RandomOrderExtensions` defined in `RandomOrderExtensions.cs`
* `Class SystemRandomProvider` defined in `SystemRandomProvider.cs`

## Related Subcontexts
* No nested subcontexts.

