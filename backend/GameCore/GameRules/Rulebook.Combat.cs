using System.Collections.Generic;
using System.Linq;

public partial class Rulebook
{
    internal class CombatRules : RulebookSection
    {
        public CombatRules(Rulebook rulebook) : base(rulebook)
        {
        }

        internal OrganicBodyPartComponent GetRandomHitBodyPart(Character target)
        {
            List<OrganicBodyPartComponent> aliveBodyParts = new();

            foreach (var kvp in target.Anatomy.GetAllBodyParts())
            {
                var bodySlotPath = kvp.Key;
                var bodyPart = kvp.Value;

                // Can't randomly hit head
                if (bodySlotPath == SlotIds.Organic.Head) { continue; }

                if (bodyPart.GetRemainingInjuryCapacity() > 0)
                {
                    aliveBodyParts.Add(bodyPart);
                }
            }

            if (aliveBodyParts.Count == 0)
                return null;

            int index = Random.Range(0, aliveBodyParts.Count);
            return aliveBodyParts[index];
        }

        internal PropLayerHitResult HitPropLayer(
            MapChunk mapChunk,
            Prop prop,
            PropLayer layer,
            BulletInstance bulletInstance)
        {
            var structuralDamageInstances = new Dictionary<string, DamageInstance>();
            var cyberneticDamageInstances = new Dictionary<string, DamageInstance>();

            int energyAppliedToObject = 0;
            int energyConsumedByDamage = 0;
            int leftoverEnergy = 0;

            bool didHit = false;
            CombatAPI.Events.PropLayerDamagedEvent propDamagedEvent = null;
            if (!layer.GetIsDestroyed())
            {
                // --- DAMAGE ---
                // Severity implied by incoming energy
                int damageSeverity = Rulebook.InjuriesAndDamageSection.GetSeverityFromEnergy(bulletInstance.CurrentEnergy);

                // Consume energy based on the clamped severity
                int energyConsumed = Rulebook.InjuriesAndDamageSection.ComputeMaximumEnergyForSeverity(damageSeverity);

                int receivedPower = Mathf.Min(bulletInstance.CurrentEnergy, energyConsumed);

                bulletInstance.ConsumeEnergy(energyConsumed);

                didHit = Rulebook.InjuriesAndDamageSection.TryDamagePropLayer(
                    mapChunk, 
                    prop, 
                    layer, 
                    bulletInstance.DamageType, 
                    damageSeverity,
                    out propDamagedEvent);
            }

            return new PropLayerHitResult(
                didHit, 
                structuralDamageInstances, 
                cyberneticDamageInstances,
                energyAppliedToObject,
                energyConsumedByDamage,
                leftoverEnergy,
                layer.Id,
                layer,
                propDamagedEvent);
        }

        public class BulletInstance
        {
            public BulletInstance(int initialEnergy, DamageType damageType)
            {
                CurrentEnergy = initialEnergy;
                DamageType = damageType;
            }

            public int CurrentEnergy { get; private set; }
            public DamageType DamageType { get; }

            internal void ConsumeEnergy(int energyConsumed)
            {
                SetEnergy(CurrentEnergy - energyConsumed);
            }

            internal void SetEnergy(int newEnergyValue)
            {
                CurrentEnergy = Mathf.Max(0, newEnergyValue);
            }
        }

        internal PropHitResult HitProp(
            MapChunk mapChunk,
            CellPosition hitCell,
            Vec2 hitPoint,
            PropPlacementOnMap propPlacement,
            Prop target,
            BulletInstance bulletInstance,
            CellPosition direction)
        {
            List<PropLayerHitResult> layerHitResults = new();

            bool didHit = false;
            // Keep going until energy is zero
            foreach (var componentLayer in target.PropLayers)
            {
                if (componentLayer.GetIsDestroyed())
                    continue;

                var hit = HitPropLayer(
                    mapChunk,
                    target,
                    componentLayer, 
                    bulletInstance);

                if (hit.DidHit)
                    didHit = true;

                layerHitResults.Add(hit);

                if (bulletInstance.CurrentEnergy <= 0) { break; }
            }

            int energyAppliedToObject = layerHitResults.Sum(x => x.EnergyAppliedToObject);
            int energyConsumedByDamage = layerHitResults.Sum(x => x.EnergyConsumedByDamage);
            int leftoverEnergy = bulletInstance.CurrentEnergy;

            // --- IMPACT PUSH ---
            // PUSH
            MoveResult pushResult = default; // TODO: Remove this
            if (bulletInstance.DamageType == DamageType.Impact && bulletInstance.CurrentEnergy > 0)
            {
                int pushTiles = Rulebook.MovementSection.GetPushTilesFromEnergy(bulletInstance.CurrentEnergy);

                for (int i = 0; i < pushTiles; i++)
                {
                    Rulebook.GameAPI.Maps.TryMovePropOneStepRelative(
                        new MapsAPI.Commands.MovePropOneStepRelative(
                            Root.SYSTEM_ID,
                            propPlacement.PropId, 
                            direction.X, 
                            direction.Y));
                }

                bulletInstance.SetEnergy(0); // No further energy propagation for impact
            }

            //bool wasDestroyed = target.IsDestroyed(); // evaluated pre-apply, consistent with current pipeline

            return new PropHitResult(
                mapChunk.Id,
                target.Id,
                target,
                direction.X,
                direction.Y,
                hitCell.X,
                hitCell.Y,
                hitPoint.x,
                hitPoint.y,

                layerHitResults,

                energyAppliedToObject,
                energyConsumedByDamage,
                leftoverEnergy,
                didHit,
                pushResult);
        }

        // Damages clothing
        // Hits Cyber OR Organic Part
        internal CharacterHitResult HitCharacter(
            MapChunk map,
            CellPosition hitCell,
            Vec2 hitPoint,
            CharacterPlacementOnMap characterPlacementOnGrid,
            Character target,
            OrganicBodyPartComponent organicBodyPart,
            BulletInstance bulletInstance,
            CellPosition direction)
        {
            int initialEnergy = bulletInstance.CurrentEnergy;

            // DAMAGE INSTANCES
            var clothingDamageInstances = new Dictionary<ItemId, DamageInstance>();
            var modFrameDamageInstances = new Dictionary<ItemId, DamageInstance>();
            var bodyPartInjuries = new Dictionary<(CharacterId CharacterUid, AnatomySlotId BodySlotPath), DamageInstance>();

            // PUSH
            MoveResult pushResult = default; // REMOVE THIS

            // Map for items affected
            Dictionary<ItemId, Item> itemSnapshots = new Dictionary<ItemId, Item>();

            //TODO: Pass slot
            // --- Clothing Damage ---
            // Clothing provides no ballistic absorption, only durability loss
            // Clothing stops at 1 condition. They don't disappear, they just become debuffed
            // Clothing condition does not matter in combat right now as its used for social mechanics, not damage mitigation
            //foreach (var clothingItemUid in target.Style.GetForBodyPart(organicBodyPart))
            //{
            //    if (!Rulebook.GameAPI.Databases.TryGetModel(clothingItemUid, out Item clothingItem)) { continue; }
            //    if (!clothingItem.GetIsClothing()) { continue; }
            //    if (!RollClothingIsHitByShot(clothingItem.Clothing)) { continue; }

            //    itemSnapshots.TryAdd(clothingItem.Id, clothingItem);

            //    var clothingDamage = Rulebook.InjuriesAndDamageSection.GetSeverityFromEnergy(bulletInstance.CurrentEnergy);

            //    var clothingDamageInstance = Rulebook.InjuriesAndDamageSection.CreateClothingDamage(clothingItem, bulletInstance.DamageType, clothingDamage);

            //    clothingDamageInstances.Add(clothingItem.Id, clothingDamageInstance);
            //}

            // Now it hits the character

            // --- Armor Resistance ---
            int armorResistance = 0;

            var targetArmorItemId = target.GetArmor();
            if (targetArmorItemId.HasValue)
            {
                if (Rulebook.GameAPI.Databases.TryGetModel(targetArmorItemId.Value, out Item armor))
                {
                    armorResistance = Rulebook.StatsSection.GetEnergyAbsorptionFromArmorItem(armor, bulletInstance.DamageType);
                    bulletInstance.ConsumeEnergy(armorResistance);
                }
            }

            // --- Physical Resistance ---
            int bodyResistance = Rulebook.StatsSection.GetEnergyAbsorptionFromCharacterBody(target, bulletInstance.DamageType);
            bulletInstance.ConsumeEnergy(bodyResistance);

            // --- Absorbed values ---
            int totalResistedEnergy = Mathf.Clamp(
                armorResistance + bodyResistance,
                0,
                initialEnergy
            );

            // CHOOSE MOD FRAME OR BODY PART. Only one will be hit
            // --- Determine cyber coverage ---
            //float organicCoverage = Rulebook.StatsSection.GetOrganicCoveragePercent(
            //    target, 
            //    organicBodyPart.OrganicBodySlotPath);
            float organicCoverage = 1; //HACK
            //TODO: Reolve better


            float cyberCoverage = Mathf.Clamp01(1f - organicCoverage);

            bool hitCyber;

            var modFramesForBodyPart = Rulebook.StatsSection.GetAliveModFramesForBodyPart(
                target, 
                organicBodyPart);

            if (modFramesForBodyPart.Count == 0)
            {
                hitCyber = false;
            }
            else if (organicCoverage <= 0f)
            {
                hitCyber = true;
            }
            else if (cyberCoverage <= 0f)
            {
                hitCyber = false;
            }
            else
            {
                hitCyber = Random.value < cyberCoverage;
            }

            // CYBER HIT
            if (hitCyber)
            {
                // Pick ONE cyber frame to be hit
                Item hitFrame = modFramesForBodyPart[Random.Range(0, modFramesForBodyPart.Count)];

                int cyberSeverity = Rulebook.InjuriesAndDamageSection.GetSeverityFromEnergy(bulletInstance.CurrentEnergy);
                bulletInstance.ConsumeEnergy(Rulebook.InjuriesAndDamageSection.ComputeMaximumEnergyForSeverity(cyberSeverity));

                itemSnapshots.TryAdd(hitFrame.Id, hitFrame);

                var modFrameDamageInstance = Rulebook.InjuriesAndDamageSection.CreateCyberDamage(bulletInstance.DamageType, cyberSeverity);
                modFrameDamageInstances.Add(hitFrame.Id, modFrameDamageInstance);
            }
            // ORGANIC HIT
            else
            {
                int organicSeverity = Rulebook.InjuriesAndDamageSection.GetSeverityFromEnergy(bulletInstance.CurrentEnergy);
                bulletInstance.ConsumeEnergy(Rulebook.InjuriesAndDamageSection.ComputeMaximumEnergyForSeverity(organicSeverity));

                var bodyPartDamageIInstance = Rulebook.InjuriesAndDamageSection.CreateOrganicDamage(bulletInstance.DamageType, organicSeverity);
                //bodyPartInjuries.Add((target.Id, organicBodyPart.OrganicBodySlotPath), bodyPartDamageIInstance); //TODO: Slot
            }

            if (bulletInstance.DamageType == DamageType.Impact && bulletInstance.CurrentEnergy > 0)
            {
                int pushTiles = Rulebook.MovementSection.GetPushTilesFromEnergy(bulletInstance.CurrentEnergy);

                for (int i = 0; i < pushTiles; i++)
                {
                    Rulebook.GameAPI.Maps.TryMoveCharacterOneStepRelative(
                        new MapsAPI.Commands.MoveCharacterOneStepRelative(
                            Root.SYSTEM_ID,
                            characterPlacementOnGrid.CharacterId,
                            direction.X,
                            direction.Y));
                }

                bulletInstance.SetEnergy(0); // We dont want any more energy to persist in the impact damage branch
            }

            int totalEnergyAppliedToCharacter = initialEnergy - totalResistedEnergy - bulletInstance.CurrentEnergy;
            totalEnergyAppliedToCharacter = Mathf.Max(0, totalEnergyAppliedToCharacter);

            bool didHit = bodyPartInjuries.Count < 0 || modFrameDamageInstances.Count > 0;

            return new CharacterHitResult(
                map.Id,
                direction.X,
                direction.Y,
                hitCell.X,
                hitCell.Y,
                hitPoint.x,
                hitPoint.y,
                target.Id,
                target,

                didHit,

                itemSnapshots,

                clothingDamageInstances,
                modFrameDamageInstances,
                bodyPartInjuries,
                    
                totalResistedEnergy,
                totalEnergyAppliedToCharacter,
                bulletInstance.CurrentEnergy,
                    
                pushResult);
        }

        private bool RollClothingIsHitByShot(ClothingComponent clothingComponent)
        {
            return Random.Range(0f, 1f) <= clothingComponent.SurfaceArea;
        }

        internal bool TryAttackInDirection(
            Character attacker, 
            Item weaponItem, 
            MapChunk map,
            float directionX, 
            float directionY)
        {
            // Gun Entry Point
            if (weaponItem.GetIsGun())
            {
                return Rulebook.WeaponsSection.TryShootInDirection(
                    attacker,
                    weaponItem,
                    map,
                    directionX,
                    directionY);
            }
            // TODO: Other weapon types
            else
            {

            }

            return false;
        }

        internal bool IsCharacterDead(Character character)
        {
            if (!character.Anatomy.TryResolveSlot(SlotIds.Organic.Head, out var headSlot)) { return false; }
            if (!character.Anatomy.TryResolveSlot(SlotIds.Organic.Torso, out var torsoSlot)) { return false; }

            if (torsoSlot.GetRemainingInjuryCapacity() <= 0) { return true; }
            if (headSlot.GetRemainingInjuryCapacity() <= 0) { return true; }

            return false;
        }

        internal bool CanAttackCharacterWithPrimaryWeapon(Character self, Character target)
        {
            if (IsCharacterDead(self)) { return false; }
            if (IsCharacterDead(target)) { return false; }
            if (!self.HasWeapon()) { return false; }
            if (!Rulebook.GameAPI.Databases.TryGetModel(self.GetWeapon().Value, out Item weaponItem)) { return false; }
            if (!weaponItem.IsCompatibleWithSlot(SlotIds.Loadout.PrimaryWeapon) || !weaponItem.GetIsGun()) { return false; }

            return true;
        }
    }
}
