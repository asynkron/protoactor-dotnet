namespace Proto
{
    public partial class Terminated
    {
        public static Terminated From(PID who)
            => new Terminated
            {
                Who = who,
                AddressTerminated = false
            };
    }
}