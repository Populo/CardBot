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
            Timer.Interval = 60 * 60 * 1000; // 1 hour
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
                    using (var db = new CardContext())
                    {
                        if (null == c.NewCard) // deleting card
                        {
                            db.CardGivings.Remove(c.Card);
                        }
                        else
                        {
                            ChangeCard(db, c, c.NewCard);
                        }

                        SendResult(c);

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

        private void ChangeCard(CardContext db, Challenge challenge, Cards newCard)
        {
            var toChange = db.CardGivings.AsQueryable().Where(c => c.Id == challenge.Card.Id).First();

            toChange.Card = newCard;
            toChange.CardId = newCard.Id;
        }

        private void SendResult(Challenge challenge)
        {
            string update = null == challenge.NewCard  ? "The card has been removed." : 
                            $"The {challenge.Card.Card.Name} has been changed to a {challenge.NewCard.Name} card.";

            challenge.Context.Channel.SendMessageAsync(text: $"{challenge.Challenger}'s challenge on {challenge.Card.Degenerate.Name}'s {challenge.Card.Card.Name} card has been reviewed. {update}");
        }
    }
}
