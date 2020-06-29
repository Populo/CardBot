using CardBot.Models;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CardBot.Modules
{
    public class CardLeaderboard
    {
        public int FistMeDaddy(SocketUser sender, SocketUser user, string reason, string color)
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
                        CardReason = reason
                    });

                    db.SaveChanges();

                    return db.CardGivings.AsQueryable().Where(c => c.Degenerate.Id == degenerate.Id).Where(c => c.Card.Id == card.Id).Count();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
                Console.WriteLine(e);
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
                Console.WriteLine(e);
                return null;
            }
        }

        public string DisplayLeaderboard()
        {
            try
            {
                using (var db = new DataContext())
                {
                    var all = db.CardGivings.ToList();
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
                Console.WriteLine(e);
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
            var sorted = new Dictionary<string, int[]>();

            while (sorted.Count < set.Count)
            {
                string highestUser = set.Keys.FirstOrDefault();
                foreach (var s in set)
                {
                    var yellow = set[highestUser][0];
                    var red = set[highestUser][1];

                    if (red < s.Value[1])
                    {
                        highestUser = s.Key;
                        red = s.Value[1];
                        yellow = s.Value[0];
                    }
                    else if (red == s.Value[1])
                    {
                        if (yellow < s.Value[0])
                        {
                            highestUser = s.Key;
                            yellow = s.Value[0];
                        }
                    }
                }

                sorted.Add(highestUser, new[] { set[highestUser][0], set[highestUser][1] });
                set.Remove(highestUser);
            }

            sorted.Add(set.Keys.FirstOrDefault(), set[set.Keys.FirstOrDefault()]);

            return sorted;
        }

        public string GetHistory(string user)
        {
            string message = $"History for {user}:\n";

            using (var db = new DataContext())
            {
                var history = db.CardGivings.AsQueryable().Where(g => g.Degenerate.Id == db.Users.AsQueryable().Where(u => u.Name == user).Select(u => u.Id).FirstOrDefault()).ToList();

                for (int count = 0; count <= 10 && count < history.Count; ++count)
                {
                    var i = history[count];

                    var color = db.Cards.AsQueryable().Where(c => c.Id == i.CardId).Select(c => c.Name).FirstOrDefault();
                    var giver = db.Users.AsQueryable().Where(u => u.Id == i.GiverId).Select(u => u.Name).FirstOrDefault();

                    message += $"**{color}** card given by **{giver}** for {i.CardReason}\n";
                }
            }

            return message;
        }
    }
}
