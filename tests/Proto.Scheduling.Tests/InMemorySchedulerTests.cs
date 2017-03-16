using Proto.Scheduling;
using Proto.TestFixtures;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Scheduling.Tests
{
    public class InMemorySchedulerTests
    {
        [Fact]
        public void Test()
        {
            var count = 0;
            var sent = 0;
            var dead = 0;

            var pid = Actor.Spawn(
               Actor
                   .FromFunc(context =>
                       {
                           switch (context.Message)
                           {
                               case Started _:
                                   context.ScheduleTransient("count", 0.5);
                                   sent++;
                                   break;
                               case string s:
                                   if (s == "count") count++;
                                   context.SchedulePersistent("count", TimeSpan.FromMilliseconds(500));
                                   sent++;
                                   break;
                           }
                           return Actor.Done;
                       })
                   .WithMailbox(() => new TestMailbox())
                   .WithScheduler(new InMemoryScheduler())
               );

            EventStream.Instance.Subscribe(msg => { if (msg is DeadLetterEvent letter && letter.Message == (object)"count") dead++; });

            /*
             * Started -> sent "count" to myself in 0.5s
             * "count" -> count++; sent "count" to myself in 0.5s
             * "count" -> count++; sent "count" to myself in 0.5s
             * Stopped
             * 
             * last "count" while end in dead letters
             * */

            Task.Delay(1100).Wait();
            pid.Stop();
            Task.Delay(500).Wait();

            Assert.Equal(2, count);
            Assert.Equal(3, sent);
            Assert.Equal(1, dead);
        }
    }
}
