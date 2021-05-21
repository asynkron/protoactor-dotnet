// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto;

namespace Saga.Messages
{
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

    record UnknownResult(PID Pid) : Result(Pid);
    record TransferCompleted(PID From, decimal FromBalance, PID To, decimal ToBalance);
    record TransferFailed(string Reason);
    record AccountCredited;
    record AccountDebited;
    record CreditRefused;
    record DebitRolledBack;
    record EscalateTransfer(string Message);
    record GetBalance;
    record InsufficientFunds;
    record InternalServerError; 
    record OK;
    record Refused;
    record ServiceUnavailable;
    record StatusUnknown;
    record TransferStarted;
}