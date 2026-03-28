using System;
using System.Collections.Generic;
using System.Linq;

public partial class Rulebook
{
    // Excess Pierce Damage results in the bullet going through the character
    // Excess Impact Damage results in the bullet pushing the character
    internal class WeaponsRules : RulebookSection
    {
        public ItemStackAPI ItemStackAPI => Rulebook.GameAPI.Items.ItemStackAPI;

        public WeaponsRules(Rulebook rulebook) : base(rulebook) { }

        internal struct ShotRay
        {
            public Vec2 Origin;
            public Vec2 Direction;
            public GridRaycaster.GridRay GridRay;
        }

        // Ranges
        internal const int CloseRangeGunShotDistance = 3;
        internal const int MidRangeGunShotDistance = 8;
        internal const int LongRangeGunShotDistance = 12;

        /*
            * INTENDED PIPELINE
            RawBulletEnergy = Ammo Energies
            → Modified by Gun
            → Modified by Range
            → Modified by Cover Material
            → Damages Clothing
            → Split into Cyber/Organic
            → Converted to Severity
            */

        internal bool TryShootInDirection(
            Character attacker,
            Item gunItem,
            MapChunk map,
            float directionX,
            float directionY)
        {
            if (!map.TryGetCharacterPlacement(attacker.Id, out var attackerPlacement))
            {
                return false;
            }

            if (!gunItem.GetIsGun())
                return false;

            GunComponent gunComponent = gunItem.Gun;
            if (!gunComponent.TryGetLoadedAmmo(out var ammoItemUid))
                return false;

            if (!Rulebook.GameAPI.Databases.TryGetModel(ammoItemUid, out Item ammoItem))
                return false;

            AmmoComponent ammo = ammoItem.Ammo;

            int bulletsToFire = Mathf.Min(ammoItem.CurrentStackCount, gunComponent.ProjectileCountPerShot);

            if (bulletsToFire == 0)
                return false;

            var damageType = ammoItem.Ammo.DamageType;
            var rangeEffectivenessData = new List<RangeEffectivenessPresentation>
            {
                new RangeEffectivenessPresentation(0, CloseRangeGunShotDistance, gunComponent.CloseRangeEnergyMultiplier),
                new RangeEffectivenessPresentation(CloseRangeGunShotDistance, MidRangeGunShotDistance, gunComponent.MidRangeEnergyMultiplier),
                new RangeEffectivenessPresentation(MidRangeGunShotDistance, LongRangeGunShotDistance, gunComponent.LongRangeEnergyMultiplier)
            };

            float shotRadiusInTiles = gunComponent.ShotRadiusInTiles;

            var shotRays = BuildShotRays(
                attackerPlacement,
                gunComponent,
                bulletsToFire,
                new Vec2(directionX, directionY),
                map);

            List<HitRayPresentation> allHits = new();
            foreach (var shotRay in shotRays)
            {
                var hitsFromCurrentBullet = new List<HitResult>();

                int dealtEnergy = 0;

                // Direction for push / knockback
                CellPosition rayDirection = shotRay.GridRay.EndTile - shotRay.GridRay.StartTile;
                rayDirection = new CellPosition(Math.Sign(rayDirection.X), Math.Sign(rayDirection.Y));

                Vec2 bulletStoppedPos = shotRay.GridRay.EndPos;

                // SHARED HIT COLLECTION
                var lineCastHits = GetHitsForRay(map, shotRay, shotRadiusInTiles);

                LinecastHit firstValidHit = null;
                CombatRules.BulletInstance bulletInstance = null;
                foreach (var hit in lineCastHits)
                {
                    // We shouldn't hit ourself
                    if (hit is CharacterLinecastHit charHit && charHit.PlacementOnGrid.CharacterId == attacker.Id)
                        continue;

                    if (firstValidHit == null)
                    {
                        firstValidHit = hit;
                        bulletInstance = new CombatRules.BulletInstance(
                            ComputeBulletShotEnergyFromDistance(
                                attacker,
                                gunItem,
                                firstValidHit.Distance),
                            ammo.DamageType);
                    }

                    int energyAtRange = ComputeBulletShotEnergyFromDistance(
                                attacker,
                                gunItem,
                                hit.Distance);

                    // We dont want bullets that pierce through to jump in energy or reset energy soo we just clamp
                    bulletInstance.SetEnergy(Mathf.Min(bulletInstance.CurrentEnergy, energyAtRange));

                    HitResult hitResult = null;

                    if (hit is CharacterLinecastHit characterHit)
                    {
                        var targetId = characterHit.PlacementOnGrid.CharacterId;

                        if (!Rulebook.GameAPI.Items.TryResolve(targetId, out Character target))
                            continue;

                        if (Rulebook.CombatSection.IsCharacterDead(target))
                            continue;

                        var bodyPart = Rulebook.CombatSection.GetRandomHitBodyPart(target);

                        if (bodyPart != null)
                        {
                            var charHitResult = Rulebook.CombatSection.HitCharacter(
                                map,
                                hit.HitCell,
                                hit.HitPoint,
                                characterHit.PlacementOnGrid,
                                target,
                                bodyPart,
                                bulletInstance,
                                rayDirection
                            );

                            hitResult = charHitResult;
                        }
                    }
                    else if (hit is PropObjectLinecastHit propHit)
                    {
                        var propId = propHit.PlacementOnGrid.PropId;

                        if (!TryResolve(propId, out Prop prop))
                            continue;

                        if (prop.GetIsDestroyed())
                            continue;

                        var propHitResult = Rulebook.CombatSection.HitProp(
                            map,
                            hit.HitCell,
                            hit.HitPoint,
                            propHit.PlacementOnGrid,
                            prop,
                            bulletInstance,
                            rayDirection
                        );
                        hitResult = propHitResult;
                    }

                    if (hitResult != null)
                    {
                        dealtEnergy +=
                            hitResult.ResistedEnergy +
                            hitResult.TotalEnergyAppliedToCharacter;

                        hitsFromCurrentBullet.Add(hitResult);
                    }

                    // Bullet Stopped
                    if (bulletInstance.CurrentEnergy <= 0)
                    {
                        bulletStoppedPos = shotRay.GridRay.StartPos + shotRay.Direction * hit.Distance;
                        break;
                    }
                }

                allHits.Add(
                    new HitRayPresentation(
                        attackerPlacement.Footprint.X, attackerPlacement.Footprint.Y,
                        shotRay.GridRay.EndTile.X, shotRay.GridRay.EndTile.Y,
                        shotRay.GridRay.StartPos.x, shotRay.GridRay.StartPos.y,
                        bulletStoppedPos.x, bulletStoppedPos.y,
                        shotRay.GridRay.EndPos.x, shotRay.GridRay.EndPos.y,
                        shotRadiusInTiles,
                        rangeEffectivenessData,
                        damageType,
                        hitsFromCurrentBullet));
            }

            TryGetGunStats(attacker, gunItem, out var gunStats);

            var shotEvent = new CombatAPI.Events.ShotWithGunInDirection(
                new Vec2(directionX, directionY), 
                attacker.Id, 
                gunItem.Id, 
                map.Id, 
                allHits, 
                gunStats);

            Rulebook.GameAPI.RaiseEvent(shotEvent); // Maybe only API is allowed to raise

            return true;
        }


        //private void ApplyShootInDirectionResult(CombatAPI.Events.ShotWithGunInDirection shootResult)
        //{
        //    var gunItem = shootResult.GunItem;
        //    if (!gunItem.GetIsGun()) { return; }
        //    if (!gunItem.Gun.TryGetLoadedAmmo(out _)) { return; }

        //    gunItem.Gun.ReduceAmmo(shootResult.HitRays.Count, out _);

        //    foreach (var hitRay in shootResult.HitRays)
        //    {
        //        foreach (var hitResult in hitRay.HitResults)
        //        {
        //            switch (hitResult)
        //            {
        //                case CharacterHitResult c:
        //                    Rulebook.CombatSection.ApplyHitResultOnCharacter(c);
        //                    break;
        //                case PropHitResult wo:
        //                    Rulebook.CombatSection.ApplyHitResultOnProp(wo);
        //                    break;
        //                default:
        //                    throw new NotImplementedException();
        //            }
        //        }
        //    }
        //}

        internal int ComputeBulletShotEnergyFromDistance(
            Character character,
            Item gunItem,
            float distance)
        {
            if (!gunItem.GetIsGun()) { return 0; }
            if (!gunItem.Gun.TryGetLoadedAmmo(out var ammoItemUid)) { return 0; }
            if (!TryResolve(ammoItemUid, out Item ammoItem)) { return 0; }

            return ComputeBulletShotEnergyFromDistance(character, gunItem, ammoItem.Ammo.BaseEnergy, distance);
        }

        internal float GetEnergyMultiplierForWeapon(Item weaponItem, float distance)
        {
            if (weaponItem.GetIsGun())
            {
                return Rulebook.WeaponsSection.GetEnergyMultiplierForGunFromDistance(weaponItem, distance);
            }

            throw new Exception("Weapon type is not supported");
        }

        internal int ComputeBulletShotEnergyFromDistance(
            Character character,
            Item gunItem,
            int currentEnergy,
            float distance)
        {
            var gunComponent = gunItem.Gun;

            float baseEnergy = currentEnergy + GetAdditionalWeaponPower(character, gunItem);
            float effectiveControlMultiplier = GetEffectiveControlPercentForWeapon(character, gunItem);
            float distanceEnergyMultiplier = GetEnergyMultiplierForWeapon(gunItem, distance);

            float finalEnergy =
                baseEnergy
                * distanceEnergyMultiplier
                * effectiveControlMultiplier;

            return (int)finalEnergy;
        }

        // Maximum tile distance at which the gun can deal >0 damage
        private int GetMaxGunRange(GunComponent gun)
        {
            if (gun.LongRangeEnergyMultiplier > 0f) return LongRangeGunShotDistance;
            if (gun.MidRangeEnergyMultiplier > 0f) return MidRangeGunShotDistance;
            if (gun.CloseRangeEnergyMultiplier > 0f) return CloseRangeGunShotDistance;

            return 0;
        }

        internal float GetEnergyMultiplierForGunFromDistance(Item weaponItem, float distance)
        {
            var gunComponent = weaponItem.Gun;

            if (distance <= CloseRangeGunShotDistance)
            {
                return gunComponent.CloseRangeEnergyMultiplier;
            }
            else if (distance <= MidRangeGunShotDistance)
            {
                return gunComponent.MidRangeEnergyMultiplier;
            }
            else if (distance <= LongRangeGunShotDistance)
            {
                return gunComponent.LongRangeEnergyMultiplier;
            }
            return 0;
        }

        internal Vec2 GetCenterPointOfCharacterOnGrid(
            CharacterPlacementOnMap placement)
        {
            int x = placement.Footprint.X;
            int y = placement.Footprint.Y;
            int w = placement.Footprint.Width;
            int h = placement.Footprint.Height;

            return new Vec2(
                x + w * 0.5f,
                y + h * 0.5f
            );
        }

        private List<Vec2> GenerateSpreadDirections(
            Vec2 baseDirection,
            int rayCount,
            float spreadAngleDeg)
        {
            var directions = new List<Vec2>();

            if (rayCount == 1)
            {
                directions.Add(baseDirection.normalized);
                return directions;
            }

            float half = spreadAngleDeg * 0.5f;

            for (int i = 0; i < rayCount; i++)
            {
                float t = (float)i / (rayCount - 1);   // 0..1
                float angle = Mathf.Lerp(-half, half, t);

                directions.Add(
                    Rotate(baseDirection, angle).normalized
                );
            }

            return directions;
        }

        private Vec2 Rotate(Vec2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            return new Vec2(
                v.x * cos - v.y * sin,
                v.x * sin + v.y * cos
            );
        }

        internal List<ShotRay> BuildShotRays(
            CharacterPlacementOnMap attackerPlacement,
            GunComponent gun,
            int rayCount,
            Vec2 baseDirection,
            MapChunk map)
        {
            int maxRange = GetMaxGunRange(gun);
            Vec2 origin = GetCenterPointOfCharacterOnGrid(attackerPlacement);

            var rays = new List<ShotRay>();

            var directions = GenerateSpreadDirections(
                baseDirection,
                rayCount,
                gun.SpreadAngleDeg
            );

            foreach (var dir in directions)
            {
                var gridRay = GridRaycaster.GetRayInDirection(
                    origin,
                    dir,
                    maxRange
                );

                rays.Add(new ShotRay
                {
                    Origin = origin,
                    Direction = dir.normalized,
                    GridRay = gridRay
                });
            }

            return rays;
        }

        internal List<LinecastHit> GetHitsForRay(
            MapChunk map,
            ShotRay ray,
            float shotRadiusInTiles)
        {
            return map.GetPlacementsIntersectedByRay(
                ray.GridRay,
                shotRadiusInTiles
            );
        }

        internal int GetAdditionalWeaponPower(Character character, Item item)
        {
            if (item.GetIsGun())
            {
                return item.Gun.AdditionalShotEnergy;
            }

            return 0;
        }

        // Raw value
        public int GetCharacterControlValueForWeapon(Character character, Item item)
        {
            if (character == null) { return GetWeaponControlRequirement(item); } // May want to hide it?

            if (item.GetIsGun())
            {
                // TODO: Change depending on type of gun
                // For example a heavy rifle should use primary Physique
                // A smart weapon should us primary technical

                //item.Gun.ControlRequirement
                var characterControl =
                    character.Stats.Physique * 0.5f +
                    character.Stats.Reflexes * 1.0f +
                    character.Stats.Technical * 0.5f;

                return (int)characterControl;
            }
            //else if (item.GetIsBlade())
            //{

            //}

            throw new Exception("Not a valid weapon");
        }

        public int GetWeaponControlRequirement(Item item)
        {
            if (item.GetIsGun())
            {
                // TODO: Change depending on type of gun
                // For example a heavy rifle should use primary Physique
                // A smart weapon should use primary technical

                return item.Gun.ControlRequirement;
            }
            //else if (item.GetIsBlade())
            //{

            //}

            throw new Exception("Not a valid weapon");
        }

        // Percentage
        public float GetEffectiveControlPercentForWeapon(Character character, Item item)
        {
            float weaponControlRequirement = GetWeaponControlRequirement(item);
            float characterControlForWeapon = GetCharacterControlValueForWeapon(character, item);
            if (weaponControlRequirement <= 0) { return 1; }
            return Mathf.Clamp01(characterControlForWeapon / weaponControlRequirement);
        }

        // TODO: consider taking into account remaining ammunition?
        private int GetMaxBulletsFiredPerShot(Character character, Item item)
        {
            return item.Gun.ProjectileCountPerShot;
        }

        // Degrees
        private float GetSpreadAngle(Character character, Item item)
        {
            return item.Gun.SpreadAngleDeg;
        }

        // 100% superficial
        private FiringStyle GetFiringStyle(Character character, Item item)
        {
            return item.Gun.FiringStyle;
        }

        internal bool TryGetGunStats(Character characterViewer, Item item, out GunStatsPresentation gunStats)
        {
            gunStats = default;

            if (!item.GetIsGun()) { return false; }
            item.Gun.TryGetLoadedAmmo(out var ammoItemId);
            Rulebook.GameAPI.Databases.TryGetModel(ammoItemId, out Item ammoItem);

            CharacterId characterId = characterViewer != null ? characterViewer.Id : default;

            Rulebook.GameAPI.Databases.EntityLocationAPI.TryFindEntityLocation(item.Id, out var location);

            // HACK
            if (location is AttachedLocation characterLocation)
                characterId = (CharacterId)characterLocation.EntityId;

            Rulebook.GameAPI.Databases.TryGetModel(characterId, out Character character);

            gunStats = new GunStatsPresentation(
                GetWeaponControlRequirement(item),
                GetCharacterControlValueForWeapon(character, item),
                GetEffectiveControlPercentForWeapon(character, item),
                GetAdditionalWeaponPower(character, item),
                GetMaxBulletsFiredPerShot(character, item),
                GetFiringStyle(character, item),
                GetSpreadAngle(character, item),
                GetEnergyMultiplierForGunFromDistance(item, CloseRangeGunShotDistance),
                ComputeBulletShotEnergyFromDistance(character, item, CloseRangeGunShotDistance),
                GetEnergyMultiplierForGunFromDistance(item, MidRangeGunShotDistance),
                ComputeBulletShotEnergyFromDistance(character, item, MidRangeGunShotDistance),
                GetEnergyMultiplierForGunFromDistance(item, LongRangeGunShotDistance),
                ComputeBulletShotEnergyFromDistance(character, item, LongRangeGunShotDistance),
                ammoItem != null ? ammoItem.CurrentStackCount : 0,
                item.Gun.ClipSize,
                ammoItem != null ? ammoItem.Ammo.DamageType : DamageType.None,
                new LoadedAmmoPresentation(Rulebook.GameAPI, characterId, ammoItemId)
                );

            return true;
        }
    }
}