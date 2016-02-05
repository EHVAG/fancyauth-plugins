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
        public async Task RudeUser(IUser actor, IUserShim targetShim)
        {
            var target = await targetShim.Load();
            bool actorRuded;
            int targetRudes;
            lock (Rudes)
            {
                // if actor rudes himself or has already ruded in last Minute
                actorRuded = actor.UserId == target.UserId
                    || (from rude in Rudes
                        where rude.ActorId == actor.UserId
                        where rude.ActualTargetId != -1
                        where rude.Timestamp > DateTime.Now.AddMinutes(-1)
                        select rude).Any();

                var rudeEntity = new RudeEntity{ Id = -1, ActorId = actor.UserId,
                        TargetId = target.UserId, ActualTargetId = -1,
                        Effectiveness = -1, Timestamp = DateTime.Now };

                // if actor already ruded in last minute, he will be ruded instead of target
                if (actorRuded)
                {
                    rudeEntity.ActualTargetId = rudeEntity.TargetId;
                    rudeEntity.TargetId = actor.UserId;
                    // if ruder ruded himself (accidently or not) he will be the new target
                    target = actor;
                }
                // add rude
                Rudes.Add(rudeEntity);

                // get all rudes in the last 2 hours of target
                targetRudes =
                    (from rude in Rudes
                    where rude.TargetId == target.UserId
                    where rude.Timestamp > DateTime.Now.AddHours(-2)
                    select rude).Count();
            }

            // The last message in PREFIXES is for users ruding themselfs when
            //  they already have the highest rude before being kicked
            // This permits them to reset their rude-state by kicking themselfes
            //  either by ruding themselfs or by ruding others too fast
            // TODO: no state is reset on dc... is this really needed?????
            if (targetRudes > PREFIXES.Length-1 && !actorRuded)
            {
                await target.Kick(REASONS[Rng.Next(REASONS.Length)]);
            }
            else
            {
                var name = target.Name;
                if (name.Contains("]"))
                    name = name.Substring(target.Name.IndexOf("]") + 2);
                var i = Math.Min(targetRudes - 1, 3);
                target.Name = PREFIXES[i] + " " + name;
                await target.SaveChanges();
            }
        }

        private class RudeEntity {
            public int Id { get; set; }
            public int TargetId { get; set; }
            public int ActualTargetId { get; set; }
            public int ActorId { get; set; }
            public float Effectiveness { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}

