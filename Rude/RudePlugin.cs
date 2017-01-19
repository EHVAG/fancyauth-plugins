using System;
using System.Linq;
using System.Threading.Tasks;
using Fancyauth.APIUtil;
using Fancyauth.API;
using System.Collections.Concurrent;

namespace Rude
{
    public class RudePlugin : PluginBase
    {
        private readonly string[] Prefixes = { "[Rude] ", "[Ruderer] ", "[Rüdiger] " };
        public const double MinRudeTime = 45;
        public const double MaxRudeTime = 120;
        public const double MinRudeActionTimeout = 3;
        public const double MaxRudeActionTimeout = 18;

        private ConcurrentDictionary<int, RudeStatus> RudeStatus = new ConcurrentDictionary<int, RudeStatus>();
        private ConcurrentDictionary<int, RudeStatus> RudeActorStatus = new ConcurrentDictionary<int, RudeStatus>();

        private Random rand = new Random();

        [ContextCallback("Rude")]
        public async Task RudeUser(IUser actor, IUserShim targetShim)
        {
            var target = await targetShim.Load();
            EnsureRudeStatus(target.UserId);
            EnsureRudeStatus(actor.UserId);

            var rude = RudeStatus[target.UserId];
            var rudeActor = RudeActorStatus[actor.UserId];

            if (rudeActor.RudeLevel > 0)
            {
                var actorRude = RudeStatus[actor.UserId];

                // You shouldn't be able to use this to reset yourself
                if (rudeActor.RudeLevel < Prefixes.Length)
                    actorRude.RaiseRudeLevel(RandomTimespan(MinRudeTime, MaxRudeTime));

                return;
            }

            rudeActor.RaiseRudeLevel(RandomTimespan(MinRudeActionTimeout, MaxRudeActionTimeout));
            rude.RaiseRudeLevel(RandomTimespan(MinRudeTime, MaxRudeTime));

            if (rude.RudeLevel > Prefixes.Length)
            {
                rude.Reset();
                await target.Kick("You have been too rude.");
            }
            else
            {
                target.Name = GetUserName(target);
                await target.SaveChanges();
            }
        }

        public override async Task OnUserConnected(IUser user)
        {
            user.Name = GetUserName(user);
            await user.SaveChanges();
        }

        private void EnsureRudeStatus(int userId)
        {
            if (!RudeStatus.ContainsKey(userId))
            {
                RudeStatus.TryAdd(userId, new RudeStatus
                {
                    OnRudeLevelDecrease = async () =>
                    {
                        var users = await Server.GetOnlineUsers();
                        var user = users.SingleOrDefault(a => a.UserId == userId);
                        if (user == null)
                            return;

                        user.Name = GetUserName(user);
                        await user.SaveChanges();
                    }
                });
            }

            if (!RudeActorStatus.ContainsKey(userId))
            {
                RudeActorStatus.TryAdd(userId, new RudeStatus
                {
                    OnRudeLevelDecrease = () => { }
                });
            }
        }

        private string GetUserName(IUser user)
        {
            EnsureRudeStatus(user.UserId);

            string name = user.Name;

            // Remove potential old prefix
            var startsWith = Prefixes.SingleOrDefault(a => name.StartsWith(a));
            if (startsWith != null)
                name = name.Substring(startsWith.Length);

            var level = RudeStatus[user.UserId].RudeLevel;

            if (level > 0)
                return Prefixes[level - 1] + name;


            return name;
        }

        private TimeSpan RandomTimespan(double minMinues, double maxMinutes)
        {
            return TimeSpan.FromMinutes(minMinues + rand.NextDouble() * (maxMinutes - minMinues));
        }
    }
}

