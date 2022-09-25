// -----------------------------------------------------------------------
// <copyright file="Delegates.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Proto.Mailbox;

// ReSharper disable once CheckNamespace
namespace Proto;

public delegate Task Receive(IContext context);

public delegate Task Receiver(IReceiverContext context, MessageEnvelope envelope);

public delegate Task Sender(ISenderContext context, PID target, MessageEnvelope envelope);

public delegate PID Spawner(ActorSystem system, string id, Props props, PID? parent, Action<IContext>? callback = null);

public delegate IActor Producer();

public delegate IActor ProducerWithSystem(ActorSystem system);

public delegate IActor ProducerWithSystemAndContext(ActorSystem system, IContext context);

public delegate IMailbox MailboxProducer();