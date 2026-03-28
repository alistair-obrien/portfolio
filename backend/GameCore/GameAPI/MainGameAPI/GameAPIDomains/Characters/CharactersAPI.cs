using System;

public sealed partial class CharactersAPI : APIDomain
{
    public CharactersAPI(GameInstance gameAPI) : base(gameAPI) { }

    public CharacterId? PlayerCharacterId => RootModel.PlayerCharacterId;

    internal override void RegisterHandlers(CommandRouter router)
    {
        router.Register<Commands.AssignPlayerCharacter>(TryAssignPlayerCharacter);
    }

    internal bool TryResolve(CharacterId id, out Character character)
    {
        return GameAPI.Databases.TryGetModel(id, out character);
    }

    private CommandResult TryAssignPlayerCharacter(Commands.AssignPlayerCharacter command)
    {
        if (RootModel.PlayerCharacterId == command.CharacterUid)
            return CommandResult.Fail($"Player is already assigned as {command.CharacterUid}");

        //if (!Rulebook.CharacterCanBeAssignedToPlayer(character))
        //    return false;

        var oldUid = RootModel.PlayerCharacterId;
        RootModel.AssignPlayerCharacter(command.CharacterUid);

        PlayerInteractionPresentation playerInteractionPresentation = null;
        if (command.CharacterUid != null)
        {
            if (TryResolve(command.CharacterUid.Value, out Character character))
                playerInteractionPresentation = new PlayerInteractionPresentation(character);
        }

        RaiseEvent(new Events.PlayerCharacterAssigned(
            oldUid, 
            command.CharacterUid,
            playerInteractionPresentation));

        return CommandResult.Success();
    }

    public CommandResult TryLootCharacter(LootCharacterRequest request)
    {
        throw new NotImplementedException();
    }

    //public bool TryInspectCharacterSheet(InspectCharacterRequest request)
    //{
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.SelfUid, out var self)) { return false; }
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.TargetUid, out var target)) { return false; }

    //    GameAPI.RaiseEvent(new InspectedCharacter(target));
    //    return true;
    //}

    //public bool TryInspectCharacterHealthReport(
    //    InspectCharacterHealthReportRequest request)
    //{
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.SelfUid, out var self)) { return false; }
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.TargetUid, out var target)) { return false; }

    //    GameAPI.RaiseEvent(new InspectedCharacterHealthReport(target));
    //    return true;

    //}

    //public bool TryTalk(TalkRequest request)
    //{
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.SelfUid, out var self)) { return false; }
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.TargetUid, out var target)) { return false; }
    //    if (string.IsNullOrWhiteSpace(request.DialogueNode)) { return false; }
    //    if (!Rulebook.InteractionSection.CanTalkToCharacter(self, target)) { return false; }

    //    GameAPI.RaiseEvent(new TalkedToCharacter(
    //            self.Name,
    //            target.Name,
    //            request.DialogueNode)
    //        );

    //    return true;
    //}

    //public bool TryTreatInjury(TreatInjuryRequest request)
    //{
    //    if (!TryResolve(request.SelfUid, out Character self)) { return false; }
    //    if (!TryResolve(request.TargetUid, out Character target)) { return false; }

    //    if (!target.Anatomy.TryResolveSlot(request.BodySlotPath, out var bodyPart))
    //    {
    //        return false;
    //    }

    //    if (!bodyPart.TryGetInjuryFromIndex(
    //        request.InjuryIndex,
    //        out var injury))
    //    {
    //        return false;
    //    }

    //    // Decide the rules of treatment
    //    return Rulebook.InjuriesAndDamageSection.TryTreatInjury(
    //        self,
    //        target,
    //        bodyPart,
    //        injury);
    //}

    //public bool TrySetCharacterDialogue(SetCharacterDialogueRequest request)
    //{
    //    if (!GameAPI.Databases.TryGetModel<Character>(request.CharacterUid, out var self)) { return false; }

    //    self.SetDialogueNode(request.DialogueNode);

    //    // Replace ALL deep clones with proper presentations
    //    GameAPI.RaiseEvent(new CharacterDialogueSet(
    //        self,
    //        request.DialogueNode)
    //    );

    //    return true;
    //}

    internal bool TryGetPlayerCharacter(out CharacterId? playerId)
    {
        playerId = RootModel.PlayerCharacterId;
        return playerId != null;
    }

    //public bool TryGetCharacterDTO(string characterUid, out CharacterDTO characterDTO)
    //{
    //    characterDTO = default;
    //    if (!GameModel.TryResolveModel<Character>(characterUid, out var character)) { return false; }

    //    ComputedGunStats equippedGunDTO = default;
    //    ComputedArmorStats equippedArmorDTO = default;

    //    if (character.HasWeapon())
    //    {
    //        GameAPI.Items.TryGetComputedGunStats(characterUid, character.GetWeapon().Uid, out equippedGunDTO);
    //    }

    //    if (character.HasArmor())
    //    {
    //        GameAPI.Items.TryGetComputedArmorStats(characterUid, character.GetArmor().Uid, out equippedArmorDTO);
    //    }

    //    characterDTO = new CharacterDTO(
    //        character.DeepClone(),
    //        equippedGunDTO,
    //        equippedArmorDTO);

    //    //_gameModel.TryGetCharacterDTO(character, out characterDTO);

    //    return true;
    //}

    internal bool TryFindCharacterLocation(
        CharacterId characterId,
        out ICharacterLocation characterLocation)
    {
        characterLocation = null;

        if (!TryResolve(characterId, out Character character)) 
            return false;

        characterLocation = character.AttachedLocation;
        return true;
    }

    //internal bool TryFindCharacterLocation(
    //    Character character, 
    //    out LocalMap foundMap, 
    //    out CharacterPlacementOnMap foundPlacement)
    //{
    //    foundMap = default;
    //    foundPlacement = default;

    //    foreach (var map in GameModel.GetAllLoadedMaps())
    //    {
    //        if (map.TryGetCharacterPlacement(character, out var placement))
    //        {
    //            foundMap = map;
    //            foundPlacement = placement;
    //            return true;
    //        }
    //    }

    //    return false;
    //}

    //internal bool EquipItem(Commands.EquipItem command)
    //{
    //    if (!GameAPI.Items.ItemMoverAPI.TryMoveItem(
    //        new ItemsAPI.Commands.MoveItem(
    //            command.CharacterUid,
    //            command.ItemUid,
    //            new EquippedLocation(command.CharacterUid, command.SlotId), false)))
    //        return false;

    //    if (!GameAPI.Databases.TryGetModel<Item>(command.ItemUid, out var item))
    //        return false;

    //    GameAPI.RaiseEvent(new Events.ItemEquipped(
    //        command.ItemUid,
    //        command.CharacterUid,
    //        command.SlotId,
    //        new ItemPresentation(GameAPI, item)
    //        ));

    //    return true;
    //}

    internal bool TryGetWeapon(CharacterId characterId, out ItemId weaponId)
    {
        weaponId = default;

        if (!TryResolve(characterId, out var character))
            return false;

        var weapon = character.GetWeapon();
        if (weapon == null)
        {
            return false;
        }

        if (!GameAPI.Databases.TryGetModel(weapon.Value, out Item item))
            return false;

        if (!item.IsCompatibleWithSlot(SlotIds.Loadout.PrimaryWeapon) || !item.GetIsGun())
            return false;

        weaponId = weapon.Value;
        return true;
    }
}
