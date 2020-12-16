using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Linq;

namespace CardBot.Models
{
    public class Challenge
    {
        private readonly DateTime _startTime;

        public bool Triggered { get
            {
#if DEBUG
                return _startTime.Add(new TimeSpan(0, 0, 10)) < DateTime.Now;
#else
                return _startTime.Add(new TimeSpan(1, 0, 0)) < DateTime.Now;
#endif
            } }

        public CardGivings Card { get; }

        public Cards NewCard { get; }

        public SocketUser Challenger { get; }

        public ulong MessageId { get; }

        public SocketCommandContext Context { get; }

        public bool Overturned => ToOverturn();

        public Challenge(CardGivings card, SocketUser challenger, Cards newCard, ulong messageId, SocketCommandContext context)
        {
            Card = card;
            Challenger = challenger;
            NewCard = newCard;
            _startTime = DateTime.Now;
            MessageId = messageId;
            Context = context;
        }

        private bool ToOverturn()
        {
            var message = Context.Channel.GetMessageAsync(MessageId).Result;

            var reacts = message.Reactions;

            var up = reacts.Keys.Where(e => e.Name == "👍").First();
            var down = reacts.Keys.Where(e => e.Name == "👎").First();

            return reacts[up].ReactionCount > reacts[down].ReactionCount;
        }
    }
}
