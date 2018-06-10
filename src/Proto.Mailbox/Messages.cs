// -----------------------------------------------------------------------
//  <copyright file="Messages.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Mailbox
{
    public abstract class SystemMessage
    {
    }

    public sealed class SuspendMailbox : SystemMessage
    {
        public static readonly SuspendMailbox Instance = new SuspendMailbox();

        private SuspendMailbox()
        {
        }
    }

    public sealed class ResumeMailbox : SystemMessage
    {
        public static readonly ResumeMailbox Instance = new ResumeMailbox();

        private ResumeMailbox()
        {
        }
    }
}