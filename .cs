using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Fancyauth.APIUtil;
using Fancyauth.API;

namespace Fancyauth.Plugins.Builtin
{
    public static class RngExtension
    {
        public static int Next(this RandomNumberGenerator rng, int to) {
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0) % to;
        }
    }

    public class Rude : PluginBase
    {
        private static readonly string[] PREFIXES = {"[Rude]", "[Ruderer]", "[Rüdiger]", "[Level Java]"};
        private static readonly string[] REASONS = {"Could you please be more polite?", "Your kindness could be increased a little.", "FUCK YOU!!! is not polite."};

        private ConcurrentDictionary<int, int> RudeLevels = new ConcurrentDictionary<int, int>();
        private RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        [ContextCallback("Rude")]
        public async Task RudeUser(IUser from, IUserShim target)
        {
            var user = await target.Load();
            var level = RudeLevels.AddOrUpdate(user.UserId, 0, (_, i) => i + 1);
            if (level >= PREFIXES.Length)
            {
                RudeLevels.TryRemove(user.UserId, out level);
                await user.Kick(REASONS[Rng.Next(REASONS.Length)]);
            }
            else
            {
                user.Name = PREFIXES[level];
                await user.SaveChanges();
            }
        }

    }
}
