using System;
using System.Collections.Generic;

public partial class Rulebook
{
    internal class StatsRules : RulebookSection
    {
        internal const int AttributesStartPosition = 5;

        public StatsRules(Rulebook rulebook) : base(rulebook)
        {
        }

        public int GetEnergyAbsorptionFromArmorItem(Item item, DamageType damageType)
        {
            if (item == null) { return 0; }
            if (!item.GetIsArmor()) { return 0; }
            var armor = item.Armor;

            return damageType switch
            {
                var x when x == DamageType.Piercing => armor.PiercingResistance,
                var x when x == DamageType.Impact => armor.ImpactResistance,
                _ => throw new NotImplementedException()
            };
        }


        private int GetBodyPiercingResistance(Character character) => 0;
        //character.Stats.Grit - AttributesStartPosition;
        private int GetBodyImpactResistance(Character character) => 0;
        //character.Stats.Physique - AttributesStartPosition;
        private int GetBodySlashingResistance(Character character) => 0;
        //character.Stats.Reflexes - AttributesStartPosition;

        // May want to factor in other elements
        public int GetEnergyAbsorptionFromCharacterBody(Character character, DamageType damageType)
        {
            return damageType switch
            {
                var x when x == DamageType.Piercing => GetBodyPiercingResistance(character),
                var x when x == DamageType.Impact => GetBodyImpactResistance(character),
                var x when x == DamageType.Slashing => GetBodySlashingResistance(character),
                _ => 0 //throw new NotImplementedException()
            };
        }

        internal List<Item> GetAliveModFramesForBodyPart(
            Character target,
            OrganicBodyPartComponent organicBodyPart)
        {
            var result = new List<Item>();

            //foreach (var frame in target.GetModFramesForBodyPart(organicBodyPart))
            //{
            //    if (frame.ModFrame.GetRemainingDamageCapacity() > 0)
            //    {
            //        result.Add(frame);
            //    }
            //}

            return result;
        }

        public float GetOrganicCoveragePercent(
            Character character,
            AnatomySlotId organicBodySlotPath)
        {
            if (!character.Anatomy.TryResolveSlot(organicBodySlotPath, out var organicBodyPart))
            {
                Debug.LogWarning("Body Slot not found");
                return 0f;
            }

            float cyberCoverage = 0f;

            //foreach (var frame in character.GetModFramesForBodyPart(organicBodyPart))
            //{
            //    // Skip destroyed frames
            //    if (frame.ModFrame.GetRemainingDamageCapacity() <= 0)
            //        continue;

            //    cyberCoverage += GetCyberFrameSurfaceAreaFromCoverage(
            //        frame.ModFrame.ModFrameCoverage
            //    );
            //}

            return Mathf.Clamp01(1f - cyberCoverage);
        }

        private float GetCyberFrameSurfaceAreaFromCoverage(ModFrameCoverage modFrameCoverage) =>
            modFrameCoverage switch
            {
                ModFrameCoverage.None => 0.0f,
                ModFrameCoverage.Subtle => 0.3f,
                ModFrameCoverage.Prominent => 0.6f,
                ModFrameCoverage.FullReplacement => 1.0f,
                _ => 0f
            };
    }
}