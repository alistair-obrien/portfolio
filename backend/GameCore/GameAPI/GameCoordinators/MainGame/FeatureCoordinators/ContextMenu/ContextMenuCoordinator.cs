using System;
using System.Collections.Generic;

public interface IContextMenuQueries : ICoordinatorQueries
{

}

public interface IContextMenuCommands : ICoordinatorCommands
{
    void SelectOption(string requestId);
    void Cancel();
}

public interface IContextMenuRenderer : IRenderer
{
    void Open(InteractionOptionsPresentation viewData);
    void Close();
}

public class ContextMenuCoordinator : CoordinatorBase<
    IContextMenuQueries,
    IContextMenuCommands,
    IContextMenuRenderer>, IContextMenuCommands, IContextMenuQueries
{
    private CharacterId _characterOwnerId;

    private Dictionary<string, InteractionRequest> _activeOptions;

    public override IContextMenuQueries QueriesHandler => this;
    public override IContextMenuCommands CommandsHandler => this;

    public ContextMenuCoordinator(GameInstance gameAPI) : base(gameAPI)
    {
    }

    public void SetCharacterOwner(CharacterId characterOwnerUid)
    {
        _characterOwnerId = characterOwnerUid;
    }

    public void Open(string title, IEnumerable<InteractionRequest> options)
    {
        if (_activeOptions != null)
        {
            Debug.LogWarning("Already Opened");
            Cancel();
        }

        _activeOptions = new Dictionary<string, InteractionRequest>();
        List<InteractionOptionPresentation> optionsViewData = new();

        foreach (var option in options)
        {
            string id = Guid.NewGuid().ToString();
            _activeOptions.Add(id, option);
            optionsViewData.Add(new InteractionOptionPresentation(id, option.Name));
        }

        var viewData = new InteractionOptionsPresentation(title, optionsViewData);

        ForEachRenderer(r => r.Open(viewData));
    }

    // Input
    public void Cancel()
    {
        _activeOptions = null;
        ForEachRenderer(r => r.Close());
    }

    public void SelectOption(string id)
    {
        if (_activeOptions == null || !_activeOptions.TryGetValue(id, out var request))
            return;

        _activeOptions = null;
        ForEachRenderer(r => r.Close());

        _gameInstance.EnqueueActions(_characterOwnerId, request.Commands);
    }
}