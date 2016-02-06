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
                Duration = null,
                Timestamp = DateTimeOffset.Now,
            };
            bool actorSelfRude;
            int targetInRudes;
            int reudigLevel;
            bool kicked;
            lock (Rudes)
            {
                var oneYear = DateTimeOffset.Now.AddYears(-1);
                var actorOutRudeQuery = Rudes.Where(r => r.ActorId == actor.UserId)
                        .Where(r => r.ActualTargetId != null);

                // if actor rudes himself or has already ruded in last Minute
                actorSelfRude = (actor.UserId == target.UserId)
                    || actorOutRudeQuery.Where(r => r.Duration != null)
                        .Any(r => r.Timestamp > DateTime.Now.AddMinutes(-1));

                // if actor already ruded in last minute, he will be ruded instead of target
                if (actorSelfRude)
                {
                    rudeEntity.ActualTargetId = rudeEntity.TargetId;
                    rudeEntity.TargetId = actor.UserId;
                    // if ruder ruded himself (accidently or not) he will be the new target
                    target = actor;
                }

                var targetQuery = Rudes.Where(r => r.TargetId == target.UserId);
                var targetRudesQuery = targetQuery.Where(r => r.Duration != null)
                        .Where(r => r.Timestamp + r.Duration > DateTimeOffset.Now);
                // get all active rudes on target
                targetInRudes = targetRudesQuery.Count();
                if (targetInRudes > PREFIXES.Length && !actorSelfRude)
                {
                    kicked = true;
                    rudeEntity.Duration = null;
                }
                else
                {
                    var lastActiveRude = targetRudesQuery.Max(r => r.Timestamp);
                    reudigLevel = targetQuery.Where(r => r.Duration == null)
                            .Count(r => r.Timestamp > lastActiveRude);

                    // get factors
                    var yearQuery = Rudes.Where(r => r.Timestamp > oneYear);
                    var actorOutRudes = actorOutRudeQuery.Count();
                    var median = yearQuery.Select(r => new {
                            r.ActorId,
                            Value = Math.Log((r.Timestamp - oneYear).TotalMinutes),
                        }).GroupBy(x => x.ActorId)
                        .Select(g => g.Sum(t => t.Value))
                        .Median();

                    double durationFactor = median.Value / actorOutRudes;
                    durationFactor = Math.Max(durationFactor, 0.25);
                    durationFactor = Math.Min(durationFactor, 2);
                    var duration = rng.NextDouble() * (targetInRudes + reudigLevel);
                    duration *= durationFactor * 120;
                    rudeEntity.Duration = new TimeSpan(0, (int)duration, 0);
                }

                // add rude
                Rudes.Add(rudeEntity);
            }

            // if the actor ruded himself, he won't be kicked
            if (kicked)
            {
                await target.Kick(REASONS[rng.Next(REASONS.Length)]);
            }
            else
            {
                var targetRudes = targetInRudes + reudigLevel;
                var name = target.Name;
                if (name.Contains("]"))
                    name = name.Substring(target.Name.IndexOf("]") + 2);
                var i = Math.Min(targetRudes - 1, PREFIXES.Length - 1);
                target.Name = PREFIXES[i] + " " + name;
                if (reudigLevel > 0)
                {
                    target.Name.Insert(1, reudigLevel.ToString());
                }
                await target.SaveChanges();
            }
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

