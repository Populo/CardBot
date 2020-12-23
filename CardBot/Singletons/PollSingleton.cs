using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using CardBot.Models;
using CardBot.Modules;
using NLog;

namespace CardBot.Singletons
{
    public sealed class PollSingleton
    {
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
     
        Timer Timer;

        private static readonly Lazy<PollSingleton> single = new Lazy<PollSingleton>(() => new PollSingleton());

        public static PollSingleton Instance => single.Value;

        public List<Poll> Polls;
        
        private PollSingleton() {
            Polls = new List<Poll>();
            Timer = new Timer();
            #if DEBUG
            Timer.Interval = 15 * 1000; // 15 seconds
            #else
            Timer.Interval = 60 * 60 * 1000; // 1 hour
            #endif            
            Timer.AutoReset = true;
            Timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Logger.Log(LogLevel.Info, "Timer Exec");
            var toExec = Polls.Where(c => c.Triggered).ToList();

            toExec.All(e => Polls.Remove(e));

            foreach (var p  in toExec)
            {
                if (p.Majority)
                {
                    p.Execute();
                }
                else
                {
                    var message = new StringBuilder();
                    message.AppendLine($"{p.Context.User.Username}'s poll could not get a majority vote. :(");
                    if (p.Type == PollType.GIVE)
                    {
                        int totalCards = GiveFailingCard(p);
                        message.AppendLine(
                            $"{p.CardGiving.Degenerate.Name} has been given a {p.Card.Name} card instead of a {p.CardGiving.Card.Name}.  They now have {totalCards} {p.Card.Name} cards.");
                    }
                    p.Context.Channel.SendMessageAsync(message.ToString());
                }
            }

            if (Polls.Count == 0) Timer.Stop();
        }

        private int GiveFailingCard(Poll poll)
        {
            var helper = new CardLeaderboard();
            return helper.GiveCard(poll.Context.User, poll.Receiver, poll.CardGiving.CardReason, poll.Card,
                poll.CardGiving.ServerId, poll.Context);
        }

        public void NewPoll(Poll p)
        {
            Polls.Add(p);
            if (!Timer.Enabled) Timer.Start();
        }
    }
}