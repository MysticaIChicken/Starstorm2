namespace EntityStates.NemCaptain
{
    public class ForcedCooldown : BaseState
    {
        public static float baseDuration = 0.5f;
        private float duration;

        //maybe play an animation here

        public override void OnEnter()
        {
            base.OnEnter();
            duration = baseDuration / attackSpeedStat;
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (fixedAge >= duration)
                outer.SetNextStateToMain();
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Frozen;
        }
    }
}
