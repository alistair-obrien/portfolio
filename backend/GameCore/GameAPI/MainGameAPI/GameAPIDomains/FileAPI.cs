using System;
using System.IO;
using System.Linq;

public class FileAPI : APIDomain
{
    IGameSerializer GameSerializer => GameAPI.GameSerializer;

    public FileAPI(GameInstance gameAPI) : base(gameAPI)
    {

    }

    internal override void RegisterHandlers(CommandRouter router)
    {

    }

    private string GetFullPath(string relativePath)
    {
        return Path.Combine(Application.persistentDataPath, relativePath);
    }

    public string SerializeObject(object toSerialize)
    {
        return GameSerializer.Serialize(toSerialize);
    }

    private bool SaveTextToDisk(string text, string relativePath)
    {
        if (text == null)
            return false;

        var fullPath = GetFullPath(relativePath);

        var directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, text);
        return true;
    }

    public T DeserializeObject<T>(string json)
    {
        return GameSerializer.Deserialize<T>(json);
    }

    private bool TryLoadTextFromDisk(string relativePath, out string json)
    {
        var fullPath = GetFullPath(relativePath);

        json = null;

        if (!File.Exists(fullPath))
            return false;

        json = File.ReadAllText(fullPath);
        return true;
    }

    internal bool TrySaveToDisk<T>(T objectToSave, string fileName)
    {
        try
        {
            var text = SerializeObject(objectToSave);
            SaveTextToDisk(text, fileName);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(e);
            return false;
        }
    }

    internal bool TryLoadFromDisk<T>(string fileName, out T result)
    {
        result = default;
        if (TryLoadTextFromDisk(fileName, out var json))
        {
            try
            {
                result = DeserializeObject<T>(json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(e.ToString());
            }
        }

        return false;
    }

    public void EnsureDirectory(string relativeFolder)
    {
        var fullPath = GetFullPath(relativeFolder);

        if (!Directory.Exists(fullPath))
            Directory.CreateDirectory(fullPath);
    }

    public bool DirectoryExists(string relativeFolder)
    {
        return Directory.Exists(GetFullPath(relativeFolder));
    }

    public string[] GetFiles(string relativeFolder)
    {
        var fullPath = GetFullPath(relativeFolder);

        if (!Directory.Exists(fullPath))
            return Array.Empty<string>();

        var fullFiles = Directory.GetFiles(fullPath, "*.json");

        return fullFiles
            .Select(f => Path.GetRelativePath(Application.persistentDataPath, f))
            .ToArray();
    }

    public void DeleteFile(string relativePath)
    {
        var fullPath = GetFullPath(relativePath);

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }
}