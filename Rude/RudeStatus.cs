using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Rude
{
    public class RudeStatus
    {
        public Action OnRudeLevelDecrease;

        private object RudeLock = new object();

        public int RudeLevel { get; private set; }
        private int RudeIteration = 0;


        public void RaiseRudeLevel()
        {
            lock (RudeLock)
            {
                RudeLevel++;
                RudeIteration++;

                StartRudeDown(RudeIteration);
            }
        }

        private async void StartRudeDown(int currentRudeInteration)
        {
            await Task.Delay(RudePlugin.RudeCooldown);
            lock (RudeLock)
            {
                if (RudeIteration != currentRudeInteration)
                    return;

                RudeLevel = RudeLevel - 1;
                RudeIteration++;

                if (RudeLevel > 0)
                    StartRudeDown(RudeIteration);

                OnRudeLevelDecrease();
            }
        }
    }
}