using CardBot.Models;
using Discord.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace CardBot.Modules
{
    public class CardLeaderboard
    {
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public int FistMeDaddy(SocketUser sender, SocketUser user, string reason, Cards card, ulong serverId)
        {
            try
            {
                using (var db = new CardContext())
                {
                    var giver = db.Users.AsQueryable().Where(u => u.Name == sender.Username).FirstOrDefault();
                    var degenerate = db.Users.AsQueryable().Where(u => u.Name == user.Username).FirstOrDefault();
                    var givenCard = db.Cards.AsQueryable().Where(c => c.Id == card.Id).FirstOrDefault();

                    if (giver == null)
                    {
                        giver = CreateUser(sender, db);
                    }

                    if (degenerate == null)
                    {
                        degenerate = CreateUser(user, db);
                    }
                    
                    var newCard = new CardGivings
                    {
                        Id = Guid.NewGuid(),
                        CardId = givenCard.Id,
                        Card = givenCard,
                        GiverId = giver.Id,
                        Giver = giver,
                        DegenerateId = degenerate.Id,
                        Degenerate = degenerate,
                        CardReason = reason,
                        ServerId = serverId,
                        TimeStamp = DateTime.Now
                    };

                    db.CardGivings.Add(newCard);

                    db.SaveChanges();

                    return db.CardGivings.AsQueryable().Where(c => c.Degenerate.Id == degenerate.Id).Where(c => c.Card.Id == card.Id).Where(c => c.ServerId == serverId).Count();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
                throw;
                return 0;
            }
        }

        private Users CreateUser(SocketUser sender, CardContext db)
        {
            try
            {
                var u = db.Users.Add(new Users
                {
                    Id = Guid.NewGuid(),
                    Name = sender.Username
                }); ;

                db.SaveChanges();

                return u.Entity;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return null;
            }
        }

        private Cards CreateCard(string color, CardContext db)
        {
            try
            {
                var c = db.Cards.Add(new Cards
                {
                    Id = Guid.NewGuid(),
                    Name = color
                });

                db.SaveChanges();

                return c.Entity;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return null;
            }
        }

        public string BuildLeaderboard(ulong serverId)
        {
            StringBuilder message = new StringBuilder();
            
            List<Cards> cards;
            List<CardGivings> givings;
            List<Users> users;
            
            using (var db = new CardContext())
            {
                cards = db.Cards.AsQueryable()
                    .Where(c => c.ServerId == serverId).OrderByDescending(c => c.Value).ToList();

                givings = db.CardGivings.AsQueryable()
                    .Where(c => c.ServerId == serverId).ToList();

                var userIds = givings.Select(u => u.DegenerateId).Distinct().ToList();
                users = db.Users.AsQueryable()
                    .Where(u => userIds.Any(i => i == u.Id)).ToList();
            }

            message.Append("Current Leaderboard:\n```");

            int longestUser = users.OrderByDescending(u => u.Name.Length).First().Name.Length + 1;
            var players = BuildPlayerInfo(givings, users, cards);

            string userHeader = "User";
            string scoreHeader = "Score";
            int scoreWidth = scoreHeader.Length;
            
            if (longestUser < userHeader.Length) longestUser = userHeader.Length;
            string longestScore = players.OrderByDescending(p => p.Score).First().Score.ToString();
            if (longestScore.Length > scoreWidth)
            {
                scoreWidth = longestScore.Length;
            }
            
            
            // build header
            string header = "", line = "", segment = "";
            header = $"| {userHeader.CenterString(longestUser)} |";
            line = $"|{new string('-', header.Length - 2)}+";
            segment = $" {scoreHeader.CenterString(scoreWidth)} |";
            line += $"{new string('-', segment.Length - 1)}+";
            header += segment;
            foreach (var c in cards)
            {
                segment = $" {c.Name} |";
                line += $"{new string('-', segment.Length - 1)}+";
                header += segment;
            }

            line = line.Substring(0, line.Length - 1) + '|'; 

            message.AppendLine(header);
            message.AppendLine(line);

            foreach (var p in players)
            {
                message.AppendLine(p.PrintMarkdownRow(longestUser, scoreWidth));
            }
            
            // end code block
            message.Append("```");

            return message.ToString();
        }

        private List<LeaderboardEntry> BuildPlayerInfo(List<CardGivings> givings, List<Users> users, List<Cards> cards)
        {
            List<LeaderboardEntry> entries = new List<LeaderboardEntry>();

            LeaderboardEntry current = new LeaderboardEntry();
            int count = 0;
            
            foreach (var u in users)
            {
                current.User = u;
                current.Givings = new Dictionary<Cards, int>();
                foreach (var c in cards)
                {
                    count = givings.Where(g => g.DegenerateId == u.Id).Where(g => g.CardId == c.Id).Count();
                    current.Givings.Add(c, count);
                }
                entries.Add(current);
                current = new LeaderboardEntry();
            }

            entries = entries.OrderByDescending(e => e.Score).ToList();
            
            return entries;
        }

        public string DisplayLeaderboard(ulong serverId)
        {
            try
            {
                using (var db = new CardContext())
                {
                    var all = db.CardGivings.AsQueryable().Where(c => c.ServerId == serverId).ToList();
                    if (all.Count > 0)
                    {
                        var set = BuildScoreboard(all);

                        var message = "Current Leaderboard:\n" +
                                      "```" +
                                      "Username|Red Cards|Yellow Cards\n";

                        foreach (var s in set)
                        {
                            message += $"{s.Key}|{s.Value[1]}|{s.Value[0]}\n";
                        }

                        message += "```";

                        return message;
                    }
                    return "No cards :(";
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return "That didn't work. frown :(";
            }
        }

        private Dictionary<string, int[]> BuildScoreboard(List<CardGivings> set)
        {
            var s = new Dictionary<string, int[]>();
            using (var db = new CardContext())
            {
                foreach (var i in set)
                {
                    var user = db.Users.AsQueryable().Where(u => u.Id == i.DegenerateId).Select(u => u.Name).FirstOrDefault();
                    var card = db.Cards.AsQueryable().Where(c => c.Id == i.CardId).Select(c => c.Name).FirstOrDefault();

                    if (!s.ContainsKey(user))
                    {
                        s.Add(user, new[] { 0, 0 });
                    }

                    if (card == "Yellow")
                    {
                        s[user][0]++;
                    }
                    else if (card == "Red")
                    {
                        s[user][1]++;
                    }
                }
            }

            s = SortScoreboard(s);

            return s;
        }

        private Dictionary<string, int[]> SortScoreboard(Dictionary<string, int[]> set)
        {
            var scoreboard = new Dictionary<string, int[]>();

            var scores = CalculateScores(set);

            var sorted = from s in scores orderby s.Value descending select s;

            foreach (var i in sorted)
            {
                scoreboard.Add(i.Key, set[i.Key]);
            }

            return scoreboard;
        }

        private Dictionary<string, int> CalculateScores(Dictionary<string, int[]> set)
        {
            var s = new Dictionary<string, int>();

            foreach(var n in set)
            {
                int score = n.Value[0];
                score += (n.Value[1] * 10);

                s.Add(n.Key, score);
            }

            return s;
        }

        public string GetHistory(string user, int toShow, ulong serverId)
        {
            string message = $"History for {user}:\n";

            using (var db = new CardContext())
            {
                var history = db.CardGivings.AsQueryable()
                                            .Where(g => g.Degenerate.Id == db.Users.AsQueryable()
                                                    .Where(u => u.Name == user).Select(u => u.Id).FirstOrDefault())
                                            .Where(c => c.ServerId == serverId)
                                            .OrderByDescending(x => x.TimeStamp).ToList();

                if (history.Count > 0)
                {
                    for (int count = 0; count <= toShow && count < history.Count; ++count)
                    {
                        var i = history[count];

                        var color = db.Cards.AsQueryable().Where(c => c.Id == i.CardId).Select(c => c.Name).FirstOrDefault();
                        var giver = db.Users.AsQueryable().Where(u => u.Id == i.GiverId).Select(u => u.Name).FirstOrDefault();

                        message += $"**{color}** card given by **{giver}**: {i.CardReason}\n";
                    }
                }
                else
                {
                    message = $"{user} does not have any cards. smile :)";
                }
                
            }

            return message;
        }
    }
}
