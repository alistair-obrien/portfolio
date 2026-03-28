using System;
using System.Collections.Generic;
using System.Linq;

public interface ITemplatesBrowserQueries : ICoordinatorQueries
{

}

public interface ITemplatesBrowserCommands : ICoordinatorCommands
{
    void SetSearchText(string str);
    void TemplatePrimaryCommit(ITemplateDbId id);
    void TemplatePrimaryDoubleCommit(ITemplateDbId id);
    void FrameTemplate(ITemplateDbId id);
    void TemplateSecondaryCommit(ITemplateDbId id);
    void CreateTemplate(Type blueprintType);
}

public interface ITemplatesBrowserRenderer : IRenderer<TemplatesBrowserPresentation>
{
    void RenderTemplateDeselected(ITemplateDbId prevSelected);
    void RenderTemplateSelected(ITemplateDbId selectedTemplateId);
}

public class TemplateBrowserSettings
{
    public string SearchText;
    public Func<GameModelTemplatePresentation, bool> CustomPredicate;
}

public class TemplatesBrowserCoordinator : CoordinatorBase<
    ITemplatesBrowserQueries, 
    ITemplatesBrowserCommands, 
    ITemplatesBrowserRenderer>,

    ITemplatesBrowserQueries,
    ITemplatesBrowserCommands
{
    public string Id { get; private set; }

    // TODO: Add some parameters here maybe? For filtering or the types of templates it should adhere to
    public override ITemplatesBrowserQueries QueriesHandler => this;
    public override ITemplatesBrowserCommands CommandsHandler => this;
    
    private TemplateBrowserSettings _settings;

    private Dictionary<ITemplateDbId, GameModelTemplatePresentation> _allTemplates;
    private Dictionary<ITemplateDbId, GameModelTemplatePresentation> _filteredTemplates; // Filtered Templates

    private ITemplateDbId _selectedTemplateId;

    public TemplatesBrowserCoordinator(GameInstance gameAPI, string id) : base(gameAPI)
    {
        Id = id;
    }

    protected override void OnInitialize()
    {
        _settings = new TemplateBrowserSettings
        {
            SearchText = "",
            CustomPredicate = null
        };

        _allTemplates = new Dictionary<ITemplateDbId, GameModelTemplatePresentation>();
        _filteredTemplates = new Dictionary<ITemplateDbId, GameModelTemplatePresentation>();

        if (_gameInstance.Templates.TryGetAllTemplates(out var templates))
        {
            foreach (var template in templates)
            {
                _allTemplates.Add(template.Id, template);
            }
        }

        RebuildFilteredTemplates();
    }

    public void ApplySettings(TemplateBrowserSettings settings)
    {
        _settings = settings;
        RebuildFilteredTemplates();
        SyncRenderers();
    }

    private void RebuildFilteredTemplates()
    {
        _filteredTemplates.Clear();

        foreach (var kvp in _allTemplates)
        {
            if (MatchesFilter(kvp.Value))
            {
                _filteredTemplates.Add(kvp.Key, kvp.Value);
            }
        }
    }

    protected override void OnRendererBound(ITemplatesBrowserRenderer renderer)
    {
        renderer.Sync(BuildPresentation()); //Contains _templates and things that are allowed/not allowed
    }

    public void TemplatePrimaryCommit(ITemplateDbId id)
    {
        SubmitIntent(new Intents.Editor.TemplatePrimaryCommit(id));
    }

    public void TemplatePrimaryDoubleCommit(ITemplateDbId id)
    {
        SubmitIntent(new Intents.Editor.OpenTemplate(id));
    }

    public void FrameTemplate(ITemplateDbId id)
    {
        SubmitIntent(new Intents.Editor.FrameTemplate(id));
    }

    public void TemplateSecondaryCommit(ITemplateDbId id)
    {
        SubmitIntent(new Intents.Editor.TemplateSecondaryCommit(id));
    }

    public void CreateTemplate(Type blueprintType)
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

    protected override void DoHandleGameEvent(IGameEvent evt)
    {
        switch (evt)
        {
            case TemplatesAPI.Events.TemplateAdded e:
                _allTemplates[e.Id] = e.Presentation;

                if (MatchesFilter(e.Presentation))
                {
                    _filteredTemplates[e.Id] = e.Presentation;
                    SyncRenderers();
                }
                break;
            case TemplatesAPI.Events.TemplateRemoved e:
                _allTemplates.Remove(e.Id);

                if (_filteredTemplates.Remove(e.Id))
                    SyncRenderers();
                break;
            case TemplatesAPI.Events.TemplateUpdated e:
                _allTemplates[e.Id] = e.Presentation;

                bool matches = MatchesFilter(e.Presentation);
                bool exists = _filteredTemplates.ContainsKey(e.Id);

                if (matches && !exists)
                    _filteredTemplates[e.Id] = e.Presentation;
                else if (!matches && exists)
                    _filteredTemplates.Remove(e.Id);
                else if (matches && exists)
                    _filteredTemplates[e.Id] = e.Presentation;
                else
                    return;

                SyncRenderers();
                break;
        };
    }

    public void SyncRenderers()
    {
        var presentation = BuildPresentation();
        ForEachRenderer(r => r.Sync(presentation));
    }

    private TemplatesBrowserPresentation BuildPresentation()
    {
        return new TemplatesBrowserPresentation()
        {
            AllTemplates = _allTemplates.Values.ToList(),
            FilteredTemplates = _filteredTemplates.Values.ToList(),
            CreatableTemplateTypes = GetCreationOptions()
        };
    }

    public void SetSearchText(string str)
    {
        _settings = new TemplateBrowserSettings
        {
            SearchText = str,
            CustomPredicate = _settings.CustomPredicate
        };

        RebuildFilteredTemplates();
        SyncRenderers();
    }

    private bool MatchesFilter(GameModelTemplatePresentation template)
    {
        if (!string.IsNullOrEmpty(_settings.SearchText) &&
            (template.Name == null || 
            !template.Name.Contains(_settings.SearchText, StringComparison.OrdinalIgnoreCase)))
            return false;

        if (_settings.CustomPredicate != null &&
            !_settings.CustomPredicate(template))
            return false;

        return true;
    }

    internal void SetSelected(ITemplateDbId newSelected)
    {
        ITemplateDbId prevSelected = _selectedTemplateId;
        _selectedTemplateId = newSelected;

        // Not working??
        if (_selectedTemplateId == null && prevSelected != null)
        {
            ForEachRenderer(r => r.RenderTemplateDeselected(prevSelected));
        }

        if (prevSelected != _selectedTemplateId && prevSelected != null)
        {
            ForEachRenderer(r => r.RenderTemplateDeselected(prevSelected));
        }
        if (_selectedTemplateId != null)
        {
            ForEachRenderer(r => r.RenderTemplateSelected(_selectedTemplateId));
        }
    }

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
}
