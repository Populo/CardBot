using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;

namespace CardBot.Models
{
    public sealed class ChallengeSingleton
    {
        Timer Timer;

        private static readonly Lazy<ChallengeSingleton> single = new Lazy<ChallengeSingleton>(() => new ChallengeSingleton());

        public static ChallengeSingleton Instance { get { return single.Value; } }

        public List<Challenge> Challenges;

        private ChallengeSingleton() {
            Challenges = new List<Challenge>();
            Timer = new Timer();
#if DEBUG
            Timer.Interval = 10 * 1000; // 10 seconds
            Console.WriteLine("Debugging.  Challenge Timer: 10 seconds");
#else
            Timer.Interval = 60 * 60 * 1000; // 1 hour
#endif
            Timer.AutoReset = true;
            Timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("Timer Exec");
            var toExec = Challenges.Where(c => c.Triggered).ToList();

            toExec.All(e => Challenges.Remove(e));

            foreach (var c  in toExec)
            {
                if (c.Overturned)
                {
                    using (var db = new DataContext())
                    {
                        switch (c.Change)
                        {
                            case CardChallengeChanges.REMOVE:
                                db.CardGivings.Remove(c.Card);
                                SendResult(c, CardChallengeChanges.REMOVE);
                                break;
                            case CardChallengeChanges.YELLOW:
                                ChangeCard(db, c, CardChallengeChanges.YELLOW);
                                break;
                            case CardChallengeChanges.RED:
                                ChangeCard(db, c, CardChallengeChanges.RED);
                                break;
                        }

                        db.SaveChanges();
                    }  
                }
                else
                {
                    c.Context.Channel.SendMessageAsync(text: $"{c.Challenger}'s challenge on {c.Card.Degenerate.Name}'s {c.Card.Card.Name} card has been reviewed. The card stands.");
                }
            }

            if (Challenges.Count == 0) Timer.Stop();
        }

        public void NewChallenge(Challenge c)
        {
            Challenges.Add(c);
            if (!Timer.Enabled) Timer.Start();
        }

        private void ChangeCard(DataContext db, Challenge challenge, CardChallengeChanges change)
        {
            var toChange = db.CardGivings.AsQueryable().Where(c => c.Id == challenge.Card.Id).First();

            var newCard = change == CardChallengeChanges.RED ? 
                                        db.Cards.AsQueryable().Where(c => c.Name == "Red").First() : 
                                        db.Cards.AsQueryable().Where(c => c.Name == "Yellow").First();

            toChange.Card = newCard;
            toChange.CardId = newCard.Id;

            SendResult(challenge, change);
        }

        private void SendResult(Challenge challenge, CardChallengeChanges change)
        {
            string update = change == CardChallengeChanges.REMOVE ? "The card has been removed." : 
                            change == CardChallengeChanges.RED ? "The card has been upgraded to a red card.  May God have mercy on your soul." : 
                                "The card has been downgraded to a yellow card.";

            challenge.Context.Channel.SendMessageAsync(text: $"{challenge.Challenger}'s challenge on {challenge.Card.Degenerate.Name}'s {challenge.Card.Card.Name} card has been reviewed. {update}");
        }
    }
}
