using RoR2.Skills;
using UnityEngine;
using JetBrains.Annotations;
using RoR2;
using SS2.Components;

namespace SS2
{
    [CreateAssetMenu(menuName = "Starstorm2/SkillDef/OrderSkillDef")]
    public class OrderSkillDef : SkillDef
    {
        [Tooltip("The amount of stress added by this skill.")]
        public float stressValue = 0;
        [Tooltip("If the skill can be casted while overstressed.")]
        public bool canCastIfOverstressed = false;
        [Tooltip("If the skill can be casted when there is insufficient stress.")]
        public bool canCastIfWillOverstress = false;
        /// <summary>
        /// If the skill will automatically cycle to the next Order (immediately on activation) or if it will be handled manually. Defaults to true.
        /// If False, YOU MUST CALL :
        /// <code>
        /// NemCaptainController.CycleNextOrder(GenericSkill skill)
        /// </code>
        /// or rather :
        /// <code>
        /// 'ncc.CycleNextOrder(activatorSkillSlot)'
        /// </code>
        /// in the entity state!
        /// </summary>
        [Tooltip("If the skill will automatically cycle to the next Order (immediately on activation) or if it will be handled manually. Defaults to true. If False, YOU MUST CALL NemCaptainController.CycleNextOrder(activatorSkillSlot) in the entity state!")]
        public bool autoHandleOrderQueue = true;
        public override BaseSkillInstanceData OnAssigned([NotNull] GenericSkill skillSlot)
        {
            return new InstanceData
            {
                ncc = skillSlot.GetComponent<NemCaptainController>()
            };
        }

        private static bool IsOverstressed([NotNull] GenericSkill skillSlot)
        {
            NemCaptainController ncc = ((InstanceData)skillSlot.skillInstanceData).ncc;
            return ncc.isOverstressed;
        }

        private static bool IsTotalReset([NotNull] GenericSkill skillSlot)
        {
            NemCaptainController ncc = ((InstanceData)skillSlot.skillInstanceData).ncc;
            return ncc.isTotalReset;
        }

        public override bool CanExecute([NotNull] GenericSkill skillSlot)
        {
            NemCaptainController ncc = ((InstanceData)skillSlot.skillInstanceData).ncc;
            return base.CanExecute(skillSlot) && (!IsOverstressed(skillSlot) || canCastIfOverstressed) && !IsTotalReset(skillSlot) && (((ncc.maxStress - ncc.stress) > stressValue) || canCastIfWillOverstress);
        }

        public override bool IsReady([NotNull] GenericSkill skillSlot)
        {
            NemCaptainController ncc = ((InstanceData)skillSlot.skillInstanceData).ncc;
            return base.IsReady(skillSlot) && (!IsOverstressed(skillSlot) || canCastIfOverstressed) && !IsTotalReset(skillSlot) && (((ncc.maxStress - ncc.stress) > stressValue) || canCastIfWillOverstress);
        }

        public override void OnExecute([NotNull] GenericSkill skillSlot)
        {
            base.OnExecute(skillSlot);
            NemCaptainController ncc = ((InstanceData)skillSlot.skillInstanceData).ncc;
            ncc.AddStress(stressValue);
        }

        protected class InstanceData : SkillDef.BaseSkillInstanceData
        {
            public NemCaptainController ncc;
        }
    }
}
