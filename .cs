using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Fancyauth.APIUtil;

namespace Fancyauth.Plugins.Builtin
{
    public class Rude : PluginBase
    {
        private static readonly string[] PREFIXES = {"[Rude]", "[Ruderer]", "[Rüdiger]", "[Level Java]"};
        private static readonly string[] REASONS = {"Could you please be more polite?", "Your kindness could be increased a little.", "FUCK YOU!!! is not polite."};

        private Dictionary<int, int> RudeLevels = new Dictionary<int, int>();
        private SecureRandom Rng = new Random();

        [ContextCallback("Rude")]
        public async Task RudeUser(API.IUser from, API.IUserShim target)
        {
            var user = await target.Load();
            var id = user.UserId;
            var level = RudeLevels[id];
            if (level >= names.Length) {
                await user.Kick(REASONS[Rng.Next(REASONS.Length)]);
            } else {
                user.Name = PREFIXES[Rng.Next(PREFIXES.Length)];
                await user.SaveChanges();
            }
        }
    }
}
