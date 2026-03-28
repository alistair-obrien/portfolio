using System;
using System.Linq;

public interface IHealthRenderer : IRenderer<AllHealthDomainsPresentation>
{

}

public interface IPlayerLoadoutQueries : ICoordinatorQueries
{

}

public interface IPlayerLoadoutCommands : ICoordinatorCommands
{
    void DoPrimaryActionOnWeapon();
    void DoSecondaryActionOnWeapon();
    void PrimaryWeaponHoverEnter();
    void PrimaryWeaponHoverExit();
}

public interface ILoadoutRenderer : IRenderer<CharacterLoadoutPresentation>
{
    void Show();
    void Hide();
    void PresentOptions(InteractionOptionsPresentation interactions);
    IInventoryRenderer InventoryRenderer { get; }

    void BindCommands(IPlayerLoadoutCommands commands);
    void UnbindCommands(IPlayerLoadoutCommands commands);
}

public class CharacterLoadoutCoordinator : CoordinatorBase<
    IPlayerLoadoutQueries, 
    IPlayerLoadoutCommands, 
    ILoadoutRenderer>, IPlayerLoadoutCommands, IPlayerLoadoutQueries
{
    public override IPlayerLoadoutQueries QueriesHandler => this;
    public override IPlayerLoadoutCommands CommandsHandler => this;

    private CharacterId? _boundCharacterID;
    private CharacterLoadoutPresentation _presentation;

    private InventoryCoordinator _inventoryCoordinator;

    public CharacterLoadoutCoordinator(GameInstance gameAPI) : base(gameAPI) 
    {
        AddSubCoordinator(_inventoryCoordinator = new InventoryCoordinator(_gameInstance));
    }

    protected override void OnRendererBound(ILoadoutRenderer renderer)
    {
        _inventoryCoordinator.BindRenderer(renderer.InventoryRenderer);
        renderer.BindCommands(this);
        renderer?.Sync(_presentation);
    }

    protected override void OnRendererUnbound(ILoadoutRenderer renderer)
    {
        renderer.UnbindCommands(this);
        _inventoryCoordinator.UnbindRenderer(renderer.InventoryRenderer);
    }

    public void BindCharacter(CharacterId? characterId)
    {
        _boundCharacterID = characterId;

        if (characterId.HasValue)
        {
            _gameInstance.Databases.TryResolve(characterId.Value, out Character character); 
            _presentation = new CharacterLoadoutPresentation(_gameInstance, character);
        }
        else
        {
            _presentation = null;
        }

        ForEachRenderer(r => r.Sync(_presentation));

        _inventoryCoordinator.BindItem(_presentation.InventoryId);
        _inventoryCoordinator.BindCharacterInteractor(characterId); // Not sure. Interactor and bound character can be different
    }

    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        switch (evt)
        {
            case CharactersAPI.Events.CharacterLoadoutUpdated loadoutUpdated:
                if (loadoutUpdated.CharacterId != _boundCharacterID)
                    return;

                var oldPresentation = _presentation;
                _presentation = loadoutUpdated.CharacterLoadoutPresentation;

                if (oldPresentation.InventoryId != _presentation.InventoryId)
                    _inventoryCoordinator.BindItem(_presentation.InventoryId.Value);
            break;
        }
    }

    public void DoPrimaryActionOnWeapon()
    {
        var location = new AttachedLocation(_boundCharacterID.Value, SlotIds.Loadout.PrimaryWeapon);
        SubmitIntent(new Intents.GameEntityLocationPrimaryCommit(location));
    }

    public void DoSecondaryActionOnWeapon()
    {
        var location = new AttachedLocation(_boundCharacterID.Value, SlotIds.Loadout.PrimaryWeapon);
        SubmitIntent(new Intents.GameEntityLocationSecondaryCommit(location));
    }

    public void PrimaryWeaponHoverEnter()
    {
        var location = new AttachedLocation(_boundCharacterID.Value, SlotIds.Loadout.PrimaryWeapon);
        SubmitIntent(new Intents.GameEntityLocationHoverEnterIntent(location));
    }

    public void PrimaryWeaponHoverExit()
    {
        var location = new AttachedLocation(_boundCharacterID.Value, SlotIds.Loadout.PrimaryWeapon);
        SubmitIntent(new Intents.GameEntityLocationHoverExitIntent(location));
    }
}