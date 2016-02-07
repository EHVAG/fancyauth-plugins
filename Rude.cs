using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Fancyauth.APIUtil;
using Fancyauth.API;

namespace Fancyauth.Plugins.Builtin
{
    public static class LinqExtension
    {
        public static double? Median<TColl, TValue>(
            this IEnumerable<TColl> source,
            Func<TColl, TValue>     selector)
        {
            return source.Select<TColl, TValue>(selector).Median();
        }

        public static double? Median<T>(
            this IEnumerable<T> source)
        {
            if(Nullable.GetUnderlyingType(typeof(T)) != null)
                source = source.Where(x => x != null);

            int count = source.Count();
            if(count == 0)
                return null;

            source = source.OrderBy(n => n);

            int midpoint = count / 2;
            if(count % 2 == 0)
                return (Convert.ToDouble(source.ElementAt(midpoint - 1)) + Convert.ToDouble(source.ElementAt(midpoint))) / 2.0;
            return Convert.ToDouble(source.ElementAt(midpoint));
        }
    }

    public class Rude : PluginBase
    {
        private static readonly string[] PREFIXES = {"[Rude]", "[Ruderer]", "[Rüdiger]"};
        private static readonly string[] REASONS = {"Could you please be more polite?", "Your kindness could be increased a little.", "FUCK YOU!!! is not polite."};

        private static readonly List<RudeEntity> Rudes = new List<RudeEntity>();
        private static readonly RandomNumberGenerator SecureRng = RandomNumberGenerator.Create();

        [ContextCallback("Rude")]
        public async Task RudeUser(IUser actor, IUserShim targetShim)
        {
            var bytes = new byte[4];
            SecureRng.GetBytes(bytes);
            int seed = BitConverter.ToInt32(bytes, 0);
            var rng = new Random(seed);

            var target = await targetShim.Load();

            // TODO: Remove this check when guests have UserIds
            if (target.UserId <= 0 || actor.UserId <= 0)
                return;

            var rudeEntity = new RudeEntity
            {
                Id = -1,
                ActorId = actor.UserId,
                TargetId = target.UserId,
                ActualTargetId = null,
                Timestamp = DateTimeOffset.Now,
            };
            int targetInRudes;
            // if the actor ruded himself, he won't be kicked
            bool kick = false;
            lock (Rudes)
            {
                var oneYear = DateTimeOffset.Now.AddYears(-1);
                var actorOutRudeQuery = Rudes
                        // get all rudes which actor has done
                        .Where(r => r.ActorId == actor.UserId)
                        // and where he didn't rude himself by ruding too often
                        .Where(r => r.ActualTargetId == null);

                // if actor rudes himself or has already ruded in last Minute
                bool punishActor = actorOutRudeQuery
                        // take all rudes, where the actor hasn't kicked someone
                        //  (kicking by ruding does not have a cooldown)
                        .Where(r => r.Duration != null)
                        // test, if the actor has ruded anyone in last minute
                        .Any(r => r.Timestamp > DateTime.Now.AddMinutes(-1));

                // if actor already ruded in last minute, he will be ruded instead of target
                if (punishActor)
                {
                    rudeEntity.ActualTargetId = rudeEntity.TargetId;
                    rudeEntity.TargetId = actor.UserId;
                    // he will be the new target
                    target = actor;
                }

                // get active rudes on target and add current rude
                targetInRudes = GetActiveInRudes(target.UserId).Count() + 1;
                if (targetInRudes > PREFIXES.Length && !punishActor)
                {
                    kick = true;
                    rudeEntity.Duration = null;
                }
                else
                {
                    var actorOutRudes = actorOutRudeQuery.Count();
                    var medianQuery =
                            from r in Rudes
                            where r.Timestamp > oneYear
                            group Math.Log((r.Timestamp - oneYear).TotalMinutes) by r.ActorId into g
                            select g.Sum();
                    var median = medianQuery.Median();

                    double durationFactor = actorOutRudes == 0 ? 2 : median.Value / actorOutRudes;
                    durationFactor = Math.Max(durationFactor, 0.25);
                    durationFactor = Math.Min(durationFactor, 2);
                    durationFactor *= rng.NextDouble() * targetInRudes;
                    var duration = TimeSpan.FromHours(durationFactor * 2).Ticks;
                    rudeEntity.Duration = TimeSpan.FromTicks(Math.Min(duration, TimeSpan.FromDays(1).Ticks));
                }

                // add rude
                Rudes.Add(rudeEntity);
            }

            if (kick)
            {
                await target.Kick(REASONS[rng.Next(REASONS.Length)]);
            }
            else
            {
                var name = target.Name;
                if (name.Contains("]"))
                    name = name.Substring(target.Name.IndexOf("]") + 2);
                // if actor gets punished, it is a completely valid rude on himself
                //  allowing an increase of targetInRudes to a value over 2
                var index = Math.Min(targetInRudes, 2);
                target.Name = PREFIXES[index] + " " + name;
                await target.SaveChanges();
            }
            await actor.SendMessage("Ruded with: " + rudeEntity.Duration);
        }

        public override async Task OnUserConnected(IUser user)
        {
            user.Name = GetUsername(user.UserId, user.Name);
            await user.SaveChanges();
        }

        [Command("rude")]
        public async Task RudeCommand(IUser user)
        {
            var inRudes = GetActiveInRudes(user.UserId);
            var inKicks = GetKickRudes(user.UserId, inRudes);
            var i = 1;
            var mapped = inRudes.Select(r => "Rude #" + i++ + ": " + r.Duration);
            i = 1;
            mapped.Concat(inKicks.Select(r => "SelfRude #" + i++ + ": " + r.Duration));

            user.SendMessage(String.Join("<br>", mapped));
        }

        private string GetUsername(int userId, string name)
        {
            var inRudes = GetActiveInRudes(userId).Count();
            var reudigLevel = GetReudigLevel(userId);
            return GetUsername(userId, name, inRudes, reudigLevel);
        }

        private string GetUsername(int userId, string name, int inRudes, int reudigLevel)
        {
            if (name.Contains("]"))
                name = name.Substring(name.IndexOf("]") + 2);
            // if actor gets punished, it is a completely valid rude on himself
            //  allowing an increase of inRudes to a value over 3
            var index = Math.Min(inRudes - 1, 2);
            name = PREFIXES[index] + " " + name;

            // a user can have a reudigLevel, but only be Ruderer if his first rude has timed out
            if (inRudes >= PREFIXES.Length && reudigLevel > 0)
                name.Insert(1, reudigLevel.ToString());
            return name;
        }

        private IEnumerable<RudeEntity> GetActiveInRudes(int userId)
        {
            return Rudes.Where(r => r.TargetId == userId)
                // take only rudes, which have not kicked the target
                .Where(r => r.Duration != null)
                // and which are still active
                .Where(r => r.Timestamp + r.Duration >= DateTimeOffset.Now);
        }

        private IEnumerable<RudeEntity> GetKickRudes(int userId)
        {
            var activeInRudes = GetActiveInRudes(userId);
            return GetKickRudes(userId, activeInRudes);
        }
        private IEnumerable<RudeEntity> GetKickRudes(int userId, IEnumerable<RudeEntity> activeInRudes)
        {
            var lastActiveTimestamp = activeInRudes.Max(r => r.Timestamp);
            return Rudes.Where(r => r.TargetId == userId)
                // count rudes kicking him since last actve rude
                .Where(r => r.Duration == null)
                .Where(r => r.Timestamp > lastActiveTimestamp);
        }
        private int GetReudigLevel(int userId)
        {
            return GetKickRudes(userId).Count();
        }

        private class RudeEntity
        {
            public int Id { get; set; }
            public int TargetId { get; set; }
            public int? ActualTargetId { get; set; }
            public int ActorId { get; set; }
            public TimeSpan? Duration { get; set; }
            public DateTimeOffset Timestamp { get; set; }
        }
    }
}

