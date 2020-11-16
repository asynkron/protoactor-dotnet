namespace Proto
{
    public partial class Terminated
    {
        public static Terminated From(PID who)
            => new()
            {
                Who = who,
                AddressTerminated = false
            };
    }
}