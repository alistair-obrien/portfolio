using System.Collections.Generic;

public sealed class DamageDomain
{
    private static readonly Dictionary<string, DamageDomain> registry = new();

    public static readonly DamageDomain None = Register("none");
    public static readonly DamageDomain Organic = Register("organic");
    public static readonly DamageDomain Cybernetic = Register("cybernetic");
    public static readonly DamageDomain Structural = Register("structural");

    public string Id { get; }

    public DamageDomain() { }

    private DamageDomain(string id) { Id = id; }

    public static DamageDomain Register(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new System.ArgumentNullException(nameof(id));

        var d = new DamageDomain(id);
        registry[id] = d;
        return d;
    }

    public static DamageDomain FromId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return None;

        if (registry.TryGetValue(id, out var d))
            return d;
        return new DamageDomain(id);
    }
}

public sealed class DamageType
{
    private static readonly Dictionary<string, DamageType> registry = new();

    public static readonly DamageType None = Register("none");
    public static readonly DamageType Piercing = Register("piercing");
    public static readonly DamageType Impact = Register("impact");
    public static readonly DamageType Slashing = Register("slashing");

    public string Id { get; }

    public DamageType() { }

    private DamageType(string id) { Id = id; }

    public static DamageType Register(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new System.ArgumentNullException(nameof(id));
        var d = new DamageType(id);
        registry[id] = d;
        return d;
    }

    public static DamageType FromId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return None;

        if (registry.TryGetValue(id, out var d))
            return d;
        return new DamageType(id);
    }
}