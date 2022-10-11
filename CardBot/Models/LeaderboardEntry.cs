using System.Collections.Generic;
using System.Linq;
using CardBot.Data;
using CardBot.Bot.Modules;

namespace CardBot.Bot.Models
{
    public class LeaderboardEntry
    {
        public Users User { get; set; }
        public Dictionary<Cards, int> Givings { get; set; }
        public int Score => CalculateScore();

        private int CalculateScore()
        {
            int score = 0;

            int mult, count;
            foreach (var c in Givings.Keys)
            {
                mult = c.Value;
                count = Givings[c];

                score += count * mult;
            }

            return score;
        }

        public string PrintMarkdownRow(int longestName, int scoreHeader)
        {
            string line = $"| {User.Name.CenterString(longestName)} |";

            line += $" {this.Score.ToString().CenterString(scoreHeader)} |";
            
            Givings = SortGivings();
            
            foreach (var c in Givings.Keys)
            {
                line += $" {Givings[c].ToString().CenterString(c.Name.Length)} |";
            }

            return line;
        }

        private Dictionary<Cards, int> SortGivings()
        {
            Dictionary<Cards, int> sorted = new Dictionary<Cards, int>();
            var cards = Givings.Keys.ToList();
            cards = cards.OrderByDescending(c => c.Value).ToList();

            foreach (var c in cards)
            {
                sorted.Add(c, Givings[c]);
            }

            return sorted;
        }
    }
}