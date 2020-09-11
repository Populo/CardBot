using System;
using System.Collections.Generic;
using System.Text;

namespace CardBot.Models
{
    public class Challenge
    {
        private readonly DateTime _startTime;

        public bool Triggered { get
            {
                return _startTime.Add(new TimeSpan(1, 0, 0)) > DateTime.Now;
            } }

        public Cards Card { get; }

        public CardChallengeChanges Change { get; }

        public Users Challenger { get; }

        public Challenge(Cards card, Users challenger, CardChallengeChanges change)
        {
            Card = card;
            Challenger = challenger;
            Change = change;
            _startTime = DateTime.Now;
        }
    }
}
