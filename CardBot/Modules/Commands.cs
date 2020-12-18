using CardBot.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CardBot.Singletons;

namespace CardBot.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        public static string CardRole = "CardBot",
            CardRoleAdmin = "CardAdmin";

        public static string CardChannel = "card-tracker",
            CardErrorChannel = "card-tracker-errors";

        private const int MAX_CHALLENGE_TIME = 3;

        private readonly Emoji Frown = new Emoji("😦");
        private readonly Emoji Smile = new Emoji("🙂");
        private readonly ulong NoUTopiaServerId = 140642236978167808;

        private CardLeaderboard Leaderboard = new CardLeaderboard();

        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        #region UserCommands
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

                if (card.TimeStamp.AddHours(MAX_CHALLENGE_TIME) < DateTime.Now)
                {
                    await ReplyAsync("This card is too old to challenge.");
                    return;
                }
                
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

            var polls = PollSingleton.Instance;
            polls.NewPoll(new Poll(
                    Context.User,
                    PollType.CHALLENGE,
                    Context,
                    message.Id,
                    card,
                    newCard
                ));
        }

        [Command("score")]
        public async Task ShowScoreboard()
        {
            await Context.Message.AddReactionAsync(Smile);
            var serverId = Context.Guild.Id;
            await ReplyAsync(Leaderboard.BuildLeaderboard(serverId));
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
        
        #endregion

        #region Utility
        private bool IsAdminUser(SocketUser user)
        {
            return Context.Guild.Roles.First(r => r.Name == CardRoleAdmin).Members.Contains(user);
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
                var cardCount = Leaderboard.GiveCard(sender, user, reason, card, serverId);

                await ReplyAsync($"{user.Mention} now has {cardCount} {card.Name} cards.");
            }
            catch (Exception e)
            {
                await Context.Guild.GetTextChannel(adminChannel.Id).SendMessageAsync(e.Message);
                await ReplyAsync("That didnt work. frown :(");
            }
        }
        
        #endregion

        #region AdminCommands
        [Command("create")]
        public async Task CreateCard(string name, int value, string emoji)
        {
            if (!IsAdminUser(Context.User))
            {
                await ReplyAsync("You don't have permission to do that :(");
                return;
            }

            ulong serverId = Context.Guild.Id;
            Cards c;

            using (var db = new CardContext())
            {
                c = db.Cards.AsQueryable()
                    .FirstOrDefault(card => card.Name.ToUpper() == name.ToUpper());

                if (c != null)
                {
                    await ReplyAsync($"{name} card already exists");
                    return;
                }
            }
            
            c = new Cards()
            {
                Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name),
                Emoji = emoji,
                Value = value,
                Id = Guid.NewGuid(),
                ServerId = serverId
            };
            
            var roleTag = Context.Guild.Roles.First(r => r.Name == CardRole).Mention;

            var message = await ReplyAsync($"{roleTag}: {Context.User.Mention} is proposing to create a {c.Name} card worth {c.Value} points.\n\n" +
                                           $"Place your votes below.  The votes will be counted in 1 hour.");
            await message.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });

            var polls = PollSingleton.Instance;
            polls.NewPoll(new Poll(
                Context.User,
                PollType.CREATE,
                Context,
                message.Id,
                null,
                c));
        }

        [Command("delete")]
        public async Task DeleteCard(string color)
        {
            if (!IsAdminUser(Context.Message.Author))
            {
                await ReplyAsync("You don't have permission to do that :(");
                return;
            }

            Cards c = null;
            
            using (var db = new CardContext())
            {
                c = GetCard(color, Context.Guild.Id);

                if (null == c)
                {
                    await ReplyAsync("That card does not exist :(");
                    return;
                }
            }
            
            var roleTag = Context.Guild.Roles.First(r => r.Name == CardRole).Mention;

            var message = await ReplyAsync($"{roleTag}: {Context.User.Mention} is proposing to delete the {c.Name} card\n\n" +
                                           $"Place your votes below.  The votes will be counted in 1 hour.");
            await message.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });
            
            var polls = PollSingleton.Instance;
            polls.NewPoll(new Poll(
                Context.User,
                PollType.DELETE,
                Context,
                message.Id,
                null,
                c));
        }

        [Command("value")]
        public async Task ChangeCardValue(string color, int newVal)
        {
            if (!IsAdminUser(Context.Message.Author))
            {
                await ReplyAsync("You don't have permission to do that :(");
                return;
            }

            Cards c, newCard;
            using (var db = new CardContext())
            {
                c = GetCard(color, Context.Guild.Id);

                if (null == c)
                {
                    await ReplyAsync("That card does not exist :(");
                    return;
                }
                
                newCard = c;
                newCard.Value = newVal;
            }

            var roleTag = Context.Guild.Roles.First(r => r.Name == CardRole).Mention;

            var message = await ReplyAsync($"{roleTag}: {Context.User.Mention} is proposing to change the value of the {c.Name} card from {c.Value} to {newVal}\n\n" +
                                           $"Place your votes below.  The votes will be counted in 1 hour.");
            await message.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });
            
            var polls = PollSingleton.Instance;
            polls.NewPoll(new Poll(
                Context.User,
                PollType.VALUE,
                Context,
                message.Id,
                null,
                newCard));
        }
        #endregion
    }
}
