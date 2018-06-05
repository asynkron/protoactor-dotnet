namespace Proto
{
    public partial class Terminated
    {
        public static Terminated From(PID who)
        {
            return new Terminated()
            {
                Who = who,
                AddressTerminated = false,
            };
        }
        
    }
}