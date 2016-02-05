using System;
using System.Linq;
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
            return Math.Abs(BitConverter.ToInt32(bytes, 0)) % to;
        }
    }

    public class Rude : PluginBase
    {
        private static readonly string[] PREFIXES = {"[Rude]", "[Ruderer]", "[Rüdiger]", "[Level Java]"};
        private static readonly string[] REASONS = {"Could you please be more polite?", "Your kindness could be increased a little.", "FUCK YOU!!! is not polite."};

        private static readonly List<RudeEntity> Rudes = new List<RudeEntity>();
        private static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        [ContextCallback("Rude")]
        public async Task RudeUser(IUser from, IUserShim target)
        {
            var user = await target.Load();
            int rudes;
            lock (Rudes)
            {
                Rudes.Add(new RudeEntity{ Id = -1, UserId = user.UserId,
                        RuderId = from.UserId, Effectiveness = -1,
                        Timestamp = DateTime.Now });
                rudes = Rudes.Where(x => x.UserId == user.UserId)
                    .Count(x => x.Timestamp > DateTime.Now.AddHours(-2));
            }

            if (rudes >= PREFIXES.Length)
            {
                await user.Kick(REASONS[Rng.Next(REASONS.Length)]);
            }
            else
            {
                var name = user.Name.Substring(user.Name.IndexOf("]") + 2);
                user.Name = PREFIXES[rudes-1] + " " + name;
                await user.SaveChanges();
            }
        }

        private class RudeEntity {
            public int Id { get; set; }
            public int UserId { get; set; }
            public int RuderId { get; set; }
            public float Effectiveness { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
