using System;
using System.Collections.Generic;

public class GameModelTemplatePresentation
{
    public bool IsSavedToDisk;
    public ITemplateDbId Id;
    public readonly string Name;
    public readonly string Type;

    public GameModelTemplatePresentation(Template template, bool isInDatabase)
    {
        Id = template.Id;
        Name = template.EntityBlueprintRoot.Name;
        IsSavedToDisk = isInDatabase;

        Type = template.Id switch
        {
            CharacterTemplateId => "Character",
            PropTemplateId => "Prop",
            ItemTemplateId => "Item",
            MapChunkTemplateId => "Map",
            _ => "Unknown"
        };
    }
}

public class GameModelTemplateListPresentation
{
    public readonly IReadOnlyList<GameModelTemplatePresentation> PropTemplates;
    public readonly IReadOnlyList<GameModelTemplatePresentation> CharacterTemplates;
    public readonly IReadOnlyList<GameModelTemplatePresentation> ItemTemplates;

    public GameModelTemplateListPresentation(
        IEnumerable<Template> propTemplates, 
        IEnumerable<Template> characterTemplates,
        IReadOnlyList<Template> itemTemplates)
    {
        // Props
        List<GameModelTemplatePresentation> propTemplatePresentations = new();
        foreach (var propTemplate in propTemplates)
        {
            propTemplatePresentations.Add(new GameModelTemplatePresentation(propTemplate, isInDatabase: true));
        }
        PropTemplates = propTemplatePresentations;

        // Characters
        List<GameModelTemplatePresentation> characterTemplatePresentations = new();
        foreach (var characterTemplate in characterTemplates)
        {
            characterTemplatePresentations.Add(new GameModelTemplatePresentation(characterTemplate, isInDatabase: true));
        }
        CharacterTemplates = characterTemplatePresentations;

        // Items
        List<GameModelTemplatePresentation> itemTemplatePresentations = new();
        foreach (var itemTemplate in itemTemplates)
        {
            itemTemplatePresentations.Add(new GameModelTemplatePresentation(itemTemplate, isInDatabase: true));
        }
        ItemTemplates = itemTemplatePresentations;
    }
}

public class GameModelIdentityPresentation
{
    public IGameDbId Id { get; private set; }
    public string Name { get; private set; }
    public string Type { get; private set; }
    public RenderKey RenderKey { get; private set; }
    public bool HasDamageSprites { get; private set; } //HACK
    public CellSize SizeOnMap { get; private set; }

    // HACK
    public GameModelIdentityPresentation(Root model)
    {
        Type = "ROOT";
        Id = null;
        Name = "Root";
    }

    public GameModelIdentityPresentation(IGameDbResolvable model)
    {
        Type = model.GetType().Name;

        if (model is Prop prop)
        {
            Id = prop.Id;
            Name = prop.Name;
            RenderKey = prop.RenderKey;
            HasDamageSprites = true; //HACK
            SizeOnMap = prop.SizeOnMap;
        }

        else if (model is Character character)
        {
            Id = character.Id;
            Name = character.Name;
            //RenderKey = character.RenderKey; // Characters are more complex
            SizeOnMap = character.SizeOnMap;
        }

        else if (model is Item item)
        {
            Id = item.Id;
            Name = item.Name;
            RenderKey = item.RenderKey;
            HasDamageSprites = false; //HACK
            SizeOnMap = item.SizeOnMap;
        }

        else if (model is MapChunk mapChunk)
        {
            Id = mapChunk.Id;
            Name = mapChunk.Name;
        }

        else if (model is World world)
        {
            Id = world.Id;
            Name = world.Name;
        }
    }
}