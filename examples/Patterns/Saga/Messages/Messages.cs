// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using Proto;

namespace Saga.Messages;

public abstract record Result(PID Pid)
{
    public record SuccessResult(PID Pid) : Result(Pid);

    public record FailedAndInconsistent(PID Pid) : Result(Pid);

    public record FailedButConsistentResult(PID Pid) : Result(Pid);
}

public abstract record ChangeBalance(decimal Amount, PID ReplyTo)
{
    public record Credit(decimal Amount, PID ReplyTo) : ChangeBalance(Amount, ReplyTo);

    public record Debit(decimal Amount, PID ReplyTo) : ChangeBalance(Amount, ReplyTo);
}

internal record UnknownResult(PID Pid) : Result(Pid);

internal record TransferCompleted(PID From, decimal FromBalance, PID To, decimal ToBalance);

internal record TransferFailed(string Reason);

internal record AccountCredited;

internal record AccountDebited;

internal record CreditRefused;

internal record DebitRolledBack;

internal record EscalateTransfer(string Message);

internal record GetBalance;

internal record InsufficientFunds;

internal record InternalServerError;

internal record OK;

internal record Refused;

internal record ServiceUnavailable;

internal record StatusUnknown;

internal record TransferStarted;