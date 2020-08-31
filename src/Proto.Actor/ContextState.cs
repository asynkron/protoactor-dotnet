namespace Proto
{
    //the lifecycle of an actor
    internal enum ContextState : byte
    {
        Alive, //default state
        Restarting, //after being restarted by supervisor
        Stopping, //initiating stop, all context members still functional
        Stopped //actor is fully stopped, Self PID goes to DeadLetter
    }
}