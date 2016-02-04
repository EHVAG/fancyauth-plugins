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
            var id = user.UserId;
            int level;
            if (!RudeLevels.TryGetValue(id, out level))
                level = 0;
            else
                level++;
            if (level >= PREFIXES.Length) {
                var reason = REASONS[Rng.Next(REASONS.Length)];
                if (!RudeLevels.TryRemove(id, out level))
                    reason = "This should not have happened. Please report to your local Admin. (really, this is a bug in the plugin)";
                await user.Kick(reason);
            } else {
                user.Name = PREFIXES[level];
                RudeLevels.AddOrUpdate(id, _ => 0, (_,i) => i+1);
                await user.SaveChanges();
            }
        }

    }
}
