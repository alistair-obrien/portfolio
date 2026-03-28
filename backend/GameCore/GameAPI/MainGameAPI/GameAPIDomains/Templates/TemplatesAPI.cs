using System;
using System.Collections.Generic;
using System.Linq;

public partial class TemplatesAPI : APIDomain
{
    private TemplatesGameModel TemplatesGameModel => GameAPI.TemplatesGameModel;

    private static readonly Dictionary<Type, Type> ModelToTemplateIdType = new()
    {
        { typeof(Item), typeof(ItemTemplateId) },
        { typeof(Character), typeof(CharacterTemplateId) },
        { typeof(MapChunk), typeof(MapChunkTemplateId) },
        { typeof(Prop), typeof(PropTemplateId) }
    };

    private static readonly Dictionary<Type, Type> BlueprintToTemplateIdType = new()
    {
        { typeof(ItemBlueprint), typeof(ItemTemplateId) },
        { typeof(CharacterBlueprint), typeof(CharacterTemplateId) },
        { typeof(MapChunkBlueprint), typeof(MapChunkTemplateId) },
        { typeof(PropBlueprint), typeof(PropTemplateId) }
    };

    private static readonly Dictionary<Type, Type> TemplateIdToModelId = new()
    {
        { typeof(ItemTemplateId), typeof(ItemId) },
        { typeof(CharacterTemplateId), typeof(CharacterId)},
        { typeof(MapChunkTemplateId), typeof(MapChunkId) },
        { typeof(PropTemplateId), typeof(PropId)}
    };

    public bool CreateModelInstanceIdFromTemplateId(
        ITemplateDbId templateDbId,
        out IGameDbId gameDbId)
    {
        gameDbId = null;

        if (!TemplateIdToModelId.TryGetValue(templateDbId.GetType(), out var modelIdType))
            return false;

        if (!typeof(IGameDbId).IsAssignableFrom(modelIdType))
            return false;

        // Create a temporary instance of the ID type
        var temp = Activator.CreateInstance(modelIdType) as IGameDbId;
        if (temp == null)
            return false;

        gameDbId = (IGameDbId)temp.NewOfSameType();
        return gameDbId != null;
    }

    public TemplatesAPI(GameInstance gameAPI) : base(gameAPI) 
    {
        // Register ALL Template Ids
        TypedIdTypeRegistry.Register("ItemTemplateId", s => new ItemTemplateId(s));
        TypedIdTypeRegistry.Register("CharacterTemplateId", s => new CharacterTemplateId(s));
        TypedIdTypeRegistry.Register("MapChunkTemplateId", s => new MapChunkTemplateId(s));
        TypedIdTypeRegistry.Register("PropTemplateId", s => new PropTemplateId(s));
    }

    internal IReadOnlyList<EntityTypeOption> GetAvailableTemplateTypes()
    {
        return ModelToTemplateIdType
            .Select(kvp => new EntityTypeOption(
                DisplayName: kvp.Key.Name,
                ModelType: kvp.Key,
                TemplateIdType: kvp.Value))
            .OrderBy(x => x.DisplayName)
            .ToList();
    }

    internal CommandResult LoadAll(Commands.LoadAllTemplates cmd)
    {
        if (!TemplatesGameModel.LoadDatabasesFromDisk(GameAPI.File))
            return Fail("Failed to load template databases from disk.");

        return Ok();
    }

    public CommandResult SaveAll(Commands.SaveAllTemplates cmd)
    {
        if (!TemplatesGameModel.SaveDatabasesToDisk(GameAPI.File))
            return Fail("Failed to save template databases to disk.");

        return Ok();
    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        router.Register<Commands.CreateNewTemplateFromModel>(TryCreateTemplate); //OLD
        router.Register<Commands.SpawnModelFromTemplate>(TrySpawnFromTemplate);
        router.Register<Commands.SaveAllTemplates>(SaveAll);
        router.Register<Commands.LoadAllTemplates>(LoadAll);
        router.Register<Commands.DeleteTemplate>(TryDeleteTemplate);
        router.Register<Commands.CreateOrUpdateTemplate>(TryCreateOrUpdateTemplate);
    }

    //OLD
    internal CommandResult TryCreateTemplate(Commands.CreateNewTemplateFromModel cmd)
    {
        if (!TryCreateTemplateInternal(
                cmd.ModelRootId,
                cmd.TemplateId))
        {
            return Fail($"Failed to create template {cmd.TemplateId} from model {cmd.ModelRootId}.");
        }

        return Ok();
    }

    internal bool TryCreateTemplate(
        IGameDbId instanceId,
        out ITemplateDbId templateDbId)
    {
        templateDbId = TemplatesDatabase.GenerateTemplateId(instanceId);

        TryCreateTemplateInternal(instanceId, templateDbId);
        return true;
    }

    private bool TryCreateTemplateInternal(
        IGameDbId modelRootId,
        ITemplateDbId templateId)
    {
        if (!TryBuildBlueprintGraph(modelRootId, out var graph))
            return false;

        return TryCreateTemplateFromBlueprintInternal(templateId, graph);
    }

    private CommandResult TryCreateOrUpdateTemplate(Commands.CreateOrUpdateTemplate cmd)
    {
        if (!TryCreateTemplateId(cmd.BlueprintsGraph, cmd.TemplateId, out var templateId))
            return Fail($"Failed to create or resolve template id '{cmd.TemplateId}' for the provided blueprint graph.");

        if (!TryCreateTemplateFromBlueprintInternal(templateId, cmd.BlueprintsGraph))
            return Fail($"Failed to create or update template {templateId}.");

        return Ok();
    }

    private bool TryCreateTemplateFromBlueprintInternal(ITemplateDbId templateId, List<IBlueprint> graph)
    {
        var templateDb = TemplatesDatabase.GetTemplateDatabaseById(templateId);
        if (templateDb == null)
            return false;

        var template = new Template(templateId, graph);

        if (!templateDb.TryAddOrUpdateModel(template))
            return false;

        RaiseEvent(new Events.TemplateAdded(templateId, new GameModelTemplatePresentation(template, isInDatabase: true)));
        return true;
    }

    internal bool TryBuildBlueprintGraph(IGameDbId dbId, out List<IBlueprint> outputGraph)
    {
        List<IBlueprint> graph = new();
        HashSet<IGameDbId> visited = new();

        if (!TryBuildGraphInternal(dbId, visited, graph))
        {
            outputGraph = null;
            return false;
        }

        outputGraph = graph;
        return true;
    }

    private bool TryBuildGraphInternal(IGameDbId dbId, HashSet<IGameDbId> walkedIds, List<IBlueprint> outputGraph)
    {
        if (!GameAPI.Databases.TryResolveUntyped(dbId, out IGameDbResolvable originalModel))
        {
            Debug.LogError($"Failed to Resolve {dbId}");
            return false;
        }

        if (originalModel is not ICustomSerialization serializable)
        {
            Debug.LogError($"Not serializable {originalModel.GetType()}");
            return false;
        }
        
        var packet = serializable.SaveAsPacket();

        outputGraph.Add((IBlueprint)packet.Data); //HACK

        if (serializable is IHasGameDbResolvableReferences databaseResolvableReferences)
        {
            var childIds = databaseResolvableReferences.GetChildIdReferences();
            foreach (var id in childIds)
            {
                if (walkedIds.Contains(id))
                    continue;

                TryBuildGraphInternal(id, walkedIds, outputGraph);
                walkedIds.Add(id);
            }
        }

        return true;
    }

    private bool TryGetTemplateDatabase<TId>(out IModelDatabase<Template,TId> database)
        where TId : ITemplateDbId
    {
        database = TemplatesDatabase.GetTemplateDatabase<TId>();
        // Aggressive crash out if there is no Database as we should not really be having any Uid type without a database
        if (database == null)
            throw new Exception();

        return database != null;
    }

    internal bool TryGetTemplate<TId>(TId templateUid, out Template template) 
        where TId : ITemplateDbId
    {
        template = default;
        if (!TryGetTemplateDatabase<TId>(out var database)) { return false; }
        if (!database.TryGetModel(templateUid, out template))
        {
            Debug.LogWarning($"No model in template Db for {templateUid}");
            return false;
        }

        return true;
    }

    public bool TryGetAllTemplates<TId>(out IReadOnlyList<GameModelTemplatePresentation> items)
        where TId : ITemplateDbId
    {
        items = null;

        if (!TryGetTemplateDatabase<TId>(out var database))
            return false;

        items = database
            .GetAllModels()
            .OfType<Template>()
            .ToList()
            .ConvertAllToList(x => new GameModelTemplatePresentation(x, isInDatabase: true));

        return true;
    }

    public bool TryGetAllTemplates(out IReadOnlyList<GameModelTemplatePresentation> items)
    {
        var rawModels = TemplatesDatabase.GetAllTemplates();
        items = rawModels.ConvertAllToList(x => new GameModelTemplatePresentation(x, isInDatabase: true));
        return true;
    }

    internal bool TryCreateTemplateIdFromBlueprint(IBlueprint root, string templateIdStr, out ITemplateDbId templateId)
    {
        templateId = null;

        if (!BlueprintToTemplateIdType.TryGetValue(root.GetType(), out var templateIdType))
            return false;

        var id = TypedIdTypeRegistry.Create(templateIdType.Name, templateIdStr);

        if (id is not ITemplateDbId typedId || !typedId.IsValid)
            return false;

        var database = TemplatesDatabase.GetTemplateDatabaseById(typedId);

        templateId = typedId;
        return true;
    }

    internal bool TryCreateTemplateId<T>(
        string stringId,
        out ITemplateDbId templateId)
        where T : IGameDbResolvable
    {
        templateId = null;

        var modelType = typeof(T);

        if (!ModelToTemplateIdType.TryGetValue(modelType, out var templateIdType))
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

        var id = TypedIdTypeRegistry.Create(templateIdType.Name, stringId);

        if (id is not ITemplateDbId typedId || !typedId.IsValid)
            return false;

        var database = TemplatesDatabase.GetTemplateDatabaseById(typedId);
        if (database == null)
            return false;

        if (database.TryGetModel(typedId, out _))
        {
            Debug.LogWarning($"Template ID already exists: {typedId}");
            return false;
        }

        templateId = typedId;
        return true;
    }

    private bool TryCreateTemplateId(List<IBlueprint> blueprintsGraph, string templateIdStr, out ITemplateDbId templateId)
    {
        templateId = null;

        if (blueprintsGraph.Count == 0) 
            return false;

        var root = blueprintsGraph.FirstOrDefault();
        if (root == null)
            return false;

        return TryCreateTemplateIdFromBlueprint(root, templateIdStr, out templateId);
    }

    private static string NormalizeId(string input)
    {
        return input
            .Trim()
            .ToLowerInvariant()
            .Replace(" ", "_");
    }

    internal bool TryCreateTemplateFromModel<T>(
        T model,
        ITemplateDbId templateId)
        where T : IGameDbResolvable, new()
    {
        if (model is not ICustomSerialization serializable)
        {
            Debug.LogError($"Model {typeof(T).Name} not serializable");
            return false;
        }

        var rootPacket = serializable.SaveAsPacket();

        var graph = new List<IBlueprint> { (IBlueprint)rootPacket.Data }; // What??

        var templateDb = TemplatesDatabase.GetTemplateDatabaseById(templateId);
        if (templateDb == null)
            return false;

        var template = new Template(templateId, graph);

        if (!templateDb.TryAddModel(template))
            return false;

        RaiseEvent(new Events.TemplateAdded(templateId, new GameModelTemplatePresentation(template, isInDatabase: true)));

        return true;
    }

    internal bool TryGetTemplate(
        ITemplateDbId templateUid,
        out Template template)
    {
        template = null;

        if (templateUid == null || !templateUid.IsValid)
            return false;

        var database = TemplatesDatabase.GetTemplateDatabaseById(templateUid);

        if (database == null)
        {
            Debug.LogError($"No template database found for id type {templateUid.GetType().Name}");
            return false;
        }

        if (!database.TryGetModel(templateUid, out var model))
        {
            return false;
        }

        if (model is not Template typedModel)
            return false;

        template = typedModel;

        return true;
    }

    internal bool TryDuplicateTemplate(
        ITemplateDbId templateId,
        out ITemplateDbId newTemplateId)
    {
        newTemplateId = null;
        // TODO: Reimplement

        return true;
    }

    internal CommandResult TryDeleteTemplate(Commands.DeleteTemplate cmd)
    {
        if (cmd.TemplateId == null || !cmd.TemplateId.IsValid)
            return Fail("DeleteTemplate was called with an invalid template id.");

        var database = TemplatesDatabase.GetTemplateDatabaseById(cmd.TemplateId);

        if (database == null)
            return Fail($"No template database found for template id {cmd.TemplateId}.");

        if (!database.TryRemoveModel(cmd.TemplateId))
            return Fail($"Failed to remove template {cmd.TemplateId} from its database.");

        RaiseEvent(new Events.TemplateRemoved(cmd.TemplateId));

        return Ok();
    }

    internal bool TryCreateTemplatePreview(
        ITemplateDbId templateUid,
        CharacterId characterViewer,
        out IPreviewPresentation preview)
    {
        preview = null;

        if (!TryGetTemplate(templateUid, out var template))
            return false;

        // Spawn
        if (!GameAPI.Templates.TrySpawnFromTemplateUntyped(templateUid, null, out var instanceId))
            return false;

        if (!GameAPI.Interactions.TryCreatePreview(instanceId, characterViewer, out preview))
        {
            GameAPI.Databases.TryRemoveModel(instanceId);
            return false;
        }

        GameAPI.Databases.TryRemoveModel(instanceId);

        return true;
    }

    private CommandResult TrySpawnFromTemplate(Commands.SpawnModelFromTemplate template)
    {
        if (!TrySpawnFromTemplateUntyped(template.TemplateDbId, template.InstanceId, out _))
            return Fail($"Failed to spawn instance from template {template.TemplateDbId} using requested instance id {template.InstanceId}.");

        return Ok();
    }

    public bool TrySpawnFromTemplateUntyped(
        ITemplateDbId templateId,
        IGameDbId instanceId, 
        out IGameDbId resolvedInstanceId)
    {
        resolvedInstanceId = null;

        if (!TryGetTemplate(
            templateId,
            out var template))
            return false;

        if (!GameModelFactory.TryBuildEntities(
            template.EntityBlueprints, 
            instanceId, 
            out resolvedInstanceId, 
            out var buildEntities))
            return false;

        // Finally add to the real database to finish off the whole process
        foreach (var model in buildEntities)
        {
            if (!GameAPI.Databases.TryAddModel(new DatabaseAPI.Commands.AddEntity(model)).Ok)
                return false;
        }

        return true;
    }
}
