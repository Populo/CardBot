using System;
using System.Linq;
using CardBot.Modules;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using NLog;

namespace CardBot.Models
{
    public class Poll
    {
        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        
        private readonly DateTime _startTime;
        
        public bool Triggered { get
        {
#if DEBUG
            return _startTime.Add(new TimeSpan(0, 0, 10)) < DateTime.Now;
#else
            return _startTime.Add(new TimeSpan(1, 0, 0)) < DateTime.Now;
#endif
        } }
        
        public SocketUser Creator { get; }
        public PollType Type { get; }
        public SocketCommandContext Context { get; }
        public ulong MessageId { get; }
        public CardGivings CardGiving { get; private set; }
        public Cards Card { get; }

        public bool Majority => CountVotes();

        public Poll(SocketUser creator, PollType type, SocketCommandContext context, ulong messageId, CardGivings cardGiving, Cards card)
        {
            Creator = creator;
            Type = type;
            Context = context;
            MessageId = messageId;
            CardGiving = cardGiving;
            Card = card;
            _startTime = DateTime.Now;
        }

        private bool CountVotes()
        {
            var message = Context.Channel.GetMessageAsync(MessageId).Result;

            var reacts = message.Reactions;

            var up = reacts.Keys.Where(e => e.Name == "👍").First();
            var down = reacts.Keys.Where(e => e.Name == "👎").First();

            return reacts[up].ReactionCount > reacts[down].ReactionCount;
        }

        public bool Execute()
        {
            bool success = false;

            switch (Type)
            {
                case PollType.VALUE:
                    success = ModifyValue();
                    break;
                case PollType.DELETE:
                    success = DeleteCard();
                    break;
                case PollType.CHALLENGE:
                    success = ExecuteChallenge();
                    break;
                case PollType.CREATE:
                    success = CreateCard();
                    break;
            }

            return success;
        }

        private bool CreateCard()
        {
            try
            {
                using (var db = new CardContext())
                {
                    var message = Context.Channel.GetMessageAsync(MessageId).Result;

                    db.Cards.Add(Card);
                    
                    db.SaveChanges();
                    
                    message.Channel.SendMessageAsync($"{Card.Name} card has been created with a value of {Card.Value}.");
                }

                return true;
            }
            catch (Exception e)
            {
                var server = Context.Guild.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != server)
                {
                    IMessageChannel channel = (IMessageChannel) server;
                    channel.SendMessageAsync(e.Message);
                }

                Logger.Log(LogLevel.Error, e);
                return false;
            }
        }

        private bool ExecuteChallenge()
        {
            try
            {
                using (var db = new CardContext())
                {
                    var message = Context.Channel.GetMessageAsync(MessageId).Result;
                    Cards c = null;
                    if (null != Card)
                    {
                        c = db.Cards
                            .AsQueryable()
                            .Where(card => string.Equals(card.Name, Card.Name, StringComparison.CurrentCultureIgnoreCase))
                            .FirstOrDefault();

                        if (null == c)
                        {
                            message.Channel.SendMessageAsync($"Cannot find {Card.Name} to delete it.");
                            return false;
                        }
                    }

                    var giving = db.CardGivings.AsQueryable()
                        .Where(g => g.Id == CardGiving.Id)
                        .FirstOrDefault();
                    
                    if (null == giving)
                    {
                        message.Channel.SendMessageAsync($"Cannot find card {CardGiving.Id} to challenge.");
                        return false;
                    }

                    if (null == c) // deleting card
                    {
                        db.CardGivings.Remove(giving);
                    }
                    else // changing card
                    {
                        giving.Card = c;
                        giving.CardId = c.Id;
                    }

                    db.SaveChanges();
                    
                    message.Channel.SendMessageAsync($"{Creator.Username}'s challenge has been executed.");
                }

                return true;
            }
            catch (Exception e)
            {
                var server = Context.Guild.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != server)
                {
                    IMessageChannel channel = (IMessageChannel) server;
                    channel.SendMessageAsync(e.Message);
                }

                Logger.Log(LogLevel.Error, e);
                return false;
            }
        }

        private bool DeleteCard()
        {
            try
            {
                using (var db = new CardContext())
                {
                    var message = Context.Channel.GetMessageAsync(MessageId).Result;
                    
                    var c = db.Cards
                        .AsQueryable()
                        .Where(card => card.Id == Card.Id)
                        .FirstOrDefault();

                    if (null == c)
                    {
                        message.Channel.SendMessageAsync($"Cannot find {Card.Name} to delete it.");
                        return false;
                    }

                    db.Cards.Remove(c);
                    db.CardGivings.RemoveRange(db.CardGivings.AsQueryable()
                        .Where(g => g.CardId == c.Id).ToArray());

                    db.SaveChanges();
                    
                    message.Channel.SendMessageAsync($"{c.Name} card has been deleted.");
                }

                return true;
            }
            catch (Exception e)
            {
                var server = Context.Guild.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != server)
                {
                    IMessageChannel channel = (IMessageChannel) server;
                    channel.SendMessageAsync(e.Message);
                }

                Logger.Log(LogLevel.Error, e);
                return false;
            }
        }

        private bool ModifyValue()
        {
            try
            {
                var message = Context.Channel.GetMessageAsync(MessageId).Result;
                
                using (var db = new CardContext())
                {
                    var c = db.Cards
                        .AsQueryable()
                        .Where(card => card.Id == Card.Id)
                        .FirstOrDefault();

                    if (null == c)
                    {
                        message.Channel.SendMessageAsync($"Cannot find {Card.Name} to modify it.");
                        return false;
                    }

                    c.Value = Card.Value;

                    db.Update(c);

                    db.SaveChanges();
                    
                    message.Channel.SendMessageAsync($"{c.Name}'s new value is {c.Value}");
                }

                return true;
            }
            catch (Exception e)
            {
                var server = Context.Guild.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != server)
                {
                    IMessageChannel channel = (IMessageChannel) server;
                    channel.SendMessageAsync(e.Message);
                }

                Logger.Log(LogLevel.Error, e);
                return false;
            }
        }
    }
}