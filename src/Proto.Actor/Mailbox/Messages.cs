// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Mailbox
{
    public interface SystemMessage
    {
    }

    public sealed class SuspendMailbox : SystemMessage
    {
        public static readonly SuspendMailbox Instance = new();

        private SuspendMailbox()
        {
        }
    }

    public sealed class ResumeMailbox : SystemMessage
    {
        public static readonly ResumeMailbox Instance = new();

        private ResumeMailbox()
        {
        }
    }
}