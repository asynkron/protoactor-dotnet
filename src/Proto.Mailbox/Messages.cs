namespace Proto
{
    public sealed class SuspendMailbox
    {
        public static readonly SuspendMailbox Instance = new SuspendMailbox();

        private SuspendMailbox()
        {
        }
    }

    public sealed class ResumeMailbox
    {
        public static readonly ResumeMailbox Instance = new ResumeMailbox();

        private ResumeMailbox()
        {
        }
    }
}
