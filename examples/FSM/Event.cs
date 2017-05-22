namespace FSMExample
{
    public class Event<TD>
    {
        public Event(object fsmEvent, TD stateData)
        {
            StateData = stateData;
            FsmEvent = fsmEvent;
        }

        public object FsmEvent { get; private set; }

        public TD StateData { get; private set; }
    }

    public class StopEvent<TD>
    {
        public StopEvent(Reason reason, string terminatedState, TD stateData)
        {
            StateData = stateData;
            TerminatedState = terminatedState;
            Reason = reason;
        }

        public Reason Reason { get; private set; }

        public string TerminatedState { get; private set; }

        public TD StateData { get; private set; }
    }
}