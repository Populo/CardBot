using CardBot.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CardBot.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        private string CardRole = "CardBot",
            CardRoleAdmin = "CardAdmin";

        private string CardChannel = "card-tracker",
            CardErrorChannel = "card-tracker-errors";

        private readonly Emoji Frown = new Emoji("😦");
        private readonly Emoji Smile = new Emoji("🙂");
        private readonly ulong NoUTopiaServerId = 140642236978167808;

        private CardLeaderboard Leaderboard = new CardLeaderboard();

        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        [Command("challenge")]
        public async Task ChallengeCard(string user, string newColor, params string[] reason)
        {
            Cards newCard = null;
            
            if (reason.Length == 0)
            {
                reason = new[] {"none"};
            }

            string reasonForChallenge = string.Join(' ', reason);

            if (newColor == "delete" || newColor == "remove")
            {
                newColor = "none";
            }
            else
            {
                using (var db = new CardContext())
                {
                    newCard = db.Cards.AsQueryable()
                        .First(c => c.Name == newColor);
                }
            }

            if (null == newCard && newColor != "none")
            {
                await ReplyAsync($"Cannot find {newColor}. Does the card exist?");
            }

            var user1 = GetUser(user);
            CardGivings card;

            using (CardContext db = new CardContext())
            {
                card = db.CardGivings.AsQueryable()
                    .Where(c => c.DegenerateId.Equals(db.Users.AsQueryable().Where(u => u.Name == user1.Username).Select(u => u.Id).First()))
                    .OrderBy(c => c.TimeStamp).Last();

                card.Card = db.Cards.AsQueryable().Where(c => c.Id == card.CardId).First();
                card.Degenerate = db.Users.AsQueryable().Where(u => u.Id == card.DegenerateId).First();
                card.Giver = db.Users.AsQueryable().Where(u => u.Id == card.GiverId).First();

                if (card.Card.Id == newCard.Id)
                {
                    await ReplyAsync("You cannot change the card to the same color");
                    return;
                }
            }

            string proposal = newColor == "none"
                ? "delete the card"
                : $"convert their {card.Card.Name} card to a {newCard.Name} card";
            var roleTag = Context.Guild.Roles.First(r => r.Name == CardRole).Mention;

            var message = await ReplyAsync($"{roleTag}: {Context.User.Mention} has challenged {user1.Mention}'s last {card.Card.Name} card.  {Context.User.Username} is proposing to {proposal}.\n\n" +
                                           $"{Context.User.Username}'s reasoning:\n ```{reasonForChallenge}```\n\n" +
                                           $"Place your votes below.  The votes will be counted in 1 hour.");
            await message.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });

            var challenges = ChallengeSingleton.Instance;
            challenges.NewChallenge(new Challenge(card, Context.User, newCard, message.Id, Context));
        }

        [Command("score")]
        public async Task ShowScoreboard()
        {
            await Context.Message.AddReactionAsync(Smile);
            var serverId = Context.Guild.Id;
            await ReplyAsync(Leaderboard.BuildLeaderboard(serverId));
        }

        private SocketUser GetUser(string user)
        {
            var mention = Context.Message.MentionedUsers.FirstOrDefault(x => x.Mention == user);
            if (mention == null)
            {
                var role = Context.Guild.Roles.Where(r => r.Name == CardRole).FirstOrDefault();
                mention = Context.Guild.Users
                    .Where(x => x.Username.Contains(user, StringComparison.CurrentCultureIgnoreCase) || x.Nickname.Contains(user, StringComparison.CurrentCultureIgnoreCase))
                    .Where(x => x.Roles.Contains(role))
                    .FirstOrDefault();
            }
            if (mention == null)
            {
                var splittedString = user.Split('@');
                user = $"{splittedString[0]}@!{splittedString[1]}";
                mention = Context.Message.MentionedUsers.FirstOrDefault(x => x.Mention == user);
            }

            return mention;
        }

        private Cards GetCard(string color, ulong serverId)
        {
            Cards card = null;
            
            using (var db = new CardContext())
            {
                card = db.Cards.AsQueryable()
                    .Where(c => c.ServerId == serverId)
                    .Where(c => c.Name.ToLower() == color.ToLower())
                    .FirstOrDefault();
            }

            return card;
        }

        private async Task AddCard(SocketUser user, Cards card, string reason)
        {
            IEmote[] emotes = new IEmote[2]
            {
                Frown,
                new Emoji(card.Emoji)
            };
            
            await Context.Message.AddReactionsAsync(emotes);

            var sender = Context.User;
            var serverId = Context.Guild.Id;
            var adminChannel = Context.Guild.Channels.First(c => c.Name == CardErrorChannel);

            try
            {
                var cardCount = Leaderboard.FistMeDaddy(sender, user, reason, card, serverId);

                await ReplyAsync($"{user.Mention} now has {cardCount} {card.Name} cards.");
            }
            catch (Exception e)
            {
                await Context.Guild.GetTextChannel(adminChannel.Id).SendMessageAsync(e.Message);
                await ReplyAsync("That didnt work. frown :(");
            }
        }

        [Command("history")]
        public async Task GetHistory(string user, int count = 10)
        {
            await Context.Message.AddReactionAsync(Smile);
            var serverId = Context.Guild.Id;
            var mention = GetUser(user);

            var reply = Leaderboard.GetHistory(mention.Username, count, serverId);

            await ReplyAsync(reply);
        }

        [Command("card")]
        public async Task GiveCard(string color, string user, params string[] reason)
        {
            if (reason.Length == 0)
            {
                reason = new[] {"being an ass"};
            }

            string r = string.Join(' ', reason);
            
            var serverId = Context.Guild.Id;
            
            var mention = GetUser(user);
            var card = GetCard(color, serverId);

            if (card == null)
            {
                await ReplyAsync("This card does not exist. Please create it with !create");
                return;
            }

            await AddCard(mention, card, r);
        }
        
        #region AdminCommands
        [Command("create")]
        public async Task CreateCard(string name, int value, string emoji)
        {
            if (!IsAdminUser(Context.Message.Author))
            {
                await ReplyAsync("You don't have permission to do that :(");
                return;
            }

            ulong serverId = Context.Guild.Id;
            Cards c;

            using (var db = new CardContext())
            {
                c = db.Cards.AsQueryable()
                    .First(card => card.Name == name);

                if (c != null)
                {
                    await ReplyAsync($"{name} card already exists");
                }
                
                c = new Cards()
                {
                    Name = name,
                    Emoji = emoji.ToString(),
                    Value = value,
                    Id = Guid.NewGuid(),
                    ServerId = serverId
                };

                db.Cards.Add(c);

                await db.SaveChangesAsync();
            }

            c = null;
            using (var db = new CardContext())
            {
                c = db.Cards.AsQueryable()
                    .First(c => c.Name == name);
            }

            await ReplyAsync($"Created {c.Name} card with a value of {c.Value} yellow cards.");
            await Context.Message.AddReactionAsync(new Emoji(c.Emoji));
        }

        [Command("delete")]
        public async Task DeleteCard(string color)
        {
            if (!IsAdminUser(Context.Message.Author))
            {
                await ReplyAsync("You don't have permission to do that :(");
                return;
            }

            if (!ConfirmChange().Result)
            {
                await ReplyAsync("Change not confirmed.");
                return;
            }

            using (var db = new CardContext())
            {
                Cards c = db.Cards.AsQueryable()
                    .First(card => card.Name == color);
                db.Cards.Remove(c);
                db.CardGivings.RemoveRange(db.CardGivings.AsQueryable()
                    .Where(g => g.Card == c));
            }
        }

        [Command("value")]
        public async Task ChangeCardValue(string color, int newVal)
        {
            if (!IsAdminUser(Context.Message.Author))
            {
                await ReplyAsync("You don't have permission to do that :(");
                return;
            }

            if (!ConfirmChange().Result)
            {
                await ReplyAsync("Change not confirmed.");
                return;
            }

            Cards c;
            using (var db = new CardContext())
            {
                c = db.Cards.AsQueryable()
                    .First(card => card.Name == color);

                c.Value = newVal;

                db.Cards.Update(c);

                db.SaveChanges();
            }

            await ReplyAsync($"{c.Name} cards are now worth {c.Value} yellow cards.");
        }

        private async Task<bool> ConfirmChange()
        {
            bool confirmed = false;
            
            Random r = new Random();
            char randomChar = (char) r.Next(97, 123);
            
            await ReplyAsync($"To proceed reply with `{randomChar}`.");
            Thread.Sleep(10 * 1000);

            var latestMessages = Context.Channel.GetMessagesAsync(5).GetAsyncEnumerator().Current;
            var m = latestMessages.Where(m => m.Author == Context.User).Where(m => m.Content == randomChar.ToString()).FirstOrDefault();
            if (m != null)
            {
                confirmed = true;
                await m.AddReactionAsync(new Emoji("👍"));
            }
            
            return confirmed;
        }

        private bool IsAdminUser(SocketUser user)
        {
            return Context.Guild.Roles.First(r => r.Name == CardRoleAdmin).Members.Contains(user);
        }
        #endregion
    }
}
