using System;
using System.Collections.Generic;
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
            return (bytes[0] << 24 + bytes[1] << 16 + bytes[2] << 8 + bytes[3]) % to;
        }
    }

    public class Rude : PluginBase
    {
        private static readonly string[] PREFIXES = {"[Rude]", "[Ruderer]", "[Rüdiger]", "[Level Java]"};
        private static readonly string[] REASONS = {"Could you please be more polite?", "Your kindness could be increased a little.", "FUCK YOU!!! is not polite."};

        private Dictionary<int, int> RudeLevels = new Dictionary<int, int>();
        private RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        [ContextCallback("Rude")]
        public async Task RudeUser(API.IUser from, API.IUserShim target)
        {
            var user = await target.Load();
            var id = user.UserId;
            var level = RudeLevels[id];
            level++;
            if (level >= PREFIXES.Length) {
                await user.Kick(REASONS[Rng.Next(REASONS.Length)]);
            } else {
                user.Name = PREFIXES[level];
                await user.SaveChanges();
            }
        }

    }
}
