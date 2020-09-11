using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace CardBot
{
    class Program
    {
        private const string prefix = "!";

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

            new Program().RunBotAsync(args[0]).GetAwaiter().GetResult();
        }

        private static void ConfigureLogger()
        {
            var config = new NLog.Config.LoggingConfiguration();

            var logfile = new NLog.Targets.FileTarget("logfile") { FileName = "Log.log" };
            var logconsole = new NLog.Targets.ConsoleTarget("logconsole");

            config.AddRule(LogLevel.Info, LogLevel.Fatal, logconsole);
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, logfile);

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

            _client.Log += Bot_Log;

            await RegisterCommandsAsync();

            await _client.LoginAsync(TokenType.Bot, key);

            await _client.StartAsync();

            await Task.Delay(-1);
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

            if (message.HasStringPrefix(prefix, ref argPos))
            {
                Logger.Info($"Command issued by {arg.Author} in #{arg.Channel}: {arg.Content}");
                if (channel.Name == "card-tracker")
                {
                    var result = await _commands.ExecuteAsync(context, argPos, _services);
                    if (!result.IsSuccess) Console.WriteLine(result.ErrorReason);
                }
            }
        }

        private Task Bot_Log(LogMessage arg)
        {
            Logger.Info(arg);
            return Task.CompletedTask;
        }
    }
}
