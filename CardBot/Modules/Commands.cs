using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CardBot.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        private readonly Emoji Frown = new Emoji("😦");
        private readonly Emoji Smile = new Emoji("🙂");

        private CardLeaderboard Leaderboard = new CardLeaderboard();

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
                reason = new[] { "being a trashcan" };
            }
            string reasonString = string.Join(' ', reason);

            await Context.Message.AddReactionAsync(Frown);
            var mention = Context.Message.MentionedUsers.FirstOrDefault(x => x.Mention == user);
            var sender = Context.User;

            var cardCount = Leaderboard.FistMeDaddy(sender, mention, reasonString, "Yellow");

            if (cardCount != 0)
            {
                await ReplyAsync($"{mention} now has {cardCount} yellow cards.");
            }
            else
            {
                await ReplyAsync("That didnt work. frown :(");
            }
        }

        [Command("red")]
        public async Task AddRed(string user, params string[] reason)
        {
            if (reason.Length < 1)
            {
                reason = new[] { "being a degenerate" };
            }
            string reasonString = string.Join(' ', reason);

            await Context.Message.AddReactionAsync(Frown);
            var mention = Context.Message.MentionedUsers.FirstOrDefault(x => x.Mention == user);
            var sender = Context.User;

            var cardCount = Leaderboard.FistMeDaddy(sender, mention, reasonString, "Red");

            if (cardCount != 0)
            {
                await ReplyAsync($"{mention} now has {cardCount} red cards.");
            }
            else
            {
                await ReplyAsync("That didnt work. frown :(");
            }
        }

        [Command("history")]
        public async Task GetHistory(string user)
        {
            await Context.Message.AddReactionAsync(Smile);
            var mention = Context.Message.MentionedUsers.FirstOrDefault(x => x.Mention == user);

            var reply = Leaderboard.GetHistory(mention.Username);

            await ReplyAsync(reply);
        }
    }
}
