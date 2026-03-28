using System;
using System.Collections.Generic;

public static class SaveLoaderRegistry
{
    private interface ILoader
    {
        object Load(object data, int version);
        Type OutputType { get; }
    }

    private class Loader<T> : ILoader
    {
        private readonly Func<T, int, object> _loader;

        public Loader(Func<T, int, object> loader)
        {
            _loader = loader;
        }

        public object Load(object data, int version)
        {
            object converted = data switch
            {
                T typed => typed,
                Newtonsoft.Json.Linq.JObject jobj => jobj.ToObject<T>(),
                _ => throw new Exception(
                    $"Invalid save data type. Expected {typeof(T).Name} but got {data?.GetType().Name}")
            };

            return _loader((T)converted, version);
        }

        public Type OutputType => typeof(T);
    }

    private static readonly Dictionary<string, ILoader> _loaders = new();
    private static readonly Dictionary<Type, ILoader> _loadersFromSaveDataType = new();

    public static void Register<T>(string id, Func<T, int, object> loader)
    {
        var newLoader = new Loader<T>(loader);

        _loaders.TryAdd(id, newLoader);
        _loadersFromSaveDataType.TryAdd(typeof(T), newLoader);

        //if (!_loaders.TryAdd(id, new Loader<T>(loader)))
            //throw new Exception($"{id} already registered.");
    }

    public static T Load<T>(SavePacket packet)
    {
        var obj = LoadUntyped(packet);

        if (obj is not T typed)
            throw new Exception($"Loaded object is the wrong type. Expected {typeof(T).Name} but go {obj.GetType().Name}");

        return typed;
    }

    public static object LoadUntyped(SavePacket packet)
    {
        if (!_loaders.TryGetValue(packet.TypeId, out var loader))
            throw new Exception($"No loader registered for {packet.TypeId}");

        return loader.Load(packet.Data, packet.Version);
    }

    public static object LoadUntyped(IBlueprint saveData)
    {
        if (!_loadersFromSaveDataType.TryGetValue(saveData.GetType(), out var loader))
            throw new Exception($"No loader registered for {saveData.GetType()}");

        return loader.Load(saveData, 1);
    }
}