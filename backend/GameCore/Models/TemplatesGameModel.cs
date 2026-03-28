using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Root Game Model is the Game State and is remade completely in save/load/other event
// But we want templates to stick around
public sealed class TemplatesGameModel
{
    public ModelDatabase<Template, ItemTemplateId> ItemTemplatesDb { get; private set; } = new();
    public ModelDatabase<Template, CharacterTemplateId> CharacterTemplatesDb { get; private set; } = new();
    public ModelDatabase<Template, MapChunkTemplateId> MapTemplatesDb { get; private set; } = new();
    public ModelDatabase<Template, PropTemplateId> PropTemplatesDb { get; private set; } = new();

    private Dictionary<Type, IUntypedModelDatabase> _databasesByIdType;

    public TemplatesGameModel()
    {
        BuildDatabaseByIdMap();
    }

    public bool SaveDatabasesToDisk(FileAPI fileApi)
    {
        SaveDatabase(fileApi, "Templates/Items", ItemTemplatesDb);
        SaveDatabase(fileApi, "Templates/Characters", CharacterTemplatesDb);
        SaveDatabase(fileApi, "Templates/Maps", MapTemplatesDb);
        SaveDatabase(fileApi, "Templates/Props", PropTemplatesDb);

        return true;
    }

    internal bool LoadDatabasesFromDisk(FileAPI file)
    {
        LoadDatabase(file, "Templates/Items", ItemTemplatesDb);
        LoadDatabase(file, "Templates/Characters", CharacterTemplatesDb);
        LoadDatabase(file, "Templates/Maps", MapTemplatesDb);
        LoadDatabase(file, "Templates/Props", PropTemplatesDb);

        return true;
    }

    private void SaveDatabase<TId>(
        FileAPI fileApi,
        string folder,
        ModelDatabase<Template, TId> database)
        where TId : IDbId
    {
        fileApi.EnsureDirectory(folder);

        // Collect existing filenames only (normalized)
        var existingFiles = new HashSet<string>(
            fileApi.GetFiles(folder)
                   .Select(f => Path.GetFileName(f).ToLowerInvariant())
        );

        foreach (var model in database.GetAllModels())
        {
            // Normalize filename
            var fileName = $"{model.Id}.json".ToLowerInvariant();
            var fullPath = Path.Combine(folder, fileName);

            // Remove from orphan list
            existingFiles.Remove(fileName);

            //var packet = model.SaveAsPacket();
            fileApi.TrySaveToDisk(model.SaveToBlueprint(), fullPath);
        }

        // Delete true orphan files
        foreach (var orphanFile in existingFiles)
        {
            var orphanFullPath = Path.Combine(folder, orphanFile);
            fileApi.DeleteFile(orphanFullPath);
        }
    }

    public void LoadDatabase<TId>(
        FileAPI fileApi,
        string folder,
        ModelDatabase<Template, TId> database)
    where TId : IDbId
    {
        database.Clear(); // <-- add this

        if (!fileApi.DirectoryExists(folder))
            return;

        var files = fileApi.GetFiles(folder);

        foreach (var file in files)
        {
            if (!fileApi.TryLoadFromDisk(file, out TemplateSaveData saveData))
                continue;

            try
            {
                Template template = new Template(saveData);
                database.TryAddModel(template);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"Failed loading template file {file}: {e}");
            }
        }
    }



    void BuildDatabaseByIdMap()
    {
        _databasesByIdType = new Dictionary<Type, IUntypedModelDatabase>
        {
            { typeof(ItemTemplateId), ItemTemplatesDb },
            { typeof(CharacterTemplateId), CharacterTemplatesDb },
            { typeof(MapChunkTemplateId), MapTemplatesDb },
            { typeof(PropTemplateId), PropTemplatesDb }
        };
    }

    public IUntypedModelDatabase GetTemplateDatabaseById(ITemplateDbId id)
    {
        if (id == null)
            return null;

        if (_databasesByIdType.TryGetValue(id.GetType(), out var db))
            return db;

        throw new InvalidOperationException(
            $"No template database registered for id type {id.GetType().Name}");
    }

    internal ModelDatabase<Template, TId> GetTemplateDatabase<TId>()
        where TId : ITemplateDbId
    {
        if (_databasesByIdType.TryGetValue(typeof(TId), out var db))
            return db as ModelDatabase<Template, TId>;

        throw new InvalidOperationException(
            $"No template database registered for id type {typeof(TId).Name}");
    }

    internal ITemplateDbId GenerateTemplateId(IGameDbId instanceId)
    {
        switch (instanceId)
        {
            case ItemId:
                return ItemTemplateId.New();
            case MapChunkId:
                return MapChunkTemplateId.New();
            case CharacterId:
                return CharacterTemplateId.New();
            case PropId:
                return PropTemplateId.New();
            default:
                break;
        }

        return null;
    }

    // Gross
    internal IReadOnlyList<Template> GetAllTemplates()
    {
        List<Template> templates = new List<Template>();
        foreach (var database in _databasesByIdType)
        {
            var x = database.Value.GetAllModels().ConvertAllToList(x => x as Template);
            templates.AddRange(x);
        }
        return templates;
    }
}