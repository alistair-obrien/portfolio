public sealed partial class CombatAPI : APIDomain
{

    public CombatAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        router.Register<Commands.AttackInDirection>(TryAttackInDirection);
        router.Register<Commands.AttackTarget>(TryAttackCharacter);
    }

    private bool CanActorAttack(CharacterId actorId)
    {
        return GameAPI.OperatingSystems.HasGrant(
            actorId,
            OperatingSystemGrantIds.Interactions.Attack,
            OperatingSystemAccessLevel.Use);
    }

    private CommandResult TryAttackInDirection(Commands.AttackInDirection request)
    {
        if (!CanActorAttack(request.AttackerId))
            return Fail($"Actor {request.AttackerId} does not have permission to attack.");

        if (!TryResolve(request.AttackerId, out Character attacker))
            return Fail($"Could not resolve attacker {request.AttackerId}.");

        if (attacker.AttachedLocation is not MapLocation mapLocation)
            return Fail($"Attacker {request.AttackerId} is not on a map.");

        //if (!attacker.TryGetItemInSlot(request.EquipmentSlotId, out ItemId weaponItemId))
        //    return false;

        if (!TryResolve(request.WeaponItemId, out Item weaponItem))
            return Fail($"Could not resolve weapon {request.WeaponItemId}.");


        if (!TryResolve(mapLocation.MapId, out MapChunk map))
            return Fail($"Could not resolve map {mapLocation.MapId} for attacker {request.AttackerId}.");

        var attackerPlacement = map.FindCharacterLocation(request.AttackerId);
        if (attackerPlacement is not MapLocation attackerWorldLocation)
            return Fail($"Could not find attacker {request.AttackerId} on map {map.Id}.");

        if (!Rulebook.CombatSection.TryAttackInDirection(
                attacker,
                weaponItem,
                map,
                request.directionX,
                request.directionY))
        {
            return Fail($"Attack in direction ({request.directionX}, {request.directionY}) failed for attacker {request.AttackerId} using weapon {request.WeaponItemId}.");
        }

        return Ok();
    }

    private CommandResult TryAttackCharacter(Commands.AttackTarget attackRequest)
    {
        if (!CanActorAttack(attackRequest.AttackerId))
            return Fail($"Actor {attackRequest.AttackerId} does not have permission to attack.");

        // --- Resolve entities ---
        if (!GameAPI.Databases.TryGetModel(attackRequest.AttackerId, out Character attacker))
            return Fail($"Could not resolve attacker {attackRequest.AttackerId}.");

        if (attacker.AttachedLocation is not MapLocation mapLocation)
            return Fail($"Attacker {attackRequest.AttackerId} is not on a map.");

        // --- Resolve entities ---
        if (!GameAPI.Databases.TryGetModel(attackRequest.TargetId, out Character target))
            return Fail($"Could not resolve target {attackRequest.TargetId}.");

        if (Rulebook.CombatSection.IsCharacterDead(target))
            return Fail($"Target {attackRequest.TargetId} is already dead.");

        if (!TryResolve(mapLocation.MapId, out MapChunk map))
            return Fail($"Could not resolve map {mapLocation.MapId} for attacker {attackRequest.AttackerId}.");

        var attackerPlacement = map.FindCharacterLocation(attackRequest.AttackerId);
        if (attackerPlacement is not MapLocation attackerWorldLocation)
            return Fail($"Could not find attacker {attackRequest.AttackerId} on map {map.Id}.");

        var targetPlacement = map.FindCharacterLocation(attackRequest.TargetId);
        if (targetPlacement is not MapLocation targetWorldLocation)
            return Fail($"Could not find target {attackRequest.TargetId} on map {map.Id}.");

        Vec2 direction =
            new Vec2(
                targetWorldLocation.CellFootprint.X,
                targetWorldLocation.CellFootprint.Y) -
                new Vec2(
                    attackerWorldLocation.CellFootprint.X,
                    attackerWorldLocation.CellFootprint.Y);



        return TryAttackInDirection(new Commands.AttackInDirection(
            attackRequest.AttackerId,
            attackRequest.WeaponItemId,
            direction.x,
            direction.y));
    }

    public bool GetComputedShotPowerClose(CharacterId characterId, ItemId gunId, out int shotPower)
    {
        return GetComputedShotPower(characterId, gunId, Rulebook.WeaponsRules.CloseRangeGunShotDistance, out shotPower);
    }

    public bool GetComputedShotPowerMid(CharacterId characterId, ItemId gunId, out int shotPower)
    {
        return GetComputedShotPower(characterId, gunId, Rulebook.WeaponsRules.MidRangeGunShotDistance, out shotPower);
    }

    public bool GetComputedShotPowerLong(CharacterId characterId, ItemId gunId, out int shotPower)
    {
        return GetComputedShotPower(characterId, gunId, Rulebook.WeaponsRules.LongRangeGunShotDistance, out shotPower);
    }

    public bool GetComputedShotPower(CharacterId characterId, ItemId gunId, int distance, out int shotPower)
    {
        shotPower = default;

        if (!TryResolve(gunId, out Item gunItem)) { return false; }
        if (!TryResolve(characterId, out Character character)) { return false; }
        if (!gunItem.GetIsGun()) { return false; }
        if (!gunItem.Gun.TryGetLoadedAmmo(out _)) { return false; }

        shotPower = Rulebook.WeaponsSection.ComputeBulletShotEnergyFromDistance(
            character,
            gunItem,
            distance);

        return true;
    }
}
