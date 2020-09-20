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
        private string CardRole = "CardBot";

        private readonly Emoji Frown = new Emoji("😦");
        private readonly Emoji Smile = new Emoji("🙂");

        private CardLeaderboard Leaderboard = new CardLeaderboard();

        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        [Command("challenge")]
        public async Task ChallengeCard(string user, string change)
        {
            CardChallengeChanges challenge;
            if (change.Equals("yellow")) challenge = CardChallengeChanges.YELLOW;
            else if (change.Equals("red")) challenge = CardChallengeChanges.RED;
            else if (change.Equals("delete")) challenge = CardChallengeChanges.REMOVE;
            else
            {
                await ReplyAsync("Valid options are yellow, red, or delete");
                return;
            }

            var user1 = GetUser(user);
            CardGivings card;

            using (DataContext db = new DataContext())
            {
                card = db.CardGivings.AsQueryable()
                    .Where(c => c.DegenerateId.Equals(db.Users.AsQueryable().Where(u => u.Name == user1.Username).Select(u => u.Id).First()))
                    .OrderBy(c => c.TimeStamp).Last();

                card.Card = db.Cards.AsQueryable().Where(c => c.Id == card.CardId).First();
                card.Degenerate = db.Users.AsQueryable().Where(u => u.Id == card.DegenerateId).First();
                card.Giver = db.Users.AsQueryable().Where(u => u.Id == card.GiverId).First();

                if ((challenge == CardChallengeChanges.YELLOW && card.Card.Name == "Yellow") || (challenge == CardChallengeChanges.RED && card.Card.Name == "Red"))
                {
                    await ReplyAsync("You cannot change the card to the same color");
                    return;
                }
            }

            string proposal = challenge == CardChallengeChanges.REMOVE ? "remove the card" : challenge == CardChallengeChanges.RED ? "convert the yellow card to red" : "convert the red card to yellow";
            var roleTag = Context.Guild.Roles.Where(r => r.Name == CardRole).FirstOrDefault().Mention;

            var message = await ReplyAsync($"{roleTag}: {Context.User.Mention} has challenged {user1.Mention}'s last {card.Card.Name} card.  {Context.User.Username} is proposing to {proposal}.  Place your votes below.  The votes will be counted in 1 hour.");
            await message.AddReactionsAsync(new[] { new Emoji("👍"), new Emoji("👎") });

            var challenges = ChallengeSingleton.Instance;
            challenges.NewChallenge(new Challenge(card, Context.User, challenge, message.Id, Context));
        }

        [Command("score")]
        public async Task ShowScoreboard()
        {
            await Context.Message.AddReactionAsync(Smile);
            var serverId = Context.Guild.Id;
            await ReplyAsync(Leaderboard.DisplayLeaderboard(serverId));
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
            IEmote[] emotes = new IEmote[2];
            emotes[0] = Frown;

            IEmote emoteYellow, emoteRed;

            if (Context.Guild.Id == 140642236978167808) // no u topia
            {
                // get emojis
                emoteYellow = Context.Guild.Emotes.Where(e => e.Name.Contains("yellowCard")).First();
                emoteRed = Context.Guild.Emotes.Where(e => e.Name.Contains("redCard")).First();
            }
            else
            {
                emoteYellow = new Emoji("🟨"); // :yellow_square:
                emoteRed = new Emoji("🟥"); // :red_square:
            }
            

            emotes[1] = color == "Red" ? emoteRed : emoteYellow;
            await Context.Message.AddReactionsAsync(emotes);

            var mention = GetUser(user);

            var sender = Context.User;
            var serverId = Context.Guild.Id;

            var cardCount = Leaderboard.FistMeDaddy(sender, mention, reason, color, serverId);

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
        public async Task GetHistory(string user, int count = 10)
        {
            await Context.Message.AddReactionAsync(Smile);
            var mention = GetUser(user);
            var serverId = Context.Guild.Id;

            var reply = Leaderboard.GetHistory(mention.Username, count, serverId);

            await ReplyAsync(reply);
        }
    }
}
