using System;
using System.Collections.Generic;

public class EntityBrowserPresentation
{
    public List<IGameDbId> BreadCrumbTrail;
    public GameModelIdentityPresentation CurrentEntityOwner;
    public GameModelIdentityPresentation CurrentEntity;
    public bool IsInvalidAttachment;

    public List<EntityBrowserPresentation> AttachedEntities;
    public List<EntityBrowserPresentation> ReferencedEntities;

    public List<BlueprintCreationOption> CreatableEntityTypes;
}

public interface IGameDatabaseBrowserQueries : ICoordinatorQueries
{

}

public interface IGameDatabaseBrowserCommands : ICoordinatorCommands
{
    public void SetSearchText(string str);
    public void CreateNewEntity();
    public void CreateEntity(Type blueprintType);
    public void DeleteSelectedEntity();
    public void EditSelectedEntity();

    void EntitySecondaryCommit(IGameDbId id, IGameDbId actingEntityId = null);
    void EntitySecondaryCommitFromTemplate(IGameDbId id, ITemplateDbId templateId);
    void EntityPrimaryCommit(IGameDbId id);
    void EntityPrimaryDoubleCommit(IGameDbId id);
    void FrameEntity(IGameDbId id);
}

public interface IGameDatabaseBrowserRenderer : IRenderer<EntityBrowserPresentation>
{
    void RenderEntitySelected(IGameDbId Id);
    void RenderEntityDeselected(IGameDbId Id);
}

public class GameDatabaseBrowserCoordinator : CoordinatorBase<
    IGameDatabaseBrowserQueries,
    IGameDatabaseBrowserCommands,
    IGameDatabaseBrowserRenderer>,

    IGameDatabaseBrowserQueries,
    IGameDatabaseBrowserCommands
{
    private Stack<IGameDbId> _breadCrumbTrail = new();

    public readonly string Id;
    private IGameDbId _selectedGameDbId;

    public GameDatabaseBrowserCoordinator(string id, GameInstance gameAPI) : base(gameAPI) { Id = id; }

    public override IGameDatabaseBrowserQueries QueriesHandler => this;
    public override IGameDatabaseBrowserCommands CommandsHandler => this;

    private List<BlueprintCreationOption> GetCreationOptions()
    {
        return new List<BlueprintCreationOption>
        {
            new("Create Map Template", typeof(MapChunkBlueprint)),
            new("Create Item Template", typeof(ItemBlueprint)),
            new("Create Prop Template", typeof(PropBlueprint)),
            new("Create Character Template", typeof(CharacterBlueprint))
        };
    }

    public void CreateNewEntity()
    {
        throw new System.NotImplementedException();
    }

    public void CreateEntity(Type blueprintType)
    {
        if (blueprintType == null || !typeof(IBlueprint).IsAssignableFrom(blueprintType))
            return;

        if (Activator.CreateInstance(blueprintType) is not IBlueprint blueprint)
            return;

        if (blueprint is CharacterBlueprint characterBlueprint &&
            string.IsNullOrWhiteSpace(characterBlueprint.Name))
        {
            characterBlueprint.Name = "Derp";
        }

        SubmitIntent(new Intents.Editor.CreateOrUpdateTemplate(Guid.NewGuid().ToString(), blueprint));
    }

    public void DeleteSelectedEntity()
    {
        throw new System.NotImplementedException();
    }

    public void EditSelectedEntity()
    {
        throw new System.NotImplementedException();
    }

    public void SetSearchText(string str)
    {
        throw new System.NotImplementedException();
    }

    protected override void OnRendererBound(IGameDatabaseBrowserRenderer renderer)
    {
        renderer.Sync(BuildPresentation());
    }

    private EntityBrowserPresentation BuildPresentation()
    {
        var rootModel = _gameInstance.RootModel;

        var rootPresentation = new EntityBrowserPresentation
        {
            CurrentEntity = new GameModelIdentityPresentation(rootModel),
            AttachedEntities = new List<EntityBrowserPresentation>(),
            ReferencedEntities = new List<EntityBrowserPresentation>(),
            CreatableEntityTypes = GetCreationOptions(),
            BreadCrumbTrail = new List<IGameDbId> { }
        };

        HashSet<IGameDbId> visited = new();

        foreach (var entityId in rootModel.AttachedEntities)
        {
            var child = BuildPresentations(entityId, visited);
            if (child != null)
            {
                rootPresentation.AttachedEntities.Add(child);
            }
        }

        return rootPresentation;
    }

    private EntityBrowserPresentation BuildPresentations(
        IGameDbId gameDbId,
        HashSet<IGameDbId> visited)
    {
        if (!visited.Add(gameDbId))
            return null;

        if (!_gameInstance.Databases.TryGetModelUntypedReadOnly(gameDbId, out var model))
            return null;

        var presentation = new EntityBrowserPresentation
        {
            CurrentEntity = new GameModelIdentityPresentation(model),
            IsInvalidAttachment = IsInvalidAttachment(model),
            AttachedEntities = new List<EntityBrowserPresentation>(),
            ReferencedEntities = new List<EntityBrowserPresentation>()
        };

        // Owner
        if (model.AttachedLocation?.OwnerEntityId != null &&
            _gameInstance.Databases.TryGetModelUntypedReadOnly(model.AttachedLocation.OwnerEntityId, out var ownerEntity))
        {
            presentation.CurrentEntityOwner = new GameModelIdentityPresentation(ownerEntity);
        }

        // Attachments (TREE)
        if (model is IHasAttachments hasAttachments)
        {
            foreach (var id in hasAttachments.GetAttachedEntityIds())
            {
                var child = BuildPresentations(id, visited);
                if (child != null)
                {
                    presentation.AttachedEntities.Add(child);
                }
            }
        }

        // References (GRAPH — optional recursion)
        if (model is IHasReferences hasReferences)
        {
            foreach (var id in hasReferences.GetReferencedEntityIds())
            {
                if (_gameInstance.Databases.TryGetModelUntypedReadOnly(id, out var referencedEntity))
                {
                    presentation.ReferencedEntities.Add(new EntityBrowserPresentation
                    {
                        CurrentEntity = new GameModelIdentityPresentation(referencedEntity),
                        AttachedEntities = null,
                        ReferencedEntities = null
                    });
                }
            }
        }

        return presentation;
    }

    private static bool IsInvalidAttachment(IGameDbResolvable model)
    {
        if (model?.AttachedLocation is not AttachedLocation attachedLocation)
            return false;

        if (model is not Item item)
            return false;

        return !item.IsCompatibleWithSlot(attachedLocation.SlotPath);
    }


    // Force Rebuild on every event for now
    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        if (evt is DatabaseAPI.Events.ModelAttached modelAttached)
        {
            SyncRenderers();
        }
        if (evt is DatabaseAPI.Events.ModelDetached modelDettached)
        {
            SyncRenderers();
        }
        if (evt is DatabaseAPI.Events.ModelCreated modelCreated)
        {
            SyncRenderers();
        }
        if (evt is DatabaseAPI.Events.ModelRemoved modelRemoved)
        {
            SyncRenderers();
        }
        if (evt is DatabaseAPI.Events.ModelUpdated modelUpdated)
        {
            SyncRenderers();
        }
    }

    public void SyncRenderers()
    {
        var presentation = BuildPresentation();
        ForEachRenderer(r => r.Sync(presentation));

        if (_selectedGameDbId != null)
        {
            ForEachRenderer(r => r.RenderEntitySelected(_selectedGameDbId));
        }
    }

    public void EntityPrimaryCommit(IGameDbId id)
    {
        SubmitIntent(new Intents.GameEntityPrimaryCommit(id));
    }

    public void EntitySecondaryCommit(IGameDbId id, IGameDbId actingEntityId = null)
    {
        SubmitIntent(new Intents.GameEntitySecondaryCommit(id, actingEntityId));
    }

    public void EntitySecondaryCommitFromTemplate(IGameDbId id, ITemplateDbId templateId)
    {
        SubmitIntent(new Intents.GameEntitySecondaryCommitFromTemplate(id, templateId));
    }

    public void EntityPrimaryDoubleCommit(IGameDbId id)
    {
        SubmitIntent(new Intents.Editor.OpenEntity(id));
    }

    public void FrameEntity(IGameDbId id)
    {
        SubmitIntent(new Intents.Editor.FrameEntity(id));
    }

    public void EntityHoverExit(IGameDbId id)
    {
        SubmitIntent(new Intents.GameEntityHoverExit(id));
    }

    public void EntityHoverEnter(IGameDbId id)
    {
        SubmitIntent(new Intents.GameEntityHoverEnter(id));
    }

    internal void SetSelected(IGameDbId newSelected)
    {
        var prevSelected = _selectedGameDbId;
        _selectedGameDbId = newSelected;

        if (prevSelected != null && prevSelected != newSelected)
        {
            ForEachRenderer(r => r.RenderEntityDeselected(prevSelected));
        }

        if (newSelected != null)
        {
            ForEachRenderer(r => r.RenderEntitySelected(newSelected));
        }
    }

    internal void Populate(IGameDbId newSelected)
    {
        IGameDbId prevSelected = _selectedGameDbId;
        _selectedGameDbId = newSelected;

        var presentation = BuildPresentation();
        ForEachRenderer(r => r.Sync(presentation));
        SetSelected(_selectedGameDbId);
    }

    internal void Refresh()
    {
        SyncRenderers();
    }
}
