using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CardBot.Models
{
    public sealed class ChallengeSingleton
    {
        private static readonly Lazy<ChallengeSingleton> single = new Lazy<ChallengeSingleton>(() => new ChallengeSingleton());

        public static ChallengeSingleton Instance { get { return single.Value; } }

        public List<Challenge> Challenges;

        private ChallengeSingleton() {
            Challenges = new List<Challenge>();
        }
    }
}
