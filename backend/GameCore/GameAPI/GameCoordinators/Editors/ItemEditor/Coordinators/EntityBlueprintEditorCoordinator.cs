using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public sealed record EntityAction(Intents.IIntent Intent, string Title);

public sealed record AttachmentOptionPresentation(
    string Name,
    List<IGameCommand> Commands,
    bool OpensEntityPicker
);

public sealed record AssignableEntityOption(IGameDbId EntityId, string Name, string Type);
public sealed record AssignableTemplateOption(ITemplateDbId TemplateId, string Name, string Type);

public record AttachmentPresentation(
    IGameDbId CurrentValueId,
    string CurrentValueName,
    bool IsValid,
    IEnumerable<AttachmentOptionPresentation> Options
);

public sealed record EntityBlueprintEditorPresentation(
    IBlueprint EntitySaveData,
    string EntitySaveDataAsString,
    IPreviewPresentation Preview,
    List<EntityAction> EntityActions,
    bool TemplateExists,
    Dictionary<string, AttachmentPresentation> Attachments
);

public interface IEntityBlueprintEditorQueries : ICoordinatorQueries
{
    IGameDbId EntityId { get; }
    IReadOnlyList<AssignableEntityOption> GetAssignableEntities(string attachmentPath);
    IReadOnlyList<AssignableTemplateOption> GetAssignableTemplates(string attachmentPath);
}

public interface IEntityBlueprintEditorCommands : ICoordinatorCommands
{
    void Close();
    void SaveAndClose();
    void SubmitSelectAttachmentEntity(string attachmentPath);
    void SubmitAssignExisting(string attachmentPath, IGameDbId selectedEntityId);
    void SubmitAssignTemplate(string attachmentPath, ITemplateDbId templateId);
    void SubmitCommands(List<IGameCommand> commands);
    void SubmitEntityUpdate(IBlueprint blueprint);
    void SubmitEntityUpdate(string rawBlueprint);
    void SubmitSaveTemplate();
    void SubmitTemplateIdChanged(string text);
}

public interface IEntityBlueprintEditorRenderer : IRenderer<EntityBlueprintEditorPresentation>
{

}

public class EntityBlueprintEditorCoordinator : CoordinatorBase<
    IEntityBlueprintEditorQueries,
    IEntityBlueprintEditorCommands,
    IEntityBlueprintEditorRenderer>,

    IEntityBlueprintEditorQueries,
    IEntityBlueprintEditorCommands
{
    private readonly string _editorId;
    private IGameDbId _boundEntityId;
    private IBlueprint _boundBlueprint;
    private ITemplateDbId _boundTemplateId;
    private readonly Dictionary<string, AttachmentBinding> _attachmentBindings = new();

    private sealed record AttachmentBinding(IGameDbId OwnerId, ISlotId SlotId, Type IdType, string DisplayName);

    public override IEntityBlueprintEditorQueries QueriesHandler => this;
    public override IEntityBlueprintEditorCommands CommandsHandler => this;

    public IGameDbId EntityId => _boundEntityId;

    public EntityBlueprintEditorCoordinator(GameInstance gameAPI, string editorId) : base(gameAPI)
    {
        _editorId = editorId; 
    }

    public void Close()
    {
        SubmitIntent(new Intents.Editor.CloseEntityEditor(_editorId));
    }

    public void SaveAndClose()
    {
        SubmitIntent(new Intents.Editor.CloseEntityEditor(_editorId));
    }

    protected override void OnRendererBound(IEntityBlueprintEditorRenderer renderer)
    {
        renderer.Sync(BuildPresentation());
    }

    public void Populate(IBlueprint blueprint, ITemplateDbId templateDbId = null)
    {
        _boundBlueprint = blueprint;
        _boundTemplateId = templateDbId;
        _boundEntityId = blueprint.Id;
    }

    internal void Populate(ITemplateDbId templateId)
    {
        if (!_gameInstance.Databases.TryGetBlueprintFromTemplate(templateId, out var blueprint))
            return;

        _boundBlueprint = blueprint;
        _boundTemplateId = templateId;
        _boundEntityId = null;

        SyncRenderers();
    }

    internal void Populate(IGameDbId instanceId)
    {
        if (!_gameInstance.Databases.TryGetBlueprintFromInstance(instanceId, out var blueprint))
            return;

        _boundBlueprint = blueprint;
        _boundTemplateId = null;
        _boundEntityId = instanceId;

        SyncRenderers();
    }

    private Dictionary<string, AttachmentPresentation> BuildAttachments(IBlueprint blueprint)
    {
        _attachmentBindings.Clear();

        var result = new Dictionary<string, AttachmentPresentation>();

        var fields = blueprint.GetType().GetFields();

        foreach (var field in fields)
        {
            var attr = field.GetCustomAttribute<AttachmentAttribute>();
            if (attr == null)
                continue;

            IAttachmentSlotResolver slotResolver = null;
            if (blueprint is CharacterBlueprint)
            {
                slotResolver = new CharacterAttachmentSlotResolver();
            }
            if (slotResolver == null) { continue; }

            if (!slotResolver.TryResolve(blueprint, field.Name, out var slotId))
                continue;

            var currentValue = field.GetValue(blueprint) as IGameDbId; // can improve later

            var path = field.Name;

            var idType = GetAttachmentIdType(field);

            var bindingDisplayName = attr.DisplayName ?? field.Name;
            _attachmentBindings[path] = new AttachmentBinding(blueprint.Id, slotId, idType, bindingDisplayName);

            var isValid = IsCurrentAttachmentValid(slotId, currentValue);
            var currentValueDisplayName = GetEntityDisplayName(currentValue);
            if (currentValue != null && !isValid)
                currentValueDisplayName = $"{currentValueDisplayName} [Invalid]";

            result[path] = new AttachmentPresentation(
                currentValue,
                currentValueDisplayName,
                isValid,
                BuildOptions(blueprint.Id, slotId)
            );
        }

        return result;
    }

    private static Type GetAttachmentIdType(FieldInfo field)
    {
        var t = field.FieldType;

        if (Nullable.GetUnderlyingType(t) is Type underlying)
            return underlying;

        return t;
    }

    private IEnumerable<AttachmentOptionPresentation> BuildOptions(IGameDbId ownerId, ISlotId slot)
    {
        var locationTarget = new LocationTarget(new AttachedLocation(ownerId, slot));
        var interactionsContext = new InteractionContext(Root.SYSTEM_ID, null, locationTarget);

        var interactions = _gameInstance.Interactions.GetAvailableActions(interactionsContext);
        var options = interactions.Select(x =>
        {
            var opensEntityPicker = IsAssignExistingOption(x);
            return new AttachmentOptionPresentation(
                x.Name,
                opensEntityPicker ? null : x.Commands,
                opensEntityPicker);
        }).ToList();

        // Safety fallback for slots/domains that do not provide an assign-existing interaction.
        if (!options.Any(x => x.OpensEntityPicker))
        {
            options.Insert(0, new AttachmentOptionPresentation("Assign Existing...", null, true));
        }

        return options;
    }

    private static bool IsAssignExistingOption(InteractionRequest request)
    {
        return request.Name?.StartsWith("Assign Existing", StringComparison.OrdinalIgnoreCase) == true
               && (request.Commands == null || request.Commands.Count == 0);
    }

    private EntityBlueprintEditorPresentation BuildPresentation()
    {
        RefreshBoundBlueprintFromSource();

        if (_boundBlueprint == null)
            return new EntityBlueprintEditorPresentation(null, "", null, new(), false, new());

        var transientModel = SaveLoaderRegistry.LoadUntyped(_boundBlueprint); // Kinda hacky

        _gameInstance.Interactions.TryCreatePreview(transientModel, Root.SYSTEM_ID, out var preview);

        List<EntityAction> entityActions = new List<EntityAction>();

        bool templateExists = _gameInstance.Templates.TryGetTemplate(_boundTemplateId, out _);
        bool instanceExists = _gameInstance.Databases.TryGetModelUntypedReadOnly(_boundBlueprint.Id, out _);

        // This doesnt handle editing templates properly since there is no instance made in the Db
        if (_boundBlueprint is MapChunkBlueprint mapChunkBlueprint)
        {
            if (instanceExists)
            {
                entityActions.Add(new EntityAction(new Intents.Editor.OpenInstanceInMapEditor((MapChunkId)_boundBlueprint.Id), "Open In Map Editor"));
            }
            else if (templateExists)
            {
                entityActions.Add(new EntityAction(new Intents.Editor.OpenTemplateInMapEditor((MapChunkTemplateId)_boundTemplateId), "Open In Map Editor"));
            }
        }

        EntityBlueprintEditorPresentation presentation = new EntityBlueprintEditorPresentation(
            _boundBlueprint,
            _gameInstance.File.SerializeObject(new SavePacket(_boundBlueprint)),
            preview,
            entityActions,
            templateExists,
            BuildAttachments(_boundBlueprint)
        );

        return presentation;
    }

    public void SubmitSaveTemplate()
    {
        SubmitIntent(new Intents.Editor.CreateOrUpdateTemplate(_boundTemplateId.Value, _boundBlueprint));
    }

    public void SubmitEntityUpdate(IBlueprint blueprint)
    {
        // This should be a command not an intent!!
        ExecuteTracked(new DatabaseAPI.Commands.CreateOrUpdateModel(blueprint));
    }

    public void SubmitEntityUpdate(string rawSaveDataString)
    {
        var blueprintSavePacket = _gameInstance.File.DeserializeObject<SavePacket>(rawSaveDataString);

        if (blueprintSavePacket == null)
            return;

        var untypedBlueprint = SaveLoaderRegistry.LoadUntyped(blueprintSavePacket);
        if (untypedBlueprint is not IBlueprint blueprint)
            return;

        _boundBlueprint = blueprint;
        ExecuteTracked(new DatabaseAPI.Commands.CreateOrUpdateModel(_boundBlueprint));
    }

    public IReadOnlyList<AssignableEntityOption> GetAssignableEntities(string attachmentPath)
    {
        if (!_attachmentBindings.TryGetValue(attachmentPath, out var binding))
            return Array.Empty<AssignableEntityOption>();

        return _gameInstance.Databases
            .GetAllModels()
            .Where(model => model?.Id != null)
            .Where(model => binding.IdType.IsAssignableFrom(model.Id.GetType()))
            .Where(model => IsCompatibleWithAttachment(binding, model))
            .Select(model =>
            {
                var identity = new GameModelIdentityPresentation(model);
                var name = string.IsNullOrWhiteSpace(identity.Name) ? model.Id.ToString() : identity.Name;
                return new AssignableEntityOption(model.Id, name, identity.Type ?? model.GetType().Name);
            })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SubmitAssignExisting(string attachmentPath, IGameDbId selectedEntityId)
    {
        if (selectedEntityId == null)
            return;

        if (!_attachmentBindings.TryGetValue(attachmentPath, out var binding))
            return;

        if (binding.IdType != null &&
            !binding.IdType.IsAssignableFrom(selectedEntityId.GetType()))
            return;

        if (!_gameInstance.Databases.TryGetModelUntypedReadOnly(selectedEntityId, out var selectedModel))
            return;

        if (!IsCompatibleWithAttachment(binding, selectedModel))
            return;

        ExecuteTracked(new ItemsAPI.Commands.MoveEntity(
            Root.SYSTEM_ID,
            selectedEntityId,
            new AttachedLocation(binding.OwnerId, binding.SlotId),
            false));
    }

    public void SubmitSelectAttachmentEntity(string attachmentPath)
    {
        if (string.IsNullOrWhiteSpace(attachmentPath))
            return;

        if (!TryGetAttachmentEntityId(attachmentPath, out var attachedEntityId))
            return;

        SubmitIntent(new Intents.GameEntityPrimaryCommit(attachedEntityId));
    }

    public IReadOnlyList<AssignableTemplateOption> GetAssignableTemplates(string attachmentPath)
    {
        if (!_attachmentBindings.TryGetValue(attachmentPath, out var binding))
            return Array.Empty<AssignableTemplateOption>();

        if (!_gameInstance.Templates.TryGetAllTemplates(out var templates) || templates == null)
            return Array.Empty<AssignableTemplateOption>();

        return templates
            .Where(template => template?.Id != null && template.Id.IsValid)
            .Where(template => IsTemplateCompatibleWithAttachment(binding, template.Id))
            .Select(template =>
            {
                var name = string.IsNullOrWhiteSpace(template.Name) ? template.Id.ToString() : template.Name;
                var type = string.IsNullOrWhiteSpace(template.Type) ? template.Id.GetType().Name : template.Type;
                return new AssignableTemplateOption(template.Id, name, type);
            })
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public void SubmitAssignTemplate(string attachmentPath, ITemplateDbId templateId)
    {
        if (templateId == null || !templateId.IsValid)
            return;

        if (!_attachmentBindings.TryGetValue(attachmentPath, out var binding))
            return;

        if (!IsTemplateCompatibleWithAttachment(binding, templateId))
            return;

        if (!_gameInstance.Templates.CreateModelInstanceIdFromTemplateId(templateId, out var instanceId))
            return;

        ExecuteTracked(
        new List<IGameCommand>
        {
            new TemplatesAPI.Commands.SpawnModelFromTemplate(templateId, instanceId),

            new ItemsAPI.Commands.MoveEntity(
                Root.SYSTEM_ID,
                instanceId,
                new AttachedLocation(binding.OwnerId, binding.SlotId),
                false)
        });
    }

    private bool IsCompatibleWithAttachment(AttachmentBinding binding, IGameDbResolvable model)
    {
        if (binding.OwnerId is CharacterId ownerCharacterId)
        {
            if (model is not Item item)
                return false;

            if (!_gameInstance.Databases.TryGetModel(ownerCharacterId, out Character ownerCharacter))
                return false;

            return ownerCharacter.CanEquip(item, binding.SlotId);
        }

        return true;
    }

    private bool IsTemplateCompatibleWithAttachment(AttachmentBinding binding, ITemplateDbId templateId)
    {
        if (!_gameInstance.Templates.CreateModelInstanceIdFromTemplateId(templateId, out var instanceId))
            return false;

        if (binding.IdType != null &&
            !binding.IdType.IsAssignableFrom(instanceId.GetType()))
            return false;

        if (!_gameInstance.Databases.TryGetBlueprintFromTemplate(templateId, out var templateBlueprint) ||
            templateBlueprint == null)
            return false;

        if (binding.SlotId is not LoadoutSlotId)
            return true;

        if (!GameModelFactory.TryBuildBaseEntity(templateBlueprint, out var model) || model == null)
            return false;

        return IsCompatibleWithAttachment(binding, model);
    }

    private bool TryGetAttachmentEntityId(string attachmentPath, out IGameDbId entityId)
    {
        entityId = null;

        if (_boundBlueprint == null)
            return false;

        var field = _boundBlueprint.GetType().GetField(attachmentPath);
        if (field == null)
            return false;

        if (field.GetValue(_boundBlueprint) is not IGameDbId attachedId || attachedId == null || !attachedId.IsValid)
            return false;

        entityId = attachedId;
        return true;
    }

    // TODO: Maybe genericize


    public void SyncRenderers()
    {
        RefreshBoundBlueprintFromSource();
        ForEachRenderer(r => r.Sync(BuildPresentation()));
    }

    private void RefreshBoundBlueprintFromSource()
    {
        if (_boundEntityId != null &&
            _gameInstance.Databases.TryGetBlueprintFromInstance(_boundEntityId, out var instanceBlueprint))
        {
            _boundBlueprint = instanceBlueprint;
            return;
        }

        if (_boundTemplateId != null &&
            _gameInstance.Databases.TryGetBlueprintFromTemplate(_boundTemplateId, out var templateBlueprint))
        {
            _boundBlueprint = templateBlueprint;
        }
    }

    private string GetEntityDisplayName(IGameDbId entityId)
    {
        if (entityId == null)
            return "<None>";

        if (_gameInstance.Databases.TryGetModelUntypedReadOnly(entityId, out var entity))
        {
            var identity = new GameModelIdentityPresentation(entity);
            if (!string.IsNullOrWhiteSpace(identity.Name))
                return identity.Name;
        }

        return entityId.ToString();
    }

    private bool IsCurrentAttachmentValid(ISlotId slotId, IGameDbId entityId)
    {
        if (entityId == null)
            return true;

        if (!_gameInstance.Databases.TryGetModelUntypedReadOnly(entityId, out var model))
            return false;

        if (model is not Item item)
            return false;

        return item.IsCompatibleWithSlot(slotId);
    }

    public void SubmitTemplateIdChanged(string text)
    {
        if (_gameInstance.Templates.TryCreateTemplateIdFromBlueprint(_boundBlueprint, text, out var templateId))
            _boundTemplateId = templateId;

        SyncRenderers();
    }

    public void SubmitCommands(List<IGameCommand> commands)
    {
        if (commands == null || commands.Count == 0)
            return;

        ExecuteTracked(commands);
    }

    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        bool shouldSync = false;

        if (_boundBlueprint == null)
            return;

        var boundId = _boundBlueprint.Id;

        if (evt is DatabaseAPI.Events.ModelCreated entityCreated)
        {
            if (Equals(boundId, entityCreated.GameDbId))
                shouldSync = true;
        }
        if (evt is DatabaseAPI.Events.ModelUpdated entityUpdated)
        {
            if (Equals(boundId, entityUpdated.GameDbId))
                shouldSync = true;
        }
        if (evt is DatabaseAPI.Events.ModelAttached modelAttached)
        {
            if (Equals(boundId, modelAttached.attachedLocation?.OwnerEntityId))
                shouldSync = true;
        }
        if (evt is DatabaseAPI.Events.ModelDetached modelDetached)
        {
            if (Equals(boundId, modelDetached.detachedLocation?.OwnerEntityId))
                shouldSync = true;
        }
        if (evt is DatabaseAPI.Events.ModelRemoved modelRemoved)
        {
            if (Equals(boundId, modelRemoved.GameDbId))
                shouldSync = true;
        }

        if (shouldSync)
            SyncRenderers();
    }
}
