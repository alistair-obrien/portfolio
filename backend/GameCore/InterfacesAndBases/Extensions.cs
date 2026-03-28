
using System;
using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
    public static List<TOut> ConvertAllToList<TIn, TOut>(
        this IEnumerable<TIn> source,
        Func<TIn, TOut> converter)
        => source?.Select(converter).ToList() ?? new();

    public static T Clone<T>(this T obj)
        where T : ICustomSerialization
    {
        var packet = obj.SaveAsPacket();
        var clone = SaveLoaderRegistry.Load<T>(packet);
        
        if (clone is IPostCloneInitialize init)
            init.OnPostClone();

        return clone;
    }

    public static SavePacket Clone(this SavePacket obj)
    {
        var clone = SaveLoaderRegistry.LoadUntyped(obj);

        if (clone is IPostCloneInitialize init)
            init.OnPostClone();

        if (clone is not ICustomSerialization cust)
            return null;

        return cust.SaveAsPacket();
    }
}