using System;
using System.Collections.Generic;

public partial class Rulebook
{
    internal bool CharacterCanBeAssignedToPlayer(Character character)
    {
        return true;
    }

    //TODO: Apply a debuff to armor based on condition
    internal class ArmorRules : RulebookSection
    {
        public ArmorRules(Rulebook rulebook) : base(rulebook)
        {
        }

        private ResistanceData GetArmorImpactResistance(
            Character character, 
            Item item, 
            ArmorComponent armorComponent)
        {
            return new ResistanceData(
                DamageType.Impact,
                armorComponent.ImpactResistance);
        }

        private ResistanceData GetArmorPiercingResistance(
            Character character, 
            Item item, 
            ArmorComponent armorComponent)
        {
            return new ResistanceData(
                DamageType.Piercing, 
                armorComponent.PiercingResistance);
        }

        private ResistanceData GetArmorSlashingResistance(
            Character character, 
            Item item, 
            ArmorComponent armorComponent)
        {
            return new ResistanceData(
                DamageType.Slashing, 
                armorComponent.SlashingResistance);
        }

        internal bool TryGetArmorStats(Character character, Item armorItem, out ComputedArmorStats computedArmorStats)
        {
            computedArmorStats = default;

            if (!armorItem.GetIsArmor()) { return false; }

            computedArmorStats = new ComputedArmorStats(
                GetArmorResistances(character, armorItem)
            );

            return true;
        }

        private List<ResistanceData> GetArmorResistances(
            Character character, 
            Item armorItem)
        {
            return new List<ResistanceData>()
            {
                GetArmorPiercingResistance(character, armorItem, armorItem.Armor),
                GetArmorImpactResistance(character, armorItem, armorItem.Armor),
                GetArmorSlashingResistance(character, armorItem, armorItem.Armor),
            };
        }
    }
}