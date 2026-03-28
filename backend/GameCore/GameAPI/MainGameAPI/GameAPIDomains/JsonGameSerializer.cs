using Newtonsoft.Json;

// TODO:
//public class ObsidianMDSerializer : IGameSerializer
//{
//    public T Deserialize<T>(string data)
//    {
        
//    }

//    public string Serialize(object obj)
//    {

//    }
//}

public class JsonGameSerializer : IGameSerializer
{
    private JsonSerializerSettings _settings;

    public JsonGameSerializer()
    {
        _settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            Formatting = Formatting.Indented,
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            ReferenceLoopHandling = ReferenceLoopHandling.Error,
            NullValueHandling = NullValueHandling.Include,
        };
        TypedIdTypeRegistry.EnsureInitialized();
    }

    public string Serialize(object obj)
    {
        return JsonConvert.SerializeObject(obj, _settings);
    }

    public T Deserialize<T>(string data)
    {
        var result = JsonConvert.DeserializeObject<T>(data, _settings);
        return result;
    }
}