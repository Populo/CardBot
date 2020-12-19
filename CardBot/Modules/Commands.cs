using CardBot.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
                        .Where(c => c.Name.ToUpper() == newColor.ToUpper())
                        .Where(c => c.ServerId == Context.Guild.Id)
                        .FirstOrDefault();
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
                    .Where(c => c.DegenerateId.Equals(db.Users.AsQueryable()
                        .Where(u => u.Name == user1.Username)
                        .Select(u => u.Id).First()))
                    .OrderBy(c => c.TimeStamp).Last();

                if (card.TimeStamp.AddHours(MAX_CHALLENGE_TIME) < DateTime.Now)
                {
                    await ReplyAsync("This card is too old to challenge.");
                    return;
                }
                
                card.Card = db.Cards.AsQueryable().Where(c => c.Id == card.CardId).First();
                card.Degenerate = db.Users.AsQueryable().Where(u => u.Id == card.DegenerateId).First();
                card.Giver = db.Users.AsQueryable().Where(u => u.Id == card.GiverId).First();

                if (null != newCard && card.Card.Id == newCard.Id)
                {
                    await ReplyAsync("You cannot change the card to the same color");
                    return;
                }
            }

            string proposal = newColor == "none"
                ? "delete the card"
                : $"convert their {card.Card.Name} card to a {newCard.Name} card";
            var roleTag = Context.Guild.Roles.First(r => r.Name == CardRole).Mention;

            var message = await ReplyAsync($"{roleTag}: {Context.User.Mention} has challenged {user1.Mention}'s last {card.Card.Name} card.  {Context.User.Username} is proposing to {proposal}.\n" +
                                           $"{Context.User.Username}'s reasoning: ```{reasonForChallenge}```\n" +
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

        [Command("list")]
        public async Task GetCards()
        {
            List<Cards> cards;

            using (var db = new CardContext())
            {
                cards = db.Cards.AsQueryable()
                    .Where(c => c.ServerId == Context.Guild.Id)
                    .OrderByDescending(c => c.Value)
                    .ToList();
            }

            if (null == cards || cards.Count == 0)
            {
                await ReplyAsync("There are no cards for this server. Create one with !create.");
                return;
            }
    
            var message = new StringBuilder();
            
            string segment, line, header;
            string cardHeader = "Card";
            string valueHeader = "Value";

            int longestCard = cards.OrderByDescending(c => c.Name.Length).First().Name.Length;
            int longestValue = cards.OrderByDescending(c => c.Value.ToString().Length).First().Value.ToString().Length;

            if (longestCard < cardHeader.Length) longestCard = cardHeader.Length;
            if (longestValue < valueHeader.Length) longestValue = valueHeader.Length;

            segment = $"| {cardHeader.CenterString(longestCard)} |";
            line = $"|{new string('-', segment.Length-2)}+";
            header = segment;

            segment = $" {valueHeader.CenterString(longestValue)} |";
            line += $"{new string('-', segment.Length-1)}|";
            header += segment;

            message.AppendLine(header).AppendLine(line);
            foreach (var c in cards)
            {
                message.AppendLine(
                    $"| {c.Name.CenterString(longestCard)} | {c.Value.ToString().CenterString(longestValue)} |");
            }

            await ReplyAsync($"The following cards are available:\n```{message}```");
        }

        [Command("info")]
        public async Task ShowInfo()
        {
            var message = new StringBuilder();

            message.AppendLine("Welcome to CardBot 2.0");
            message.AppendLine(
                "The purpose of this bot is to keep track of heinous things you and/or your friends say.");
            message.AppendLine("See the github readme below for more information on setup");
            message.AppendLine("Made by Populo#0001");
            message.AppendLine("https://github.com/cstaudigel/CardBot");

            await Context.Message.AddReactionAsync(Smile);
            await ReplyAsync(message.ToString());
        }

        [Command("help")]
        public async Task GetHelp(params string[] command)
        {
            var message = new StringBuilder();

            await Context.Message.AddReactionAsync(Smile);

            if (command.Length == 0|| command[0].ToLower() == "help")
            {
                message.AppendLine("Available Commands:");
                message.AppendLine("**help**: !help {command (optional)}");
                message.AppendLine("**card**: !card {color} {@user} {reason}");
                message.AppendLine("**score**: !score");
                message.AppendLine("**history**: !history {@user} {cards to show (optional, default: 10)}");
                message.AppendLine("**info**: !info");
                message.AppendLine("**challenge**: !challenge {@user} {new card(remove/delete to delete card)} {reason}");
                message.AppendLine("Available Commands: (CardAdmin role only)");
                message.AppendLine("**create**: !create {color} {point value} {emoji}");
                message.AppendLine("**value**: !value {color} {new point value}");
                message.AppendLine("**delete**: !delete {color}");
            }
            else
            {
                string c = command[0].ToLower();
                switch (c)
                {
                    case "card":
                        message.AppendLine(
                            "This command will give the tagged user a card of the indicated color for the provided reason and adds the value of the card to this user's score.");
                        break;
                    case "score":
                        message.AppendLine(
                            "This command will display a score board of all users in the channel with cards sorted highest score to lowest.");
                        break;
                    case "history":
                        message.AppendLine(
                            "This command will show the most recent 10 cards that the provided user has been given.  Provide a number after the user to change the 10.");
                        break;
                    case "info":
                        message.AppendLine(
                            "This command will show basic info about the bot including the author and the github repository.");
                        break;
                    case "challenge":
                        message.AppendLine(
                            "This command will allow you to challenge a card that anybody has received. Issuing a challenge will begin a poll that allows everybody with access to the text channel to vote in. Note that it only allows changing of the most recent card and must be done within 3 hours of the person receiving the card.");
                        break;
                    case "create":
                        message.AppendLine(
                            "This command is used by admins to create cards.  This will begin a poll that anybody with access to the text channel can vote in before creating the card.");
                        break;
                    case "value":
                        message.AppendLine(
                            "This command allows admins to change the value of existing cards.  This will begin a poll that anybody with access to the text channel can vote in before modifying the card.");
                        break;
                    case "delete":
                        message.AppendLine(
                            "This command allows admins to delete cards. THIS CANNOT BE UNDONE AND WILL RESULT IN ALL CARDS GIVEN OF THIS TYPE TO BE ERASED AS WELL. This will begin a poll that anybody with access to the text channel can vote in before deleting the card.");
                        break;
                    default:
                        message.AppendLine("This command does not exist.");
                        break;
                }
            }
            await ReplyAsync(message.ToString());
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
                    .Where(card => card.Name.ToUpper() == name.ToUpper())
                    .Where(card => card.ServerId == serverId)
                    .FirstOrDefault();

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

            Cards c;
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

            var message = await ReplyAsync($"{roleTag}: {Context.User.Mention} is proposing to change the value of the {c.Name} card from {c.Value} to {newVal}\n\n" +
                                           $"Place your votes below.  The votes will be counted in 1 hour.");
            await message.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });

            // i dont feel like dealing with memory and shallow/deep copy so this will go here
            c.Value = newVal;
            
            var polls = PollSingleton.Instance;
            polls.NewPoll(new Poll(
                Context.User,
                PollType.VALUE,
                Context,
                message.Id,
                null,
                c));
        }
        #endregion
    }
}
