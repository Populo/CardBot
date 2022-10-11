using CardBot.Data;
using Discord;
using Discord.WebSocket;
using System.Linq;

namespace CardBot.Bot.Modules
{
    static class SlashCommands
    {
        public static void InitChallengeSlashCommand(DiscordSocketClient client)
        {
            var command = new SlashCommandBuilder();

            command.WithName("challenge");
            command.WithDescription("Challenge someone's most recent card");
        }

        public static void InitCardSlashCommand(DiscordSocketClient client)
        {
            using (var db = new CardContext())
            {
                foreach (var s in client.Guilds)
                {
                    var command = new SlashCommandBuilder();

                    command.WithName("card");
                    command.WithDescription("Give someone a card");

                    var cards = db.Cards.Where(c => c.ServerId == s.Id);
                    var cardOption = new SlashCommandOptionBuilder()
                                        .WithName("card")
                                        .WithDescription("Card to give person")
                                        .WithRequired(true)
                                        .WithType(ApplicationCommandOptionType.String);
                    foreach(var c in cards)
                    {
                        cardOption.AddChoice(c.Name, c.Name);
                    }

                    command.AddOption(cardOption);
                    command.AddOption("person", ApplicationCommandOptionType.User, "person who is getting the card", isRequired: true);
                    command.AddOption("reason", ApplicationCommandOptionType.String, "reason they are getting a card", isRequired: true);

                    s.CreateApplicationCommandAsync(command.Build());
                }
            }
                
        }

        public static SlashCommandBuilder InitCardCreateCommand()
        {
            var command = new SlashCommandBuilder();

            command.WithName("list");
            command.WithDescription("List all available cards in this server");

            return command;
        }
    }
}
