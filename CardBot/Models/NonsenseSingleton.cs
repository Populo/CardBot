using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Discord.WebSocket;

namespace CardBot.Models
{
    public sealed class NonsenseSingleton
    {
        Timer Timer;

        private readonly List<string> Sayings = new List<string> () {
            "SHE SHARTED ON MY SHIT",
            "YUH",
            "YOU PLAY WITH BALLS LIKE ITS FIFA",
        };

        private static readonly Lazy<NonsenseSingleton> single = new Lazy<NonsenseSingleton>(() => new NonsenseSingleton());

        public static NonsenseSingleton Instance { get { return single.Value; } }

        private NonsenseSingleton() {
            Timer = new Timer();
            Timer.Interval = 2 * 60 * 60 * 1000; // 2 hours
            Timer.AutoReset = true;
            Timer.Elapsed += Timer_Elapsed;
            Timer.Start();
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Console.WriteLine("Nonsense Exec");

            var r = new Random();

            if (r.Next(1000) < 5) {
                string message = Sayings[r.Next(Sayings.Count)];
                await DistributeNonsense(message);
            }
        }

        private async Task<int> DistributeNonsense(string message) {
            DiscordSocketClient bot = new DiscordSocketClient();
            ulong id = 726829445163253850;

            var channel = bot.GetChannel(id) as ISocketMessageChannel;
            var result = await channel.SendMessageAsync(text: message, isTTS: true);

            return 0;
        }
    }
}
