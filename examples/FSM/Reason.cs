namespace FSMExample
{
    public abstract class Reason { }

    public class Normal : Reason { }

    public class Shutdown : Reason { }

    public class Failure : Reason
    {
        public Failure(object cause)
        {
            Cause = cause;
        }

        public object Cause { get; }
    }
}