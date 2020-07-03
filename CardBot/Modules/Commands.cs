using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardBot.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        private string CardRole = "CardBot";

        private readonly Emoji Frown = new Emoji("😦");
        private readonly Emoji Smile = new Emoji("🙂");

        private CardLeaderboard Leaderboard = new CardLeaderboard();

        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        [Command("score")]
        public async Task ShowScoreboard()
        {
            await Context.Message.AddReactionAsync(Smile);
            await ReplyAsync(Leaderboard.DisplayLeaderboard());
        }

        [Command("yellow")]
        public async Task AddYellow(string user, params string[] reason)
        {
            if (reason.Length < 1)
            {
                reason = new[] { "being a degenerate" };
            }
            string reasonString = string.Join(' ', reason);

            await AddCard(user, "Yellow", reasonString);
        }

        [Command("red")]
        public async Task AddRed(string user, params string[] reason)
        {
            if (reason.Length < 1)
            {
                reason = new[] { "being a degenerate" };
            }
            string reasonString = string.Join(' ', reason);

            await AddCard(user, "Red", reasonString);
        }

        private SocketUser GetUser(string user)
        {
            var mention = Context.Message.MentionedUsers.FirstOrDefault(x => x.Mention == user);
            if (mention == null)
            {
                var role = Context.Guild.Roles.Where(r => r.Name == CardRole).FirstOrDefault();
                mention = Context.Guild.Users
                    .Where(x => x.Username.Contains(user, StringComparison.CurrentCultureIgnoreCase))
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

        private async Task AddCard(string user, string color, string reason) {
            await Context.Message.AddReactionAsync(Frown);

            var mention = GetUser(user);

            var sender = Context.User;

            var cardCount = Leaderboard.FistMeDaddy(sender, mention, reason, color);

            if (cardCount != 0)
            {
                await ReplyAsync($"{mention} now has {cardCount} {color} cards.");
            }
            else
            {
                Logger.Error("Sent Message: {message}", Context.Message);
                await ReplyAsync("That didnt work. frown :(");
            }
        }

        [Command("history")]
        public async Task GetHistory(string user)
        {
            await Context.Message.AddReactionAsync(Smile);
            var mention = GetUser(user);

            var reply = Leaderboard.GetHistory(mention.Username);

            await ReplyAsync(reply);
        }
    }
}
