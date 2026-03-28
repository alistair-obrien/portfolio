public interface IInventoryQueries : ICoordinatorQueries
{
}

public interface IInventoryCommands : ICoordinatorCommands
{
    void CellHoverEnter(CellPosition cellPosition);
    void CellHoverExit(CellPosition cellPosition);
    void CellPrimaryAction(CellPosition cellPosition);
    void CellSecondaryAction(CellPosition cellPosition);
    void EntityPrimaryAction(IGameDbId entityId);
}

public interface IInventoryRenderer : IRenderer<InventoryPresentation>
{
}

public class InventoryCoordinator : CoordinatorBase<
    IInventoryQueries,
    IInventoryCommands,
    IInventoryRenderer>, 
    IInventoryCommands, 
    IInventoryQueries
{
    public override IInventoryQueries QueriesHandler => this;
    public override IInventoryCommands CommandsHandler => this;

    private InventoryPresentation _presentation;

    private ItemId? _inventoryitemId;
    private CharacterId? _boundCharacterInteractor;

    public InventoryCoordinator(GameInstance gameAPI) : base(gameAPI) { }

    public void BindItem(ItemId? inventoryitemId)
    {
        _inventoryitemId = inventoryitemId;

        PullPresentation();
        ForEachRenderer(x => x.Sync(_presentation));
    }

    public void BindCharacterInteractor(CharacterId? characterId)
    {
        _boundCharacterInteractor = characterId;

        PullPresentation();
        ForEachRenderer(x => x.Sync(_presentation));
    }

    protected override void OnRendererBound(IInventoryRenderer renderer)
    {
        renderer.Sync(_presentation);
    }

    private void PullPresentation()
    {
        if (_inventoryitemId == null)
        {
            _presentation = null;
            return;
        }

        _presentation = new InventoryPresentation(_gameInstance, _inventoryitemId);
    }

    public void CellHoverEnter(CellPosition cellPosition)
    {
        var location = new InventoryLocation(_inventoryitemId.Value, cellPosition);
        SubmitIntent(new Intents.GameEntityLocationHoverEnterIntent(location));
    }

    public void CellHoverExit(CellPosition cellPosition)
    {
        var location = new InventoryLocation(_inventoryitemId.Value, cellPosition);
        SubmitIntent(new Intents.GameEntityLocationHoverExitIntent(location));
    }

    public void CellPrimaryAction(CellPosition cellPosition)
    {
        var location = new InventoryLocation(_inventoryitemId.Value, cellPosition);
        SubmitIntent(new Intents.GameEntityLocationPrimaryCommit(location));
    }

    public void EntityPrimaryAction(IGameDbId entityId)
    {
        SubmitIntent(new Intents.GameEntityPrimaryCommit(entityId));
    }

    public void CellSecondaryAction(CellPosition cellPosition)
    {
        var location = new InventoryLocation(_inventoryitemId.Value, cellPosition);
        SubmitIntent(new Intents.GameEntityLocationSecondaryCommit(location));
    }
}
