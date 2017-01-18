using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Fancyauth.APIUtil;
using Fancyauth.API;

namespace Rude
{
    public class RudePlugin : PluginBase
    {
        private readonly string[] Prefixes = { "[Rude] ", "[Ruderer] ", "[Rüdiger] " };
        public static readonly TimeSpan RudeCooldown = TimeSpan.FromMinutes(30);

        public Dictionary<int, RudeStatus> RudeStatus = new Dictionary<int, RudeStatus>();

        [ContextCallback("Rude")]
        public async Task RudeUser(IUser actor, IUserShim targetShim)
        {
            EnsureRudeStatus(actor.UserId);
            var rude = RudeStatus[actor.UserId];

            rude.RaiseRudeLevel();

            if (rude.RudeLevel > Prefixes.Length)
            {
                RudeStatus.Remove(actor.UserId);
                await actor.Kick("You have been too rude.");
            }
            else
            {
                actor.Name = GetUserName(actor);
                await actor.SaveChanges();
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
                RudeStatus[userId] = new RudeStatus
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
                };
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
    }
}

