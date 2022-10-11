using CardBot.Bot.Models;
using CardBot.Bot.Singletons;
using CardBot.Data;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CardBot.Bot.Modules
{
    public class SlashCommandWorker
    {

        public static string CardRole = "CardUser",
            CardRoleAdmin = "CardAdmin";

        public static string CardChannel = "card-tracker",
            CardErrorChannel = "card-tracker-errors";

        private const int MAX_CHALLENGE_TIME = 3;

        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        #region Utility
        private static bool IsAdminUser(SocketSlashCommand command, SocketGuild server)
        {
            return server.Roles.FirstOrDefault(r => r.Name == CardRoleAdmin).Members.Select(u => u.Id).Contains(command.User.Id);
        }

        public static Users GetDBUser(SocketUser mention, CardContext db)
        {
            Users u;

            u = db.Users.AsQueryable()
                .Where(u => u.Name == mention.Username)
                .FirstOrDefault();

            if (null == u)
            {
                u = CreateUser(mention, db);
            }

            return u;
        }

        private static Users CreateUser(SocketUser sender, CardContext db)
        {
            try
            {
                var u = db.Users.Add(new Users
                {
                    Id = Guid.NewGuid(),
                    Name = sender.Username
                });

                db.SaveChanges();

                return u.Entity;
            }
            catch (Exception e)
            {
                Logger.Error(e);
                return null;
            }
        }

        private Cards GetCard(Guid cardId, ulong serverId)
        {
            Cards card = null;

            using (var db = new CardContext())
            {
                card = db.Cards.AsQueryable()
                    .Where(c => c.ServerId == serverId)
                    .Where(c => c.Id == cardId)
                    .FirstOrDefault();
            }

            return card;
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

        #endregion


        public static async void GiveCard(SocketSlashCommand command, SocketGuild server)
        {
            using (var db = new CardContext())
            {
                var giver = command.User;
                var card = command.Data.Options.FirstOrDefault(o => o.Name == "card").Value.ToString();
                var person = command.Data.Options.FirstOrDefault(o => o.Name == "person").Value as SocketUser;
                var reason = command.Data.Options.FirstOrDefault(o => o.Name == "reason").Value.ToString();

                var dbCard = db.Cards.FirstOrDefault(c => c.Name == card && c.ServerId == command.GuildId);
                var dbGiver = db.Users.FirstOrDefault(u => u.Name == person.Username);
                var dbReceiver = db.Users.FirstOrDefault(u => u.Name == giver.Username);
                
                if (null == dbCard)
                {
                    await command.RespondAsync($"There is no {card} in this server, pick one that exists or use /list to see all available cards.");
                    return;
                }

                if (null == dbGiver)
                {
                    CreateUser(giver, db);
                }
                if (null == dbReceiver)
                {
                    CreateUser(person, db);
                }

                var giving = new CardGivings()
                {
                    Card = dbCard,
                    CardId = dbCard.Id,
                    Degenerate = dbReceiver,
                    DegenerateId = dbReceiver.Id,
                    Id = new Guid(),
                    CardReason = reason,
                    Giver = dbGiver,
                    GiverId = dbGiver.Id,
                    MessageId = command.Id,
                    ServerId = server.Id,
                    TimeStamp = DateTime.Now
                };

                if (dbCard.Poll)
                {

                    var dbCard_fail = db.Cards.FirstOrDefault(c => c.ServerId == command.GuildId && c.Id == dbCard.FailedId);
                    var roleTag = server.Roles.FirstOrDefault(r => r.Name == CardRole).Mention;
                    var builder = new ComponentBuilder()
                        .WithButton("Yay", customId: $"give-{giving.Id}-yes", style: ButtonStyle.Success)
                        .WithButton("Nay", customId: $"give-{giving.Id}-no", style: ButtonStyle.Danger);

                    await command.RespondAsync($"{roleTag}: {command.User.Mention} is proposing to give {person.Mention} a {dbCard.Name} card worth {dbCard.Value} points.\n\n" +
                                           $"Place your votes below. The votes will be counted in {Poll.HOURS_OF_GIVE_POLL} hours.", components: builder.Build());

                    var polls = PollSingleton.Instance;
                    polls.NewPoll(new Poll(
                        person,
                        PollType.GIVE,
                        command,
                        giving,
                        dbCard_fail,
                        server));
                }
                else
                {
                    db.CardGivings.Add(giving);
                    db.SaveChanges();

                    await command.RespondAsync($"{person.Mention} has been given a {card} for {reason}.");
                }
            }
        }

        public static async void ChallengeCard(SocketSlashCommand command, SocketGuild server)
        {
            var user = command.Data.Options.FirstOrDefault(c => c.Name == "user").Value as SocketUser;
            var newCardString = command.Data.Options.FirstOrDefault(c => c.Name == "card").ToString();
            var reason = command.Data.Options.FirstOrDefault(c => c.Name == "reason").ToString();

            using (var db = new CardContext())
            {
                var userDb = db.Users.FirstOrDefault(u => u.Name == user.Mention);

                if (userDb == null)
                {
                    await command.RespondAsync($"{user.Mention} does not exist in database.");
                    CreateUser(user, db);
                    return;
                }

                var lastCard = db.CardGivings.Where(c => c.ServerId == server.Id
                                                && c.DegenerateId == userDb.Id)
                                             .OrderByDescending(o => o.TimeStamp)
                                             .FirstOrDefault();

                if (lastCard == null)
                {
                    await command.RespondAsync($"{user.Mention} does not have any cards to challenge.");
                    return;
                }

                if (DateTime.Parse(lastCard.TimeStamp.ToString()).AddHours(MAX_CHALLENGE_TIME) < DateTime.Now)
                {
                    await command.RespondAsync($"Card is too old to challenge, can only challenge cards that are {MAX_CHALLENGE_TIME} hours old.");
                    return;
                }

                Cards newCard = new Cards();

                if (newCardString == "delete" || newCardString == "remove")
                {
                    newCardString = "none";
                }
                else
                {
                    newCard = db.Cards.FirstOrDefault(c => c.Name == newCardString
                                                        && c.ServerId == server.Id);

                    if (newCard == null)
                    {
                        await command.RespondAsync($"{newCardString} card does not exist.");
                        return;
                    }
                    else if (newCard.Id == lastCard.CardId)
                    {
                        await command.RespondAsync($"you cannot change the card to the same color.");
                        return;
                    }
                }

                lastCard.Card = db.Cards.AsQueryable().Where(c => c.Id == lastCard.CardId).First();
                lastCard.Degenerate = db.Users.AsQueryable().Where(u => u.Id == lastCard.DegenerateId).First();
                lastCard.Giver = db.Users.AsQueryable().Where(u => u.Id == lastCard.GiverId).First();

                string proposal = newCardString == "none"
                ? "delete the card"
                : $"convert their {lastCard.Card.Name} card to a {newCard.Name} card";
                var roleTag = server.Roles.FirstOrDefault(r => r.Name == CardRole).Mention;

                var builder = new ComponentBuilder()
                        .WithButton("Yay", customId: $"challenge-{lastCard.Id}-yes", style: ButtonStyle.Success)
                        .WithButton("Nay", customId: $"challenge-{lastCard.Id}-no", style: ButtonStyle.Danger);

                await command.RespondAsync($"{roleTag}: {command.User.Mention} has challenged {user.Mention}'s last {lastCard.Card.Name} card.  {command.User.Username} is proposing to {proposal}.\n" +
                                               $"{command.User.Username}'s reasoning: ```{reason}```\n" +
                                               $"Place your votes below.  The votes will be counted in 1 hour.", components: builder.Build());

                var polls = PollSingleton.Instance;
                polls.NewPoll(new Poll(
                        user,
                        PollType.CHALLENGE,
                        command,
                        lastCard,
                        newCard,
                        server
                    ));
            }
        }

        public static async void ListCards(SocketSlashCommand command)
        {
            List<Cards> cards;

            using (var db = new CardContext())
            {
                cards = db.Cards.AsQueryable()
                    .Where(c => c.ServerId == command.GuildId.Value)
                    .OrderByDescending(c => c.Value)
                    .ToList();
            }

            if (null == cards || cards.Count == 0)
            {
                await command.RespondAsync("There are no cards for this server. Create one with !create.");
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
            line = $"|{new string('-', segment.Length - 2)}+";
            header = segment;

            segment = $" {valueHeader.CenterString(longestValue)} |";
            line += $"{new string('-', segment.Length - 1)}|";
            header += segment;

            message.AppendLine(header).AppendLine(line);
            foreach (var c in cards)
            {
                message.AppendLine(
                    $"| {c.Name.CenterString(longestCard)} | {c.Value.ToString().CenterString(longestValue)} |");
            }

            await command.RespondAsync($"The following cards are available:\n```{message}```");
        }

        public static async void GetScoreBoard(SocketSlashCommand command)
        {
            await command.RespondAsync(CardLeaderboard.BuildLeaderboard(command.GuildId.Value));
        }

        public static async void GetInfo(SocketSlashCommand command)
        {
            var message = new StringBuilder();

            message.AppendLine("Welcome to CardBot 2.0");
            message.AppendLine(
                "The purpose of this bot is to keep track of heinous things you and/or your friends say.");
            message.AppendLine("See the github readme below for more information on setup");
            message.AppendLine("Made by Populo#0001");
            message.AppendLine("https://github.com/populo/CardBot");

            await command.RespondAsync(message.ToString());
        }

        public static async void GetHelp(SocketSlashCommand command)
        {
            var message = new StringBuilder();

            var method = command.Data.Options.FirstOrDefault().Value.ToString().ToLower();


            if (null == method || method == "help")
            {
                message.AppendLine("Available Commands:");
                message.AppendLine("**help**: !help {command (optional)}");
                message.AppendLine("**card**: !card {color} {@user} {reason}");
                message.AppendLine("**score**: !score");
                message.AppendLine("**history**: !history {@user} {cards to show (optional, default: 10)}");
                message.AppendLine("**info**: !info");
                message.AppendLine("**challenge**: !challenge {@user} {new card(remove/delete to delete card)} {reason}");
                message.AppendLine("Available Commands: (CardAdmin role only)");
                message.AppendLine("**create**: !create {color} {point value} {emoji} {requiresPoll(true/false)} {cardIfPollFails}");
                message.AppendLine("**value**: !value {color} {new point value}");
                message.AppendLine("**delete**: !delete {color}");
            }
            else
            {
                switch (method)
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
            await command.RespondAsync(message.ToString());
        }

        public static async void GetHistory(SocketSlashCommand command)
        {
            var serverId = command.GuildId.Value;
            var user = command.Data.Options.FirstOrDefault(u => u.Name == "user").Value as SocketUser;
            var countString = command.Data.Options.FirstOrDefault(u => u.Name == "count").Value.ToString();
            int count = string.IsNullOrEmpty(countString) ? 10 : int.Parse(countString);

            if (null == user)
            {
                await command.RespondAsync($"Cannot find user. This is a discord error not a database error.");
                return;
            }

            var reply = CardLeaderboard.GetHistory(user.Username, count, serverId);

            await command.RespondAsync(reply);
        }

        // admin commands
        public static async void CreateCard(SocketSlashCommand command, SocketGuild server)
        {
            if (!IsAdminUser(command, server))
            {
                await command.RespondAsync("You don't have permission to do that :(");
                return;
            }

            string name = command.Data.Options.FirstOrDefault(o => o.Name == "name").Value.ToString();
            int value = (int)command.Data.Options.FirstOrDefault(o => o.Name == "value").Value;
            bool isPoll = (bool)command.Data.Options.FirstOrDefault(o => o.Name == "poll").Value;
            string failedCard = command.Data.Options.FirstOrDefault(o => o.Name == "failedCard").Value.ToString();

            Cards c;
            ulong serverId = command.GuildId.Value;

            using (var db = new CardContext())
            {
                c = db.Cards.AsQueryable()
                    .Where(card => card.Name.ToUpper() == name.ToUpper())
                    .Where(card => card.ServerId == serverId)
                    .FirstOrDefault();

                if (c != null)
                {
                    await command.RespondAsync($"{name} card already exists");
                    return;
                }

                Cards failedPollCard = null;

                if (isPoll)
                {
                    failedPollCard = db.Cards.AsQueryable()
                        .Where(card => card.Name.ToUpper() == failedCard.ToUpper())
                        .Where(card => card.ServerId == serverId)
                        .FirstOrDefault();

                    if (failedPollCard == null)
                    {
                        await command.RespondAsync($"Cannot find a {failedCard} card.  Has it been created?");
                        return;
                    }
                } 
                else
                {
                    failedPollCard = new Cards()
                    {
                        Id = Guid.Empty
                    };
                }

                c = new Cards()
                {
                    Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name),
                    ServerId = serverId,
                    FailedId = failedPollCard.Id,
                    Poll = isPoll,
                    Value = value,
                    Id = Guid.NewGuid()
                };

                var roleTag = server.Roles.FirstOrDefault(r => r.Name == CardRole).Mention;
                var failCardString = failedPollCard.Id == Guid.Empty ? "nothing." : $"a {failedPollCard.Name} card.";
                var pollString = isPoll
                    ? $"This card will require a poll to be given, if the poll fails the person will be given a {failCardString} card"
                    : "";

                var builder = new ComponentBuilder()
                        .WithButton("Yay", customId: $"create-{c.Id}-yes", style: ButtonStyle.Success)
                        .WithButton("Nay", customId: $"create-{c.Id}-no", style: ButtonStyle.Danger);

                await command.RespondAsync($"{roleTag}: {command.User.Mention} is proposing to create a {c.Name} card worth {c.Value} points. {pollString}\n\n" +
                                           $"Place your votes below. The votes will be counted in 1 hour.", components: builder.Build());
            }
        }

        public static async void DeleteCard(SocketSlashCommand command, SocketGuild server)
        {
            if (!IsAdminUser(command, server))
            {
                await command.RespondAsync("You don't have permission to do that :(");
                return;
            }

            Cards c = null;

            using (var db = new CardContext())
            {
                c = GetCard(color, server.Id);

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
                null,
                PollType.DELETE,
                Context,
                message.Id,
                null,
                c));
        }
    }
}
