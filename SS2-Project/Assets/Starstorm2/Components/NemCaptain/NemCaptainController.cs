using RoR2;
using RoR2.HudOverlay;
using RoR2.Skills;
using RoR2.UI;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Linq;
using EntityStates;
namespace SS2.Components
{
    public class NemCaptainController : NetworkBehaviour, IOnTakeDamageServerReceiver, IOnDamageDealtServerReceiver, IOnKilledOtherServerReceiver
    {
        [Header("Cached Components")]
        public CharacterBody characterBody;
        public SkillLocator skillLocator;
        public Animator characterAnimator;
        

        [Header("Drone Orders")]
        public SkillDef[] deck1;
        public SkillDef[] deck2;
        public SkillDef[] deck3;
        private List<SkillDef> ordersStaticList
        {
            get
            {

                GenericSkill gs = skillLocator?.FindSkillByFamilyName("sfNemCaptainDeck");
                string skillName = gs?.skillDef?.skillName;
                if (skillName == null)
                {
                    deckFound = false;
                    return null;
                }
                switch (skillName)
                {
                    default :
                        deckFound = false;
                        return null;
                    case "NemCapDeckSimple" :
                        deckFound = true;
                        return deck1.ToList();
                    case "NemCapDeckFull" :
                        deckFound = true;
                        return deck2.ToList();
                    case "NemCapDeckBased" :
                        deckFound = true;
                        return deck3.ToList();
                }
            }
        }
        private Queue<SkillDef> ordersQueue = new Queue<SkillDef>();
        private SkillStateOverrideData TheSenatorOfAuthority;
        private bool isOverriding;

        private bool order1Set = false;
        private bool order2Set = false;
        private bool order3Set = false;

        private bool deckFound = false;
        [Header("Stress Values")]
        public float minStress;
        public float maxStress;
        public float stressPerSecondInCombat;
        public float stressPerSecondOutOfCombat;
        public float stressPerSecondWhileOverstressed;
        public float stressPerSecondWhileRegenBuff;
        public float stressGainedOnFullDamage;
        public float stressGainedOnOSP;
        public float stressGainedOnHeal;
        public float stressGainedOnCrit;
        public float stressGainedOnKill;
        public float stressGainedOnMinibossKill;
        public float stressGainedOnBossKill;
        public float stressGainedWhenSurrounded;
        public float stressGainedOnItem;
        public float stressOnOutOfOrders;
        /*public float surroundedThreshold;
        [HideInInspector]
        public static float enemyCheckInterval = 0.033333333f;
        private float enemyCheckStopwatch = 0f;
        private SphereSearch enemySearch;
        private List<HurtBox> hits;
        public float enemyRadius = 12f;*/

        [Header("Stress UI")]
        [SerializeField]
        public GameObject stressOverlayPrefab;

        [SerializeField]
        public string stressOverlayChildLocatorEntry;
        private ChildLocator stressOverlayInstanceChildlocator;
        private OverlayController stressOverlayController;
        private List<ImageFillController> fillUiList = new List<ImageFillController>();
        private Text uiStressText;

        [Header("Hand UI")]
        [SerializeField]
        public GameObject cardOverlayPrefab;

        [SerializeField]
        public string cardOverlayChildLocatorEntry;
        private ChildLocator cardOverlayInstanceChildlocator;
        private OverlayController cardOverlayController;

        private NemCaptainSkillIcon ncsi1;

        private NemCaptainSkillIcon ncsi2;
        private NemCaptainSkillIcon ncsi3;

        [Header("Drones & Drone Positions")]
        [SerializeField]
        public GameObject droneA;
        private Transform droneABaseTransform;
        [SerializeField]
        public GameObject droneB;
        private Transform droneBBaseTransform;
        [SerializeField]
        public GameObject droneC;
        private Transform droneCBaseTransform;
        [SerializeField]
        public GameObject droneAimRoot;
        private Transform droneAimRootTransform;

        private int itemCount;

        [SyncVar(hook = "OnStressModified")]
        private float _stress;
        
        public float stress
        {
            get
            {
                return _stress;
            }
        }

        public float stressFraction
        {
            get
            {
                return stress / maxStress;
            }
        }

        public float stressPercentage
        {
            get
            {
                return stressFraction * 100f;
            }
        }

        public bool isFullStress
        {
            get
            {
                return stress >= maxStress;
            }
        }

        public bool isOverstressed
        {
            get
            {
                return characterBody && characterBody.HasBuff(SS2Content.Buffs.bdOverstress);
            }
        }

        public bool isTotalReset
        {
            get
            {
                return characterBody && characterBody.HasBuff(SS2Content.Buffs.bdTotalReset);
            }
        }

        private HealthComponent bodyHealthComponent
        {
            get
            {
                return characterBody.healthComponent;
            }
        }

        public float Network_stress
        {
            get
            {
                return _stress;
            }
            [param: In]
            set
            {
                if (NetworkServer.localClientActive && !syncVarHookGuard) //lol
                {
                    syncVarHookGuard = true;
                    OnStressModified(value);
                    syncVarHookGuard = false;
                }
                SetSyncVar<float>(value, ref _stress, 1U); //please work
            }
        }

        [Server]
        public void AddStress(float amount)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Server] function 'SS2.Components.NemCaptainController::AddStress(System.Single)' called on client");
                return;
            }

            //halve mana used if has reduction buff
            if (characterBody.HasBuff(SS2Content.Buffs.bdNemCapManaReduction) && amount > 0)
                amount /= 2;

            Network_stress = Mathf.Clamp(stress + amount, minStress, maxStress);
        }

        private void OnStressModified(float newStress)
        {
            //probably ui stuff here later gulp

            Network_stress = newStress;

            if (newStress >= maxStress && !isOverstressed) //I kinda wanna switch the overstress mechanic to an entitystate ala void fiend ngl because I'm not a fan of this
            {
                //characterBody.SetBuffCount(SS2Content.Buffs.bdOverstress.buffIndex, 1);
                characterBody.AddBuff(SS2Content.Buffs.bdOverstress.buffIndex);
            }

            if (newStress <= minStress && isOverstressed)
            {
                //characterBody.SetBuffCount(SS2Content.Buffs.bdOverstress.buffIndex, 0);
                characterBody.RemoveBuff(SS2Content.Buffs.bdOverstress.buffIndex);
            }
        }

        private void OnEnable()
        {
            //add prefab & necessary hooks
            OverlayCreationParams stressOverlayCreationParams = new OverlayCreationParams
            {
                prefab = stressOverlayPrefab,
                childLocatorEntry = stressOverlayChildLocatorEntry
            };
            stressOverlayController = HudOverlayManager.AddOverlay(gameObject, stressOverlayCreationParams);
            stressOverlayController.onInstanceAdded += OnStressOverlayInstanceAdded;
            stressOverlayController.onInstanceRemove += OnStressOverlayInstanceRemoved;

            OverlayCreationParams cardOverlayCreationParams = new OverlayCreationParams
            {
                prefab = cardOverlayPrefab,
                childLocatorEntry = cardOverlayChildLocatorEntry
            };
            cardOverlayController = HudOverlayManager.AddOverlay(gameObject, cardOverlayCreationParams);
            cardOverlayController.onInstanceAdded += OnCardOverlayInstanceAdded;
            cardOverlayController.onInstanceRemove += OnCardOverlayInstanceRemoved;

            //what the fuck are we doing here?
            if (droneA != null)
                droneABaseTransform = droneA.transform;
            if (droneB != null)
                droneBBaseTransform = droneB.transform;
            if (droneC != null)
                droneCBaseTransform = droneC.transform;
            if (droneAimRoot != null)
                droneAimRootTransform = droneAimRoot.transform;

            //check for a characterbody .. just in case
            if (characterBody != null)
            {
                //characterBody.OnInventoryChanged += OnInventoryChanged;
                if (NetworkServer.active)
                {
                    HealthComponent.onCharacterHealServer += OnCharacterHealServer;

                    //setup cards
                    Debug.Log("Setup cards");
                    RefreshSkillStateOverrides();
                }
            }
        }
        /// <summary>
        /// Obviously, sets the Order Overrides. Can be called repeatedly to refresh, does not leak.
        /// </summary>
        /// <param name="handIndex"></param>
        /// <returns></returns>
        public void SetOrderOverrides()
        {
            //Debug.Log("SetOrderOverrides()");
            if (isOverriding)
                UnsetOrderOverrides();
            RefreshSkillStateOverrides();
            TheSenatorOfAuthority.OverrideSkills(skillLocator);
            characterBody.onSkillActivatedAuthority += OnSkillActivatedAuthority;
            isOverriding = true;
        }
        /// <summary>
        /// Obviously, unsets the Order Overrides. Can be called repeatedly as a failsafe, does not leak.
        /// </summary>
        /// <param name="handIndex"></param>
        /// <returns></returns>
        public void UnsetOrderOverrides()
        {
            //Debug.Log("UnsetOrderOverrides()");
            if (!isOverriding)
                return;
            TheSenatorOfAuthority.ClearOverrides();
            characterBody.onSkillActivatedAuthority -= OnSkillActivatedAuthority;
            isOverriding = false;
        }
        private void OnSkillActivatedAuthority(GenericSkill skill)
        {
            //Debug.Log("OnSkillAuthority()");
            if (skill.skillDef is OrderSkillDef skillDef && skillDef.autoHandleOrderQueue)
            {
                CycleNextOrder(skill);
            }
        }
        private void RefreshSkillStateOverrides()
        {
            //Debug.Log("RefreshOrderOverrides()");
            TheSenatorOfAuthority = new SkillStateOverrideData(characterBody)
            {
                primarySkillOverride = TheSenatorOfAuthority?.primarySkillOverride,
                utilitySkillOverride = TheSenatorOfAuthority?.utilitySkillOverride,
                specialSkillOverride = TheSenatorOfAuthority?.specialSkillOverride,

                simulateRestockForOverridenSkills = true,
                overrideFullReloadOnAssign = true
            };

            if (!order1Set)
            {
                order1Set = true;
                TheSenatorOfAuthority.primarySkillOverride = GetNextOrder();
                ncsi1.UpdateSkillRef(TheSenatorOfAuthority.primarySkillOverride);
            }

            if (!order2Set)
            {
                order2Set = true;
                TheSenatorOfAuthority.utilitySkillOverride = GetNextOrder();
                ncsi2.UpdateSkillRef(TheSenatorOfAuthority.utilitySkillOverride);
            }

            if (!order3Set)
            {
                order3Set = true;
                TheSenatorOfAuthority.specialSkillOverride = GetNextOrder();
                ncsi3.UpdateSkillRef(TheSenatorOfAuthority.specialSkillOverride);
            }
        }
        private SkillDef GetNextOrder()
        {
            //Debug.Log("GetNextOrder()");
            SkillDef orderDef;
            if (ordersQueue.TryPeek(out _))
            {
                orderDef = ordersQueue.Dequeue();
            }
            else
            {
                ResetAndShuffleQueue();
                orderDef = ordersQueue.Dequeue();
            }

            return orderDef;
        }
        private void ResetAndShuffleQueue()
        {
            //Debug.Log("ResetShuffle()");
            ordersQueue.Clear();
            List<SkillDef> shuffledOrders = ordersStaticList;
            shuffledOrders.Shuffle();
            for (int i = 0; i < shuffledOrders.Count; i++)
                ordersQueue.Enqueue(shuffledOrders[i]);
        }
        /// <summary>
        /// Call the next order to the hand for the specified skillSlot. If called manually within an entityState (In which case the skillDef's autoHandleOrderQueue should be false), call this upon activatorSkillSlot.
        /// </summary>
        /// <param name="skill"></param>
        public void CycleNextOrder(GenericSkill skill)
        {
            //Debug.Log("CycleNextOrder()");
            switch (skill)
            {
                case GenericSkill _ when skill == skillLocator.primary:
                    order1Set = false;
                    break;
                case GenericSkill _ when skill == skillLocator.utility:
                    order2Set = false;
                    break;
                case GenericSkill _ when skill == skillLocator.special:
                    order3Set = false;
                    break;
                default:
                    break;
            }
            SetOrderOverrides();
        }
        /// <summary>
        /// [deprecated] remove this reference Zebra
        /// </summary>
        /// <param name="handIndex"></param>
        /// <returns></returns>
        public void DiscardCardFromHand(int handIndex)
        {
            /*GenericSkill hand = GetHandByIndex(handIndex);
            if (hand != null)
            {
                //to-do: 'empty' skill
                Debug.Log("discarded hand");
                hand.UnsetSkillOverride(gameObject, hand.skillDef, GenericSkill.SkillOverridePriority.Replacement);
                hand.SetSkillOverride(gameObject, nullSkill, GenericSkill.SkillOverridePriority.Loadout);
            }*/
        }
        /// <summary>
        /// [deprecated] remove this reference Zebra
        /// </summary>
        /// <param name="handIndex"></param>
        /// <returns></returns>
        public void DiscardCardsAndReplace()
        {
            /*DiscardCardFromHand(1);
            Debug.Log("discarded hand 1");
            DiscardCardFromHand(2);
            Debug.Log("discarded hand 2");
            DiscardCardFromHand(3);
            Debug.Log("discarded hand 3");
            DiscardCardFromHand(4);
            Debug.Log("discarded hand 4");

            //full reset
            InitializeCards();*/
        }

        //lol
        /// <summary>
        /// [deprecated] remove this reference Zebra
        /// </summary>
        /// <param name="handIndex"></param>
        /// <returns></returns>
        private GenericSkill GetHandByIndex(int handIndex)
        {
            return null;
            /*switch (handIndex)
            {
                case 1:
                    return hand1;
                case 2:
                    return hand2;
                case 3:
                    return hand3;
                case 4:
                    return hand4;
                default:
                    return null;
            }*/
        }

        private void OnDisable()
        {
            if (stressOverlayController != null)
            {
                stressOverlayController.onInstanceAdded -= OnStressOverlayInstanceAdded;
                stressOverlayController.onInstanceRemove -= OnStressOverlayInstanceRemoved;
                fillUiList.Clear();
                HudOverlayManager.RemoveOverlay(stressOverlayController);
            }
            if (characterBody)
            {
                //characterBody.onInventoryChanged -= OnInventoryChanged;
                if (NetworkServer.active)
                {
                    HealthComponent.onCharacterHealServer -= OnCharacterHealServer;
                }
            }
        }

        private void OnStressOverlayInstanceAdded(OverlayController controller, GameObject instance)
        {
            fillUiList.Add(instance.GetComponent<ImageFillController>());
            uiStressText = instance.GetComponent<Text>();
            //uiStressText.font = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidSurvivor/animVoidSurvivorCorruptionUI.controller").WaitForCompletion().GetComponent<TextMeshProUGUI>().font;
            //uiStressText.fontSharedMaterial = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidSurvivor/animVoidSurvivorCorruptionUI.controller").WaitForCompletion().GetComponent<TextMeshProUGUI>().fontSharedMaterial;
            //uiStressText.fontMaterial = Addressables.LoadAssetAsync<GameObject>("RoR2/DLC1/VoidSurvivor/animVoidSurvivorCorruptionUI.controller").WaitForCompletion().GetComponent<TextMeshProUGUI>().fontMaterial;

            stressOverlayInstanceChildlocator = instance.GetComponent<ChildLocator>();
        }

        private void OnStressOverlayInstanceRemoved(OverlayController controller, GameObject instance)
        {
            fillUiList.Remove(instance.GetComponent<ImageFillController>());
        }

        private void OnCardOverlayInstanceAdded(OverlayController controller, GameObject instance)
        {
            cardOverlayInstanceChildlocator = instance.GetComponent<ChildLocator>();

            if (cardOverlayInstanceChildlocator == null)
            {
                Debug.Log("card overlay instance childlocator null; returning");
                return;
            }

            Transform icon1 = cardOverlayInstanceChildlocator.FindChild("Skill1");
            if (icon1 != null)
            {
                Debug.Log("setting skill 1");
                ncsi1 = icon1.GetComponent<NemCaptainSkillIcon>();
                ncsi1.targetSkill = TheSenatorOfAuthority.primarySkillOverride;
                ncsi1.characterBody = characterBody;
            }

            Transform icon2 = cardOverlayInstanceChildlocator.FindChild("Skill2");
            if (icon2 != null)
            {
                Debug.Log("setting skill 2");
                ncsi2 = icon2.GetComponent<NemCaptainSkillIcon>();
                ncsi2.targetSkill = TheSenatorOfAuthority.utilitySkillOverride;
                ncsi2.characterBody = characterBody;
            }

            Transform icon3 = cardOverlayInstanceChildlocator.FindChild("Skill3");
            if (icon3 != null)
            {
                Debug.Log("setting skill 3");
                ncsi3 = icon3.GetComponent<NemCaptainSkillIcon>();
                ncsi3.targetSkill = TheSenatorOfAuthority.specialSkillOverride;
                ncsi3.characterBody = characterBody;
            }
        }

        private void OnCardOverlayInstanceRemoved(OverlayController controller, GameObject instance)
        {
            fillUiList.Remove(instance.GetComponent<ImageFillController>());
        }

        private void FixedUpdate()
        {
            if (!deckFound)
            {
                RefreshSkillStateOverrides();
            }
            float num;

            num = characterBody.outOfCombat ? stressPerSecondOutOfCombat : stressPerSecondInCombat;


            if (characterBody.HasBuff(SS2Content.Buffs.bdNemCapManaRegen))
                num += stressPerSecondWhileRegenBuff;

            if (isOverstressed)
                num = stressPerSecondWhileOverstressed;

            //add final stress per second amount; no stress if invincible
            if (NetworkServer.active && !(characterBody.HasBuff(RoR2Content.Buffs.HiddenInvincibility) || characterBody.HasBuff(RoR2Content.Buffs.Immune)))
                AddStress(num * Time.fixedDeltaTime);

            UpdateUI();
        }

        private void UpdateUI()
        {
            foreach (ImageFillController imageFillController in fillUiList)
            {
                imageFillController.SetTValue(stress / maxStress);
            }
            if (stressOverlayInstanceChildlocator)
            {
                Transform wazzok = stressOverlayInstanceChildlocator.FindChild("StressThreshold");
                if (wazzok) //dwarven engineering at its finest
                    wazzok.rotation = Quaternion.Euler(0f, 0f, Mathf.InverseLerp(0f, maxStress, stress) * -360f);
                //overlayInstanceChildlocator.FindChild("MinStressThreshold");
            }
            if (uiStressText)
            {
                StringBuilder stringBuilder = StringBuilderPool.RentStringBuilder();
                stringBuilder.AppendInt(Mathf.FloorToInt(stress), 1U, 3U).Append("%");
                uiStressText.text = stringBuilder.ToString();
                StringBuilderPool.ReturnStringBuilder(stringBuilder);
            }
        }

        private void OnCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
        {
            if (healthComponent == bodyHealthComponent)
            {
                float num = amount / bodyHealthComponent.fullCombinedHealth;
                AddStress(num * stressGainedOnHeal);
            }
        }

        public void OnDamageDealtServer(DamageReport damageReport)
        {
            if (damageReport.damageInfo.crit)
                AddStress(damageReport.damageInfo.procCoefficient * stressGainedOnCrit);
        }

        public void OnTakeDamageServer(DamageReport damageReport)
        {
            float num = damageReport.damageDealt / bodyHealthComponent.fullCombinedHealth;
            AddStress(num * stressGainedOnFullDamage);
        }

        public void OnKilledOtherServer(DamageReport damageReport)
        {
            if (damageReport.victimIsBoss || damageReport.victimIsChampion || damageReport.victimBody.hullClassification == HullClassification.BeetleQueen)
            {
                AddStress(stressGainedOnBossKill);
                return;
            }

            if (damageReport.victimIsElite || damageReport.victimBody.hullClassification == HullClassification.Golem)
            {
                AddStress(stressGainedOnMinibossKill);
                return;
            }

            AddStress(stressGainedOnKill);
        }

        private void OnInventoryChanged()
        {

        }

        

        public override void PreStartClient()
        {
        }

        private void UNetVersion()
        {
        }

        //magic idk
        public override bool OnSerialize(NetworkWriter writer, bool forceAll)
        {
            if (forceAll)
            {
                writer.Write(_stress);
                return true;
            }
            bool flag = false;
            if ((syncVarDirtyBits & 1U) != 0U)
            {
                if (!flag)
                {
                    writer.WritePackedUInt32(syncVarDirtyBits);
                    flag = true;
                }
                writer.Write(_stress);
            }
            if (!flag)
            {
                writer.WritePackedUInt32(syncVarDirtyBits);
            }
            return flag;
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                _stress = reader.ReadSingle();
                return;
            }
            int num = (int)reader.ReadPackedUInt32();
            if ((num & 1) != 0)
            {
                OnStressModified(reader.ReadSingle());
            }
        }
    }

    public static class ListExtension
    {
        internal static void Shuffle<T>(this List<T> list)
        {
            int count = list.Count;
            while (count > 1)
            {
                int swapWith = UnityEngine.Random.RandomRange(0, count);
                count--;
                T value = list[count];
                list[count] = list[swapWith];
                list[swapWith] = value;
            }
        }
    }
}
