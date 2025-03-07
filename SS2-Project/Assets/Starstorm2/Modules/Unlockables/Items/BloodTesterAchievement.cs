﻿using RoR2;
using RoR2.Achievements;
namespace SS2.Unlocks.Pickups
{
    public sealed class BloodTesterAchievement : BaseAchievement
    {
        public float healthAccumulation = 0;

        public override void OnInstall()
        {
            base.OnInstall();
            HealthComponent.onCharacterHealServer += CheckHealth;
        }

        public override void OnUninstall()
        {
            HealthComponent.onCharacterHealServer -= CheckHealth;
            base.OnUninstall();
        }
        private void CheckHealth(HealthComponent healthComponent, float healthAmount, ProcChainMask procChainMask)
        {
            if (!healthComponent.body.Equals(localUser.cachedBody) || Run.instance.isRunStopwatchPaused)
            {
                return;
            }
            bool flag = TeleporterInteraction.instance ? !TeleporterInteraction.instance.isCharged : true;
            if (flag)
            {
                if (healthComponent.health < healthComponent.body.maxHealth)
                {
                    healthAccumulation += healthAmount;
                }
            }
            if (healthAccumulation > 5000f)
            {
                Grant();
            }
        }
    }
}