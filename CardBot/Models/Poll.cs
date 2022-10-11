using System;
using System.Linq;
using CardBot.Bot.Modules;
using CardBot.Data;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using NLog;

namespace CardBot.Bot.Models
{
    public class Poll
    {
        public static int HOURS_OF_GIVE_POLL = 12;

        private static NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly DateTime _startTime;

        private int PollHours
        {
            get
            {
                switch (Type)
                {
                    case PollType.GIVE:
                        return HOURS_OF_GIVE_POLL;
                    default:
                        return 1;
                }
            }
        }

        public bool Triggered { get
            {
#if DEBUG
                return _startTime.Add(new TimeSpan(0, 0, 10)) < DateTime.Now;
#else
            return _startTime.Add(new TimeSpan(PollHours, 0, 0)) < DateTime.Now;
#endif
            } }

        public SocketUser Receiver { get; }
        public PollType Type { get; }
        public SocketSlashCommand Command { get; }
        public CardGivings CardGiving { get; private set; }
        public Cards Card { get; }
        public SocketGuild Server { get; set; }

        public int Yes { get; set; }
        public int No { get; set; }

        public Guid GivingId => CardGiving.Id;

        public bool Majority => CountVotes();

        public Poll(SocketUser receiver, PollType type, SocketSlashCommand command, CardGivings cardGiving, Cards card, SocketGuild server)
        {
            Receiver = receiver;
            Type = type;
            Command = command;
            CardGiving = cardGiving;
            Card = card;
            _startTime = DateTime.Now;
            Server = server;
        }

        private bool CountVotes()
        {
            return Yes > No;
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
                case PollType.GIVE:
                    success = GivePollCard();
                    break;
            }

            return success;
        }

        private bool GivePollCard()
        {
            var helper = new CardLeaderboard();
            try
            {
                int totalCards = helper.GiveCard(Command.User, 
                    Receiver, 
                    CardGiving.CardReason,
                    CardGiving.Card,
                    CardGiving.ServerId,
                    Command);

                Command.RespondAsync($"{Receiver.Mention} now has {totalCards} {CardGiving.Card.Name} cards.");

                return true;
            }
            catch (Exception e)
            {
                var s = Server.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != s)
                {
                    IMessageChannel channel = (IMessageChannel) s;
                    channel.SendMessageAsync(e.Message);
                }

                Logger.Log(LogLevel.Error, e);
                return false;
            }
        }

        private bool CreateCard()
        {
            try
            {
                using (var db = new CardContext())
                {
                    db.Cards.Add(Card);
                    
                    db.SaveChanges();
                    
                    Command.RespondAsync($"{Card.Name} card has been created with a value of {Card.Value}.");
                }

                return true;
            }
            catch (Exception e)
            {
                var s = Server.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != s)
                {
                    IMessageChannel channel = (IMessageChannel)s;
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
                    Cards c = null;
                    if (null != Card)
                    {
                        c = db.Cards
                            .AsQueryable()
                            .Where(card => card.Id == Card.Id)
                            .FirstOrDefault();

                        if (null == c)
                        {
                            Command.RespondAsync($"Cannot find {Card.Name} to delete it.");
                            return false;
                        }
                    }

                    var giving = db.CardGivings.AsQueryable()
                        .Where(g => g.Id == CardGiving.Id)
                        .FirstOrDefault();
                    
                    if (null == giving)
                    {
                        Command.RespondAsync($"Cannot find card {CardGiving.Id} to challenge.");
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
                    
                    Command.RespondAsync($"{Command.User.Username}'s challenge has been executed.");
                }

                return true;
            }
            catch (Exception e)
            {
                var s = Server.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != s)
                {
                    IMessageChannel channel = (IMessageChannel)s;
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
                    var c = db.Cards
                        .AsQueryable()
                        .Where(card => card.Id == Card.Id)
                        .FirstOrDefault();

                    if (null == c)
                    {
                        Command.RespondAsync($"Cannot find {Card.Name} to delete it.");
                        return false;
                    }

                    db.Cards.Remove(c);
                    db.CardGivings.RemoveRange(db.CardGivings.AsQueryable()
                        .Where(g => g.CardId == c.Id).ToArray());

                    db.SaveChanges();
                    
                    Command.RespondAsync($"{c.Name} card has been deleted.");
                }

                return true;
            }
            catch (Exception e)
            {
                var s = Server.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != s)
                {
                    IMessageChannel channel = (IMessageChannel)s;
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
                using (var db = new CardContext())
                {
                    var c = db.Cards
                        .AsQueryable()
                        .Where(card => card.Id == Card.Id)
                        .FirstOrDefault();

                    if (null == c)
                    {
                        Command.RespondAsync($"Cannot find {Card.Name} to modify it.");
                        return false;
                    }

                    c.Value = Card.Value;

                    db.Update(c);

                    db.SaveChanges();
                    
                    Command.RespondAsync($"{c.Name}'s new value is {c.Value}");
                }

                return true;
            }
            catch (Exception e)
            {
                var s = Server.Channels.Where(c => c.Name == Commands.CardErrorChannel).FirstOrDefault();
                if (null != s)
                {
                    IMessageChannel channel = (IMessageChannel)s;
                    channel.SendMessageAsync(e.Message);
                }

                Logger.Log(LogLevel.Error, e);
                return false;
            }
        }
    }
}