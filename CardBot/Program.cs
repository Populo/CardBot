using CardBot.Bot.Models;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Reflection;
using System.Threading.Tasks;
using CardBot.Bot.Singletons;
using CardBot.Bot.Modules;
using System.Linq;

namespace CardBot.Bot
{
    class Program
    {
        private readonly string _prefix = "!";

        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

        public static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                throw new Exception("Include bot access key");
            }

            ConfigureLogger();
            System.AppDomain.CurrentDomain.UnhandledException += GlobalHandler;
            PollSingleton s = PollSingleton.Instance;

            new Program().RunBotAsync(args[0]).GetAwaiter().GetResult();
        }

        private static void GlobalHandler(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error(e.ExceptionObject.ToString());
        }

        private static void ConfigureLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "Log.log" };
            var errorFile = new NLog.Targets.FileTarget("errorfile") { FileName = "Errors.log" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Info, LogLevel.Fatal, logfile);
            config.AddRule(LogLevel.Warn, LogLevel.Fatal, errorFile);

            NLog.LogManager.Configuration = config;
        }

        public async Task RunBotAsync(string key)
        {
            _client = new DiscordSocketClient();
            _commands = new CommandService();
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .BuildServiceProvider();

            _client.Log += BotLog;
            _client.Ready += _client_Ready;
            _client.SlashCommandExecuted += _client_SlashCommandExecuted;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, key);

            await _client.StartAsync();

            //await _client.SetGameAsync("for degenerates", null, ActivityType.Watching);
            await _client.SetGameAsync("TESTING", null, ActivityType.Competing);

            await Task.Delay(-1);
        }

        private async Task _client_SlashCommandExecuted(SocketSlashCommand arg)
        {
            if (arg.Data.Name == "card")
            {
                await HandleCardCommand(arg);
            } 
            else
            {
                arg.RespondAsync("uwu");
            }

            return;
        }

        private async Task HandleCardCommand(SocketSlashCommand arg) => SlashCommandWorker.GiveCard(arg, _client.GetGuild(arg.GuildId.Value));

        private async Task _client_Ready()
        {
            // globals
            await _client.CreateGlobalApplicationCommandAsync(SlashCommands.InitCardCreateCommand().Build());

            // server specific
            SlashCommands.InitCardSlashCommand(_client);
            SlashCommands.InitChallengeSlashCommand(_client);

        }

        private async Task RegisterCommandsAsync()
        {
            _client.MessageReceived += HandleCommandsAsync;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task HandleCommandsAsync(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            var channel = context.Channel;
            int argPos = 0;

            if (message.HasStringPrefix(_prefix, ref argPos))
            {
                Logger.Info($"Command issued by {arg.Author} in #{arg.Channel}: {arg.Content}");
                if (channel.Name == "card-tracker")
                {
                    var result = await _commands.ExecuteAsync(context, argPos, _services);
                    if (!result.IsSuccess) Console.WriteLine(result.ErrorReason);
                }
            }
        }

        private Task BotLog(LogMessage arg)
        {
            Logger.Info(arg);
            return Task.CompletedTask;
        }
    }
}
