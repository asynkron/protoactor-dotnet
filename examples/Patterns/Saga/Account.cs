using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Saga.Messages;

namespace Saga
{
    class Account : IActor
    {
        private readonly string _name;
        private readonly double _serviceUptime;
        private readonly double _refusalProbability;
        private readonly double _busyProbability;
        private readonly Dictionary<PID, object> _processedMessages = new Dictionary<PID, object>();
        private decimal _balance = 10;
        private readonly Random _random;

        public Account(string name, double serviceUptime, double refusalProbability, double busyProbability, Random random)
        {
            _name = name;
            _serviceUptime = serviceUptime;
            _refusalProbability = refusalProbability;
            _busyProbability = busyProbability;
            _random = random;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Credit msg when AlreadyProcessed(msg.ReplyTo):
                    return Reply(msg.ReplyTo);
                case Credit msg:
                    return AdjustBalance(msg.ReplyTo, msg.Amount);
                case Debit msg when AlreadyProcessed(msg.ReplyTo):
                    return Reply(msg.ReplyTo);
                case Debit msg when msg.Amount + _balance >= 0:
                    return AdjustBalance(msg.ReplyTo, msg.Amount);
                case Debit msg:
                    msg.ReplyTo.Tell(new InsufficientFunds());
                    break;
                case GetBalance _:
                    context.Respond(_balance);
                    break;
            }
            return Actor.Done;
        }
        
        private Task Reply(PID replyTo)
        {
            replyTo.Tell(_processedMessages[replyTo]);
            return Actor.Done;
        }

        /// <summary>
        ///  we want to simulate the following: 
        ///  * permanent refusals to process the message
        ///  * temporary refusals to process the message 
        ///  * failures before updating the balance
        ///  * failures after updating the balance
        ///  * slow processing
        ///  * successful processing
        /// </summary>
        private Task AdjustBalance(PID replyTo, decimal amount)
        {
            if (RefusePermanently())
            {
                _processedMessages.Add(replyTo, new Refused());
                replyTo.Tell(new Refused());
            }
                
            if (Busy())
                replyTo.Tell(new ServiceUnavailable());
            
            // generate the behavior to be used whilst processing this message
            var behaviour = DetermineProcessingBehavior();
            if (behaviour == Behavior.FailBeforeProcessing)
                return Failure(replyTo);
            
            // simulate potential long-running process
            Thread.Sleep(_random.Next(0, 150));
            
            _balance += amount;
            _processedMessages.Add(replyTo, new OK());
            
            // simulate chance of failure after applying the change. This will
            // force a retry of the operation which will test the operation
            // is idempotent
            if (behaviour == Behavior.FailAfterProcessing)
                return Failure(replyTo);
            
            replyTo.Tell(new OK());
            return Actor.Done;
        }

        private bool Busy()
        {
            var comparsion = _random.NextDouble() * 100;
            return comparsion <= _busyProbability;
        }

        private bool RefusePermanently()
        {
            
            var comparsion = _random.NextDouble() * 100;
            return comparsion <= _refusalProbability;
        }

        private Task Failure(PID replyTo)
        {
            replyTo.Tell(new InternalServerError());
            return Actor.Done;
        }

        private Behavior DetermineProcessingBehavior()
        {
            var comparision = _random.NextDouble() * 100;
            if (comparision > _serviceUptime)
            {
                return _random.NextDouble() * 100 > 50 ? Behavior.FailBeforeProcessing : Behavior.FailAfterProcessing;
            }
            return Behavior.ProcessSuccessfully;
        }

        private bool AlreadyProcessed(PID replyTo)
        {
            return _processedMessages.ContainsKey(replyTo);
        }

        private enum Behavior
        {
            FailBeforeProcessing,
            FailAfterProcessing,
            ProcessSuccessfully
        }
    }
    
    
}