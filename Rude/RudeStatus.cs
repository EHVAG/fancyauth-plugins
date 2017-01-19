using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Rude
{
    public class RudeStatus
    {
        public Action OnRudeLevelDecrease { get; set; }

        private object RudeLock = new object();

        public int RudeLevel { get; private set; }
        private int RudeIteration = 0;


        public void RaiseRudeLevel(TimeSpan length)
        {
            lock (RudeLock)
            {
                RudeLevel++;

                StartRudeDown(length, RudeIteration);
            }
        }

        public void Reset()
        {
            lock(RudeLock)
            {
                // Invalidate all old timeouts
                RudeIteration++;
                RudeLevel = 0;
            }
        }

        private async void StartRudeDown(TimeSpan length, int currentRudeInteration)
        {
            await Task.Delay(length);
            lock (RudeLock)
            {
                if (RudeIteration != currentRudeInteration)
                    return;

                RudeLevel--;

                OnRudeLevelDecrease();
            }
        }
    }
}