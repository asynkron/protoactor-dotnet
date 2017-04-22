using System;
using System.Linq;
using System.Threading;
using Proto;

namespace FSMExample
{
    class Program
    {
        public class Talker : FSM<string>
        {
            private readonly string[] _greetings = { "hey", "salut", "hello", "hi" };
            private readonly string[] _standartPhrases =
            {
                "Wow! It's sooo interesting!",
                "Thanks! you too.",
                "Okay, continue",
                "I'm glad to talk with you.",
                "Are you crazy??? Why did you said that???",
            };
            private const string GreetingState = "GreetingState";
            private const string HowAreYouState = "HowAreYouState";
            private const string TalkingState = "TalkingState";
            private const int MaxNameWordsLength = 5;
            private Random _rnd;

            public Talker()
            {
                _rnd = new Random();

                When(GreetingState, @evt =>
                    {
                        var message = @evt.FsmEvent as string;
                        if (message != null)
                        {
                            if (_greetings.Contains(message.ToLower()))
                            {
                                TellAnswer("Hello! What is your name.");
                                return GoTo(HowAreYouState);
                            }

                            TellAnswer("First of all you have to say hello.");
                        }

                        return null;
                    }
                );

                When(HowAreYouState, @evt =>
                {
                    var message = @evt.FsmEvent as string;
                    if (message != null)
                    {
                        var trimedWords = message.Split(' ').Select(w => w.Trim()).ToList();
                        var numberOfWords = trimedWords.Count(w => w.Any());

                        if (numberOfWords > MaxNameWordsLength)
                        {
                            TellAnswer("Are you sure that you have so big name? Maybe you have some some shortname?");
                            return GoTo(HowAreYouState);
                        }

                        var fixedName = string.Join(" ", trimedWords);
                        TellAnswer(string.Format("Okay, {0}. Nice to meet you.", fixedName));
                        return GoTo(TalkingState, fixedName);
                    }

                    return null;
                });

                When(TalkingState, @evt =>
                {
                    var message = @evt.FsmEvent as string;
                    if (message != null)
                    {
                        message = message.Trim();

                        if (string.Compare(message, "bye", StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            TellAnswer("It was nice to talk with you! bye!");
                            return Stop();
                        }

                        var ordnalAnswer = _standartPhrases[_rnd.Next(0, _standartPhrases.Length)];
                        TellAnswer(ordnalAnswer);
                        return GoTo(TalkingState);
                    }

                    return null;
                });

                StartWith(GreetingState, string.Empty);
            }

            private static void TellAnswer(string answer)
            {
                Console.WriteLine(string.Format("[ANSWER]: {0}", answer));
            }
        }

        private static void TellToTalker(string message, PID pid)
        {
            Console.WriteLine(string.Format("[USER]: {0}", message));
            pid.Tell(message);
            Thread.Sleep(2000);
        }

        static void Main(string[] args)
        {
            var props = Actor.FromProducer(() => new Talker());
            var pid = Actor.Spawn(props);
            TellToTalker("mmm... ", pid);
            TellToTalker("hey", pid);
            TellToTalker("John Bill Boney Money Philip Bob", pid);
            TellToTalker("Bill", pid);
            TellToTalker("I want to talk", pid);
            TellToTalker("I like watching moovies", pid);
            TellToTalker("I like travelling", pid);
            TellToTalker("I like pizza", pid);
            TellToTalker("Bye", pid);

            Console.ReadLine();
        }
    }
}
