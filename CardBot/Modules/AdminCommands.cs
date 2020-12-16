using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardBot.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace CardBot.Modules
{
    public class AdminCommands : ModuleBase<SocketCommandContext>
    {
        private readonly string CardRoleAdmin = "CardAdmin";
        
        [Command("create")]
        public async void CreateCard(string name, int value, Emoji emoji)
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
        public async void DeleteCard(string color)
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
        public async void ChangeCardValue(string color, int newVal)
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
    }
}