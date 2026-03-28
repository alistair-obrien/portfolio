using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Registry for database ID types, mapping prefixes to concrete IDbId implementations.
/// </summary>
public static class TypedIdTypeRegistry
{
    private static readonly Dictionary<string, Func<string, ITypedStringId>> PrefixToConstructor = new();
    private static readonly Dictionary<Type, string> TypeToPrefix = new();
    private static readonly object Lock = new();
    private static bool _initialized;

    /// <summary>
    /// Manually registers a database ID type with a prefix.
    /// </summary>
    public static void Register<T>(string prefix, Func<string, T> constructor)
        where T : ITypedStringId
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix cannot be null or whitespace", nameof(prefix));
        if (constructor == null)
            throw new ArgumentNullException(nameof(constructor));

        lock (Lock)
        {
            if (PrefixToConstructor.ContainsKey(prefix))
                return;
                //throw new InvalidOperationException(
                //    $"Prefix '{prefix}' is already registered");

            PrefixToConstructor[prefix] = value => constructor(value);
            TypeToPrefix[typeof(T)] = prefix;
        }
    }

    /// <summary>
    /// Ensures all IDbId types in loaded assemblies are registered.
    /// This is idempotent and thread-safe.
    /// </summary>
    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        lock (Lock)
        {
            if (_initialized)
                return;

            _initialized = true;
            AutoRegisterAll();
        }
    }

    public static string GetPrefix(Type type)
    {
        EnsureInitialized();

        lock (Lock)
        {
            if (!TypeToPrefix.TryGetValue(type, out var prefix))
                throw new InvalidOperationException(
                    $"Type '{type.FullName}' is not registered as a DbId");

            return prefix;
        }
    }

    public static ITypedStringId Create(string prefix, string value)
    {
        EnsureInitialized();

        lock (Lock)
        {
            if (!PrefixToConstructor.TryGetValue(prefix, out var constructor))
                throw new JsonSerializationException(
                    $"Unknown DbId prefix '{prefix}'");

            return constructor(value);
        }
    }

    private static void AutoRegisterAll()
    {
        var dbIdInterface = typeof(IDbId);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var types = GetTypesFromAssembly(assembly);

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                    continue;

                if (!dbIdInterface.IsAssignableFrom(type))
                    continue;

                var constructor = type.GetConstructor(new[] { typeof(string) });
                if (constructor == null)
                    continue;

                var prefix = DerivePrefix(type);
                RegisterDynamic(type, prefix, constructor);
            }
        }
    }

    private static Type[] GetTypesFromAssembly(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }

    private static void RegisterDynamic(
        Type type,
        string prefix,
        ConstructorInfo constructor)
    {
        if (PrefixToConstructor.ContainsKey(prefix))
            throw new InvalidOperationException(
                $"Prefix '{prefix}' is already registered (conflict with type '{type.FullName}')");

        PrefixToConstructor[prefix] = value =>
            (IDbId)constructor.Invoke(new object[] { value });

        TypeToPrefix[type] = prefix;
    }

    private static string DerivePrefix(Type type)
    {
        var name = type.Name;

        if (name.EndsWith("Id", StringComparison.Ordinal))
            name = name.Substring(0, name.Length - 2);

        return name.ToLowerInvariant();
    }
}

/// <summary>
/// JSON converter for IDbId that serializes as "prefix:value" strings.
/// </summary>
public sealed class PolymorphicDbIdConverter : JsonConverter<ITypedStringId>
{
    private const char Separator = ':';

    public override void WriteJson(JsonWriter writer, ITypedStringId value, JsonSerializer serializer)
    {
        if (value == null)
        {
            writer.WriteNull();
            return;
        }

        var prefix = TypedIdTypeRegistry.GetPrefix(value.GetType());
        writer.WriteValue($"{prefix}{Separator}{value.Value}");
    }

    public override ITypedStringId ReadJson(
        JsonReader reader,
        Type objectType,
        ITypedStringId existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException(
                $"Expected String token but got {reader.TokenType}");

        var str = (string)reader.Value;
        var separatorIndex = str.IndexOf(Separator);

        if (separatorIndex <= 0)
            throw new JsonSerializationException(
                $"Invalid DbId format: '{str}'. Expected 'prefix:value'");

        var prefix = str.Substring(0, separatorIndex);
        var value = str.Substring(separatorIndex + 1);

        return TypedIdTypeRegistry.Create(prefix, value);
    }
}

/// <summary>
/// Base interface for entities that can be resolved from a database.
/// </summary>
public interface IDbResolvable
{
    IDbId Id { get; }

    //IDbResolvable Save();
    //public object Save();
    //public static IDbResolvable Load(object)
}

[JsonConverter(typeof(PolymorphicDbIdConverter))]
public interface ITypedStringId
{
    string Value { get; }
    bool IsValid { get; }
    ITypedStringId NewOfSameType();
    ITypedStringId NewOfSameType(string value);
}

/// <summary>
/// Base interface for database identifiers.
/// </summary>
public interface IDbId : ITypedStringId
{

}


/// <summary>
/// Database ID for template entities.
/// </summary>
public interface ITemplateDbId : IDbId
{

}

/// <summary>
/// Database ID for game entities.
/// </summary>
public interface IGameDbId : IDbId
{

}

/// <summary>
/// Database resolvable entity for games.
/// </summary>
public interface IGameDbResolvable : IDbResolvable
{
    new IGameDbId Id { get; }
    string Name { get; }
    IGameModelLocation AttachedLocation { get; }

    void ClearAttachedLocation();
    void SetAttachedLocation(IGameModelLocation targetLocation);
    void ReattachReferences(IGameDbResolvable originalModel);
    void AttachEntities(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict);
    void ApplyBlueprint(IBlueprint blueprint);
    IEnumerable<AttachmentChange> GetAttachmentChanges(IBlueprint blueprint, Dictionary<IGameDbId, IGameDbResolvable> databaseDict);
}

public interface IHasAttachments
{
    IEnumerable<IGameDbId> GetAttachedEntityIds();
}

public interface IHasReferences
{
    IEnumerable<IGameDbId> GetReferencedEntityIds();
}

public abstract class Saveable : ICustomSerialization
{
    public ITemplateDbId SourceTemplateId { get; private set; }
    protected abstract int Version { get; }

    protected abstract string TypeId { get; }
    //public abstract object SaveUntyped();
    public abstract SavePacket SaveAsPacket();

    public bool IsTemplateLinked => SourceTemplateId != null;

    public void SetTemplate(ITemplateDbId templateDbId)
    {
        SourceTemplateId = templateDbId;
    }

    public void DetachFromTemplate()
    {
        SourceTemplateId = null;
    }

}

public abstract class Entity<TData> : Saveable
{
    public abstract TData SaveToBlueprint();
    public override SavePacket SaveAsPacket() => new(TypeId, Version, SaveToBlueprint(), SourceTemplateId);
}

public sealed class SavePacket
{
    public string TypeId;
    public int Version;
    public object Data;
    public ITemplateDbId SourceTemplateId = null;
    
    public SavePacket()
    {

    }
    
    public SavePacket(IBlueprint x)
    {
        TypeId = x.TypeId;
        Version = 1;
        Data = x;
    }

    public SavePacket(
        string typeId, 
        int version,
        object data, 
        ITemplateDbId sourceTemplateId)
    {
        TypeId = typeId;
        Version = version;
        Data = data;
        SourceTemplateId = sourceTemplateId;
    }
}

public interface ICustomSerialization
{
    SavePacket SaveAsPacket();
}

/// <summary>
/// Interface for entities that contain references to game database entities.
/// </summary>
public interface IHasGameDbResolvableReferences
{
    /// <summary>
    /// Returns all child database IDs that this entity references.
    /// </summary>
    List<IGameDbId> GetChildIdReferences();

    /// <summary>
    /// Remaps IDs according to the provided mapping (e.g., for cloning operations).
    /// </summary>
    void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap);
}