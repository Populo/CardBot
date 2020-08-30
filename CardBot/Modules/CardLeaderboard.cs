using CardBot.Models;
using Discord.WebSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace CardBot.Modules
{
    public class CardLeaderboard
    {
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public int FistMeDaddy(SocketUser sender, SocketUser user, string reason, string color, ulong serverId)
        {
            try
            {
                using (var db = new DataContext())
                {
                    var card = db.Cards.AsQueryable().Where(c => c.Name == color).FirstOrDefault();
                    var giver = db.Users.AsQueryable().Where(u => u.Name == sender.Username).FirstOrDefault();
                    var degenerate = db.Users.AsQueryable().Where(u => u.Name == user.Username).FirstOrDefault();

                    if (giver == null)
                    {
                        giver = CreateUser(sender, db);
                    }

                    if (degenerate == null)
                    {
                        degenerate = CreateUser(user, db);
                    }

                    if (card == null)
                    {
                        card = CreateCard(color, db);
                    }

                    db.CardGivings.Add(new CardGivings
                    {
                        Id = new Guid(),
                        CardId = card.Id,
                        Card = card,
                        GiverId = giver.Id,
                        Giver = giver,
                        DegenerateId = degenerate.Id,
                        Degenerate = degenerate,
                        CardReason = reason,
                        ServerId = serverId,
                        TimeStamp = DateTime.Now
                    });

                    db.SaveChanges();

                    return db.CardGivings.AsQueryable().Where(c => c.Degenerate.Id == degenerate.Id).Where(c => c.Card.Id == card.Id).Where(c => c.ServerId == serverId).Count();
                }
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return 0;
            }
        }

        private Users CreateUser(SocketUser sender, DataContext db)
        {
            try
            {
                var u = db.Users.Add(new Users
                {
                    Id = new Guid(),
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

        private Cards CreateCard(string color, DataContext db)
        {
            try
            {
                var c = db.Cards.Add(new Cards
                {
                    Id = new Guid(),
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

        public string DisplayLeaderboard(ulong serverId)
        {
            try
            {
                using (var db = new DataContext())
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
            using (var db = new DataContext())
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

            using (var db = new DataContext())
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
