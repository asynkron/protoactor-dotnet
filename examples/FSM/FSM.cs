using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto;

namespace FSMExample
{
    public class FSM<TData> : IActor
    {
        public delegate State<TData> StateFunction(Event<TData> fsmEvent);

        public delegate void TransitionHandler(string initialState, string nextState);

        public void When(string stateName, StateFunction func, TimeSpan? timeout = null)
        {
            Register(stateName, func, timeout);
        }

        public void StartWith(string stateName, TData stateData, TimeSpan? timeout = null)
        {
            _currentState = new State<TData>(stateName, stateData, timeout);
        }

        public State<TData> GoTo(string nextStateName)
        {
            return new State<TData>(nextStateName, _currentState.StateData);
        }

        public State<TData> GoTo(string nextStateName, TData stateData)
        {
            return new State<TData>(nextStateName, stateData);
        }

        public State<TData> Stay()
        {
            return GoTo(_currentState.StateName);
        }

        public State<TData> Stop()
        {
            return Stop(new Normal());
        }

        public State<TData> Stop(Reason reason)
        {
            return Stop(reason, _currentState.StateData);
        }

        public State<TData> Stop(Reason reason, TData stateData)
        {
            return Stay().Using(stateData).WithStopReason(reason);
        }

        public void SetStateTimeout(string state, TimeSpan? timeout)
        {
            if (!_stateTimeouts.ContainsKey(state))
                _stateTimeouts.Add(state, timeout);
            else
                _stateTimeouts[state] = timeout;
        }

        public void OnTransition(TransitionHandler transitionHandler)
        {
            _transitionEvent.Add(transitionHandler);
        }

        public void OnTermination(Action<StopEvent<TData>> terminationHandler)
        {
            _terminateEvent = terminationHandler;
        }

        public void WhenUnhandled(StateFunction stateFunction)
        {
            HandleEvent = OrElse(stateFunction, HandleEventDefault);
        }

        public string StateName
        {
            get
            {
                if (_currentState != null)
                    return _currentState.StateName;
                throw new IncorrectStateException("You must call StartWith before calling StateName.");
            }
        }

        public TData StateData
        {
            get
            {
                if (_currentState != null)
                    return _currentState.StateData;
                throw new IncorrectStateException("You must call StartWith before calling StateData.");
            }
        }

        public TData NextStateData
        {
            get
            {
                if (_nextState == null) throw new InvalidOperationException("NextStateData is only available during OnTransition");
                return _nextState.StateData;
            }
        }

        private State<TData> _currentState;
        private State<TData> _nextState;

        private readonly IList<TransitionHandler> _transitionEvent = new List<TransitionHandler>();

        private Action<StopEvent<TData>> _terminateEvent = @event => { };

        private readonly Dictionary<string, StateFunction> _stateFunctions = new Dictionary<string, StateFunction>();
        private readonly Dictionary<string, TimeSpan?> _stateTimeouts = new Dictionary<string, TimeSpan?>();

        public Task ReceiveAsync(IContext context)
        {
            var fsmEvent = new Event<TData>(context.Message, _currentState.StateData);
            ProcessEvent(fsmEvent, context);

            return Actor.Done;
        }

        private void Register(string name, StateFunction function, TimeSpan? timeout)
        {
            if (_stateFunctions.ContainsKey(name))
            {
                _stateFunctions[name] = OrElse(_stateFunctions[name], function);
                _stateTimeouts[name] = _stateTimeouts[name] ?? timeout;
            }
            else
            {
                _stateFunctions.Add(name, function);
                _stateTimeouts.Add(name, timeout);
            }
        }

        private void ProcessEvent(Event<TData> fsmEvent, IContext context)
        {
            var stateFunc = _stateFunctions[_currentState.StateName];
            State<TData> upcomingState = null;

            if (stateFunc != null)
            {
                upcomingState = stateFunc(fsmEvent);
            }

            if (upcomingState == null)
            {
                upcomingState = HandleEvent(fsmEvent);
            }

            ApplyState(upcomingState, context);
        }


        private void ApplyState(State<TData> upcomingState, IContext context)
        {
            if (upcomingState.StopReason == null)
            {
                MakeTransition(upcomingState, context);
                return;
            }
            var replies = upcomingState.Replies;
            replies.Reverse();
            foreach (var reply in replies)
            {
                context.Sender.Tell(reply);
            }
            Terminate(upcomingState);
            context.Self.Stop();
        }

        private StateFunction HandleEventDefault
        {
            get
            {
                return delegate (Event<TData> @event)
                {
                    return Stay();
                };
            }
        }

        private StateFunction _handleEvent;

        private StateFunction HandleEvent
        {
            get { return _handleEvent ?? (_handleEvent = HandleEventDefault); }
            set { _handleEvent = value; }
        }

        private static StateFunction OrElse(StateFunction original, StateFunction fallback)
        {
            StateFunction chained = delegate(Event<TData> @event)
            {
                var originalResult = original.Invoke(@event);
                if (originalResult == null) return fallback.Invoke(@event);
                return originalResult;
            };

            return chained;
        }

        private void HandleTransition(string previous, string next)
        {
            foreach (var tran in _transitionEvent)
            {
                tran.Invoke(previous, next);
            }
        }

        private void MakeTransition(State<TData> upcomingState, IContext context)
        {
            if (!_stateFunctions.ContainsKey(upcomingState.StateName))
            {
                Terminate(
                    Stay()
                        .WithStopReason(
                            new Failure(String.Format("Next state {0} does not exist", upcomingState.StateName))));
            }
            else
            {
                var replies = upcomingState.Replies;
                replies.Reverse();
                foreach (var r in replies)
                {
                    context.Sender.Tell(r);
                }
                if (!_currentState.StateName.Equals(upcomingState.StateName))
                {
                    _nextState = upcomingState;
                    HandleTransition(_currentState.StateName, _nextState.StateName);
                    _nextState = null;
                }
                _currentState = upcomingState;
            }
        }

        private void Terminate(State<TData> upcomingState)
        {
            if (_currentState.StopReason == null)
            {
                var reason = upcomingState.StopReason;
                _currentState = upcomingState;

                var stopEvent = new StopEvent<TData>(reason, _currentState.StateName, _currentState.StateData);
                _terminateEvent(stopEvent);
            }
        }
    }
}
