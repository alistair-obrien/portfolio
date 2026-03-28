using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;

public class CommandExecutionException : Exception
{
    public IGameCommand Command { get; }

    public CommandExecutionException(
        IGameCommand command,
        string message = null,
        Exception inner = null)
        : base(message ?? $"Command failed: {command}", inner)
    {
        Command = command;
    }
}

public class DatabaseAPI : APIDomain
{
    public class Commands
    {
        public sealed record CreateOrUpdateModel(IBlueprint Blueprint) : IGameCommand;
        public sealed record AddEntity(IGameDbResolvable Entity) : IGameCommand;
        public sealed record RemoveEntity(IGameDbId GameDbId) : IGameCommand;
        public sealed record DuplicateEntity(IGameDbId Entity) : IGameCommand;
    }

    public class Events
    {
        public sealed record ModelCreated(IGameDbId GameDbId) : IGameEvent;
        public sealed record ModelUpdated(IGameDbId GameDbId) : IGameEvent;
        public sealed record ModelRemoved(IGameDbId GameDbId) : IGameEvent;
        public sealed record ModelAttached(IGameDbId GameDbId, IGameModelLocation attachedLocation) : IGameEvent;
        public sealed record ModelDetached(IGameDbId GameDbId, IGameModelLocation detachedLocation) : IGameEvent;

    }

    public readonly EntityLocationAPI EntityLocationAPI;
    public readonly EntityAttachmentAPI EntityAttachmentAPI;
    public readonly EntityMoverAPI EntityMoverAPI;

    private readonly Dictionary<Type, Type> _modelToIdTypeRegistry = new();

    public DatabaseAPI(GameInstance gameAPI) : base(gameAPI) 
    {
        // Register ALL Database Ids
        TypedIdTypeRegistry.Register("CharacterId", s => new CharacterId(s));
        TypedIdTypeRegistry.Register("ItemId", s => new ItemId(s));
        TypedIdTypeRegistry.Register("MapChunkId", s => new MapChunkId(s));
        TypedIdTypeRegistry.Register("PropId", s => new PropId(s));
        TypedIdTypeRegistry.Register("FactionId", s => new FactionId(s));
        TypedIdTypeRegistry.Register("ManufacturerId", s => new ManufacturerId(s));

        // Register Non Db
        TypedIdTypeRegistry.Register("PropLayerId", s => new PropLayerId(s));

        // Register Slots
        TypedIdTypeRegistry.Register("CyberneticSlotId", s => new CyberneticSlotId(s));
        TypedIdTypeRegistry.Register("AnatomySlotId", s => new AnatomySlotId(s));
        TypedIdTypeRegistry.Register("LoadoutSlotId", s => new LoadoutSlotId(s));
        TypedIdTypeRegistry.Register("StyleSlotId", s => new StyleSlotId(s));

        EntityLocationAPI = new EntityLocationAPI(gameAPI);
        EntityAttachmentAPI = new EntityAttachmentAPI(gameAPI);
        EntityMoverAPI = new EntityMoverAPI(gameAPI);
    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        router.Register<Commands.AddEntity>(TryAddModel);
        router.Register<Commands.RemoveEntity>(TryRemoveModel);
        router.Register<Commands.CreateOrUpdateModel>(TryCreateOrUpdateModel);
    }

    public static IBlueprint BuildNewBlueprint(Type modelType)
    {
        IBlueprint blueprint = null;
        if (modelType == typeof(Character))
        {
            blueprint = new CharacterBlueprint();
        }
        else if (modelType == typeof(Item))
        {
            blueprint = new ItemBlueprint();
        }
        else if (modelType == typeof(Prop))
        {
            blueprint = new PropBlueprint();
        }
        else if (modelType == typeof(MapChunk))
        {
            blueprint = new MapChunkBlueprint();
        }

        return blueprint;
    }

    public bool TryGetBlueprintFromTemplate(ITemplateDbId templateDbId, out IBlueprint blueprint)
    {
        blueprint = null;
        
        if (!GameAPI.Templates.TryGetTemplate(templateDbId, out var template))
            return false;
        
        blueprint = template.EntityBlueprintRoot;
        return true;
    }

    public bool TryGetBlueprintFromInstance(IGameDbId instanceId, out IBlueprint blueprint)
    {
        blueprint = null;

        if (!TryGetModelUntypedReadOnly(instanceId, out var model))
            return false;

        if (model is not ICustomSerialization s)
            return false;

        blueprint = (IBlueprint)s.SaveAsPacket().Data; // HACK

        return true;
    }

    public CommandResult TryCreateOrUpdateModel(Commands.CreateOrUpdateModel cmd)
    {
        if (cmd.Blueprint == null)
            return Fail("CreateOrUpdateModel was called without a blueprint.");

        if (cmd.Blueprint.Id == null || !cmd.Blueprint.Id.IsValid)
            return Fail("CreateOrUpdateModel was called with an invalid blueprint id.");

        TryGetModelUntyped(cmd.Blueprint.Id, out var existingModel);

        var databaseDict = GameAPI.RootModel.GameDatabases.GetRaw();

        if (existingModel != null)
        {
            Debug.Log("Checking Attachment Changes on: " + existingModel.Name);
            var attachmentChanges = existingModel.GetAttachmentChanges(cmd.Blueprint, databaseDict);
            Debug.Log("Attachment Changes: " + attachmentChanges.Count());

            Debug.Log("Applying Blueprint to: " + existingModel.Name);
            existingModel.ApplyBlueprint(cmd.Blueprint);

            Debug.Log($"Applying {attachmentChanges.Count()} attachment Changes to: " + existingModel.Name);
            foreach (var change in attachmentChanges)
            {
                if (Equals(change.OldLocation, change.NewLocation))
                    continue;

                if (!EntityMoverAPI.TryMoveEntity(
                    new ItemsAPI.Commands.MoveEntity(
                        Root.SYSTEM_ID,
                        change.EntityId,
                        change.NewLocation,
                        false)).Ok)
                {
                    return Fail($"Failed to move dependent entity {change.EntityId} while updating model {existingModel.Id}.");
                }
            }

            RaiseEvent(new Events.ModelUpdated(existingModel.Id));
            return Ok();
        }

        if (!GameModelFactory.TryBuildBaseEntity(cmd.Blueprint, out var newModel))
            return Fail($"Failed to build a model instance from blueprint {cmd.Blueprint.Id}.");

        var addResult = TryAddModel(new Commands.AddEntity(newModel));
        if (!addResult.Ok)
            return Fail($"Failed to add model {newModel.Id}: {addResult.ErrorMessage}");

        return Ok();
    }

    public void Initialize()
    {
        _modelToIdTypeRegistry.Clear();
        Register<MapChunk, MapChunkId>();
        Register<Character, CharacterId>();
        Register<Item, ItemId>();
        Register<Prop, PropId>();
    }

    private void Register<TModel, TId>()
        where TModel : IGameDbResolvable
        where TId : IGameDbId
    {
        _modelToIdTypeRegistry.Add(typeof(TModel), typeof(TId));
    }

    public bool TryDuplicateModel(
        IGameDbId originalId,
        out IGameDbId newInstanceId)
    {
        newInstanceId = null;

        if (!GameModelCloner.TryDuplicateGraph(this, originalId, out var duplicatedGraph))
            return false;

        var addedIds = new List<IGameDbId>();
        var succeeded = false;

        try
        {
            foreach (var clone in duplicatedGraph.ClonesByNewId.Values)
            {
                if (!TryAddModel(new Commands.AddEntity(clone)).Ok)
                    return false;

                addedIds.Add(clone.Id);
            }

            foreach (var internalLocation in duplicatedGraph.InternalLocationsByNewId)
            {
                if (!GameAPI.TryExecuteCommand(new ItemsAPI.Commands.MoveEntity(
                    Root.SYSTEM_ID,
                    internalLocation.Key,
                    internalLocation.Value,
                    false)).Ok)
                {
                    return false;
                }
            }

            newInstanceId = duplicatedGraph.RootId;
            succeeded = true;
            return true;
        }
        finally
        {
            if (!succeeded)
            {
                for (int i = addedIds.Count - 1; i >= 0; i--)
                {
                    TryRemoveModel(addedIds[i]);
                }
            }
        }
    }

    public bool TryGetModelUntyped(IGameDbId id, out IGameDbResolvable model)
    {
        model = default;

        if (id == null)
            return false;

        if (!id.IsValid)
            return false;

        var sim = GameAPI.RootModel.GameDatabases;

        return sim.TryGetMutable(
            id,
            out model);
    }

    public bool TryGetModelUntypedReadOnly(IGameDbId id, out IGameDbResolvable model)
    {
        model = default;

        if (id == null)
            return false;

        if (!id.IsValid)
            return false;

        var sim = GameAPI.RootModel.GameDatabases;
        return sim.TryGetVisible(id, out model);
    }

    public bool TryGetModelReadOnly<TModel, TId>(TId id, out TModel model)
        where TModel : IGameDbResolvable
        where TId : IGameDbId
    {
        model = default;

        if (!TryGetModelUntypedReadOnly(id, out var untypedModel))
            return false;

        if (untypedModel is not TModel typedModel)
            return false;

        model = typedModel;
        return true;
    }

    public bool TryGetModel<TModel, TId>(TId id, out TModel model)
        where TModel : IGameDbResolvable
        where TId : IGameDbId
    {
        model = default;
        
        if (!TryGetModelUntyped(id, out var untypedModel))
            return false;

        if (untypedModel is not TModel typedModel)
            return false;

        model = typedModel;
        return true;
    }

    public CommandResult TryAddModel(Commands.AddEntity cmd)
    {
        if (cmd.Entity == null)
            return Fail("AddEntity was called without an entity.");

        if (cmd.Entity.Id == null || !cmd.Entity.Id.IsValid)
            return Fail("AddEntity was called with an invalid entity id.");

        if (!RootModel.GameDatabases.TryAddModel(cmd.Entity))
            return Fail($"Failed to add entity {cmd.Entity.Id} to the game database.");

        var moveToRootResult = GameAPI.TryExecuteCommand(new ItemsAPI.Commands.MoveEntity(
                Root.SYSTEM_ID,
                cmd.Entity.Id,
                new RootLocation(),
                false));
        if (!moveToRootResult.Ok)
        {
            RootModel.GameDatabases.TryRemoveModel(cmd.Entity.Id);
            return Fail($"Entity {cmd.Entity.Id} was added, but attaching it to the root failed: {moveToRootResult.ErrorMessage}");
        }

        RaiseEvent(new Events.ModelCreated(cmd.Entity.Id));

        return Ok();

    }

    public CommandResult TryRemoveModel(Commands.RemoveEntity removeModel)
    {
        if (removeModel.GameDbId == null || !removeModel.GameDbId.IsValid)
            return Fail("RemoveEntity was called with an invalid entity id.");

        if (!TryRemoveModel(removeModel.GameDbId))
            return Fail($"Failed to remove entity {removeModel.GameDbId}.");

        return Ok();
    }

    public bool TryRemoveModel(IGameDbId id)
    {
        if (id == null || !id.IsValid)
            return false;

        var visited = new HashSet<IGameDbId>();

        bool DespawnRecursive(IGameDbId currentId)
        {
            if (!visited.Add(currentId))
                return true; // already processed

            if (!RootModel.GameDatabases.TryGetMutable(currentId, out var model))
                return false;

            if (model is IHasGameDbResolvableReferences refs)
            {
                foreach (var child in refs.GetChildIdReferences())
                {
                    if (!DespawnRecursive(child))
                        return false;
                }
            }

            // Remove from root
            if (!GameAPI.TryExecuteCommand(new ItemsAPI.Commands.MoveEntity(
                    Root.SYSTEM_ID,
                    currentId,
                    null,
                    false)).Ok)
            {
                return false;
            }

            if (!RootModel.GameDatabases.TryRemoveModel(currentId))
                return false;

            return true;
        }

        return DespawnRecursive(id);
    }

    public bool TryGetLocationOfModel<TIdType, TLocationType>(
       TIdType gameDbId,
       out TLocationType gameModelLocation)
       where TIdType : IGameDbId
    where TLocationType : IGameModelLocation
    {
        gameModelLocation = default;

        if (!TryGetLocationOfModel(gameDbId, out var location))
            location = null;

        if (location is not TLocationType typedLocation)
            return false;

        gameModelLocation = typedLocation;
        return true;
    }

    public bool TryGetLocationOfModel(IGameDbId gameDbId, out IGameModelLocation gameModelLocation)
    {
        gameModelLocation = null;

        if (!TryGetModelUntyped(gameDbId, out var model))
            return false;

        gameModelLocation = model.AttachedLocation;
        return true;
    }

    internal bool TryCreateGameModelId(
        Type modelType,
        string stringId,
        out IGameDbId gameDb)
    {
        gameDb = null;

        if (!_modelToIdTypeRegistry.TryGetValue(modelType, out var idType))
        {
            Debug.LogError($"No template id mapping for model type {modelType.Name}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(stringId))
        {
            Debug.LogWarning("Template ID must be explicitly provided.");
            return false;
        }

        stringId = NormalizeId(stringId);

        var id = TypedIdTypeRegistry.Create(idType.Name, stringId);

        if (id is not IGameDbId typedId || !typedId.IsValid)
            return false;

        if (TryGetModelUntyped(typedId, out _))
        {
            Debug.LogWarning($"ID already exists: {typedId}");
            return false;
        }

        gameDb = typedId;
        return true;
    }

    private static string NormalizeId(string input)
    {
        return input
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_");
    }

    internal IEnumerable<IGameDbResolvable> GetAllModels()
    {
        return RootModel.GameDatabases.GetAllModels();
    }
}
