using System;
using System.Collections.Generic;

public partial class Rulebook
{
    internal class InjuriesAndDamageRules : RulebookSection
    {
        private const int MaxSeverity = 3;
        private const int EnergyPerSeverityPoint = 5;

        // INJURY COMPLICATION TABLE
        private readonly Dictionary<(DamageType damageType, int severity), OrganicInjuryComplication>
            InjuryComplicationTable = new()
        {
            { (DamageType.Piercing, 3), OrganicInjuryComplication.Disabled },
            { (DamageType.Impact, 3), OrganicInjuryComplication.Disabled },
            { (DamageType.Slashing, 3), OrganicInjuryComplication.Bleeding },
        };

        private readonly Dictionary<(DamageType damageType, int severity), CyberneticComplication>
            CyberneticComplicationTable = new()
        {
            { (DamageType.Piercing, 3), CyberneticComplication.None },
            { (DamageType.Impact, 3), CyberneticComplication.None },
            { (DamageType.Slashing, 3), CyberneticComplication.None },
        };

        // INJURY TREATMENTS TABLE
        private readonly Dictionary<(DamageDomain domain, DamageType damageType, int severity), IReadOnlyList<string>>
            DamagePossibleTreatmentsTable = new()
        {
            // === ORGANIC ===
            // PIERCING
            { (DamageDomain.Organic, DamageType.Piercing, 1), new[] { "Rest", "Item: Apply Ointment" } },
            { (DamageDomain.Organic, DamageType.Piercing, 2), new[] { "Rest", "Item: Apply Patch" } },
            { (DamageDomain.Organic, DamageType.Piercing, 3), new[] { "Surgery: Extract Foreign Object" } },
            // IMPACT
            { (DamageDomain.Organic, DamageType.Impact, 1), new[] { "Rest", "Item: Apply Ointment" } },
            { (DamageDomain.Organic, DamageType.Impact, 2), new[] { "Rest", "Item: Apply Cast" } },
            { (DamageDomain.Organic, DamageType.Impact, 3), new[] { "Surgery: Install Splint" } },
            // SLASHING
            { (DamageDomain.Organic, DamageType.Slashing, 1), new[] { "Rest", "Item: Apply Ointment" } },
            { (DamageDomain.Organic, DamageType.Slashing, 2), new[] { "Rest", "Item: Wrap with Bandages" } },
            { (DamageDomain.Organic, DamageType.Slashing, 3), new[] { "Surgery: Close with Stitches" } },

            // === CYBERNETIC ===
            // PIERCING
            { (DamageDomain.Cybernetic, DamageType.Piercing, 1), new[] { "Rest", "Item: Apply Ointment" } },
            { (DamageDomain.Cybernetic, DamageType.Piercing, 2), new[] { "Rest", "Item: Apply Patch" } },
            { (DamageDomain.Cybernetic, DamageType.Piercing, 3), new[] { "Surgery: Extract Foreign Object" } },
            // IMPACT
            { (DamageDomain.Cybernetic, DamageType.Impact, 1), new[] { "Rest", "Item: Apply Ointment" } },
            { (DamageDomain.Cybernetic, DamageType.Impact, 2), new[] { "Rest", "Item: Apply Cast" } },
            { (DamageDomain.Cybernetic, DamageType.Impact, 3), new[] { "Surgery: Install Splint" } },
            // SLASHING
            { (DamageDomain.Cybernetic, DamageType.Slashing, 1), new[] { "Rest", "Item: Apply Ointment" } },
            { (DamageDomain.Cybernetic, DamageType.Slashing, 2), new[] { "Rest", "Item: Wrap with Bandages" } },
            { (DamageDomain.Cybernetic, DamageType.Slashing, 3), new[] { "Surgery: Close with Stitches" } },

            // === WORLD OBJECTS ===
            // PIERCING
            { (DamageDomain.Structural, DamageType.Piercing, 1), new[] { "Item: Something" } },
            { (DamageDomain.Structural, DamageType.Piercing, 2), new[] { "Item: Something" } },
            { (DamageDomain.Structural, DamageType.Piercing, 3), new[] { "Construction: Repair" } },
            // IMPACT
            { (DamageDomain.Structural, DamageType.Impact, 1), new[] { "Rest", "Item: Something" } },
            { (DamageDomain.Structural, DamageType.Impact, 2), new[] { "Rest", "Item: Something" } },
            { (DamageDomain.Structural, DamageType.Impact, 3), new[] { "Construction: Repair" } },
            // SLASHING
            { (DamageDomain.Structural, DamageType.Slashing, 1), new[] { "Rest", "Item: Something" } },
            { (DamageDomain.Structural, DamageType.Slashing, 2), new[] { "Rest", "Item: Something" } },
            { (DamageDomain.Structural, DamageType.Slashing, 3), new[] { "Construction: Repair" } },
        };

        public InjuriesAndDamageRules(Rulebook rulebook) : base(rulebook)
        {
        }

        internal IReadOnlyList<string> GetPossibleTreatmentsForInjury(DamageInstance damageInstance)
        {
            if (DamagePossibleTreatmentsTable.TryGetValue((DamageDomain.Organic, damageInstance.DamageType, damageInstance.DamageSeverity), out var treatments))
            {
                return treatments;
            }

            return Array.Empty<string>();
        }

        internal bool TryTreatInjury(
            Character self,
            Character target,
            AnatomySlotId slot,
            DamageInstance injury)
        {
            // TODO: Rules for treatment

            // Remove Injury Instance
            if (!target.Anatomy.TryRemoveInjury(slot, injury))
            {
                return false;
            }

            // Depending on the treatment and surgeon skill, healing can result in other injuries
            if (injury.DamageSeverity == 3)
            {
                target.Anatomy.TryApplyInjury(slot, CreateOrganicDamage(DamageType.Impact, 1));
            }

            //Rulebook.GameAPI.RaiseEvent(new InjuryTreated(self, target));
            return true;
        }

        // CONVERT ENERGY INTO INJURY/DAMAGE SEVERITY POINTS
        // Converts remaining energy into severity.
        // Any non-zero energy results in at least severity 1.
        // Each additional EnergyPerSeverityPoint increases severity by 1, capped at MaxSeverity.
        internal int GetSeverityFromEnergy(float energy)
        {
            if (energy <= 0)
                return 0;

            int level = Mathf.FloorToInt(energy / EnergyPerSeverityPoint) + 1;
            return Mathf.Clamp(level, 0, MaxSeverity);
        }

        internal int ComputeMaximumEnergyForSeverity(int severity)
        {
            return severity * EnergyPerSeverityPoint;
        }

        // BODY PART DAMAGE
        internal DamageInstance CreateOrganicDamage(
            DamageType damageType,
            int damageSeverity)
        {
            InjuryComplicationTable.TryGetValue((damageType, damageSeverity), out var injuryComplication);

            return new DamageInstance(
                DamageDomain.Organic,
                damageType,
                damageSeverity);
        }

        internal bool TryApplyDamageToOrganicBodyPart(CharacterId characterId, AnatomySlotId bodyPartSlot, DamageInstance injuryInstance)
        {
            if (!Rulebook.GameAPI.Databases.TryGetModel(characterId, out Character character)) { return false; }
            if (!character.Anatomy.TryResolveSlot(bodyPartSlot, out var slot)) { return false; }

            return TryApplyDamageToOrganicBodyPart(slot, injuryInstance);
        }

        internal bool TryApplyDamageToOrganicBodyPart(OrganicBodyPartComponent bodyPart, DamageInstance injuryInstance)
        {
            return bodyPart.TryApplyInjury(injuryInstance);
        }

        // MOD FRAME DAMAGE
        // Just raw damage instance. Complications and application happen later
        internal DamageInstance CreateCyberDamage(
            DamageType damageType,
            int damageSeverity)
        {
            //CyberneticComplicationTable.TryGetValue((damageType, damageSeverity), out var complication);

            return new DamageInstance(
                DamageDomain.Cybernetic,
                damageType,
                damageSeverity);
        }

        internal bool TryApplyDamageToCyberneticFrame(ItemId modFrameItemUid, DamageInstance damageInstance)
        {
            if (Rulebook.GameAPI.Databases.TryGetModel(modFrameItemUid, out Item modFrameItem))
            {
                return TryApplyDamageToCyberneticFrame(modFrameItem, damageInstance);
            }

            return false;
        }

        internal bool TryApplyDamageToCyberneticFrame(Item modFrameItem, DamageInstance damageInstance)
        {
            if (!modFrameItem.GetIsModFrame()) { return false; }
            return modFrameItem.ModFrame.TryApplyDamage(damageInstance);
        }

        // CLOTHING DAMAGE
        internal DamageInstance CreateClothingDamage(
            Item item,
            DamageType damageType,
            int damageSeverity
        )
        {
            return new DamageInstance(DamageDomain.Structural, damageType, damageSeverity);
        }

        internal void TryApplyClothingDamage(
            ItemId clothingItemId,
            DamageInstance itemDamageInstance)
        {
            if (Rulebook.GameAPI.Databases.TryGetModel(clothingItemId, out Item clothingItem))
            {
                clothingItem.ApplyDamage(itemDamageInstance);
            }
        }

        internal void TryApplyClothingDamage(
            Item clothingItem,
            DamageInstance itemDamageInstance)
        {
            clothingItem.ApplyDamage(itemDamageInstance);
        }

        //internal HealthDTO GetHealthSummaryOfWorldObject(WorldObject worldObject)
        //{
        //    return new HealthDTO(
        //        0, 0,
        //        worldObject.GetCurrentCyberneticHP(), worldObject.GetMaxCyberneticHP(),
        //        worldObject.GetCurrentStructuralHP(), worldObject.GetMaxStructuralHP());

        //}

        internal IReadOnlyList<ResistanceData> GetResistancesSummaryOfWorldObject(Prop prop)
        {
            return prop.GetResistances();
        }

        internal int GetEnergyMitigationOfProp(Prop prop)
        {
            // Projects severity
            int projDamageSeverity = GetSeverityFromEnergy(EnergyPerSeverityPoint * MaxSeverity);
            //int severity = Mathf.Min(projDamageSeverity, minHPSum);
            int projEnergyConsumed = ComputeMaximumEnergyForSeverity(projDamageSeverity);

            var totalHP = prop.GetMaxHP();
            int minEnergyMitigation = projEnergyConsumed * totalHP; 

            return minEnergyMitigation;
        }

        internal bool TryDamagePropLayer(
            MapChunk map, 
            Prop prop, 
            PropLayer layer, 
            DamageType damageType, 
            int damageSeverity,
            out CombatAPI.Events.PropLayerDamagedEvent evt)
        {
            evt = null;
            if (prop.GetIsDestroyed()) return false;

            var damageInstance = new DamageInstance(
                layer.DamageDomain,
                damageType,
                damageSeverity);

            layer.DamageInstances.Add(damageInstance);

            evt = new CombatAPI.Events.PropLayerDamagedEvent(
                map.Id,
                prop.Id,
                layer.Id,
                new DamageInstancePresentation(damageInstance),
                prop.GetIsDestroyed(),
                prop.GetHPPercentage());

            return true;
        }
    }
}