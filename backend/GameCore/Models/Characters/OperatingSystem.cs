using System.Collections.Generic;
using System.Linq;

public static class OperatingSystemModuleIds
{
    public const string Building = "building";
    public const string Authoring = "authoring";
}

public static class OperatingSystemIds
{
    public const string GenoSys = "genosys";
}

public enum OperatingSystemAccessLevel
{
    None = 0,
    Read = 10,
    Use = 20,
    Write = 30,
    Admin = 40
}

public static class OperatingSystemGrantIds
{
    public static class Interactions
    {
        public const string Move = "interactions.move";
        public const string Build = "interactions.build";
        public const string Talk = "interactions.talk";
        public const string Attack = "interactions.attack";
        public const string DuplicateEntity = "interactions.entity.duplicate";
        public const string DeleteEntity = "interactions.entity.delete";
        public const string DetachEntity = "interactions.entity.detach";
        public const string PossessCharacter = "interactions.character.possess";
    }
}

// Compatibility alias for older code paths that still refer to "capabilities".
public static class OperatingSystemCapabilityIds
{
    public const string InteractionsMove = OperatingSystemGrantIds.Interactions.Move;
    public const string InteractionsBuild = OperatingSystemGrantIds.Interactions.Build;
}

public readonly struct RequiredOperatingSystemGrant
{
    public string Id { get; }
    public OperatingSystemAccessLevel MinimumAccess { get; }

    public RequiredOperatingSystemGrant(string id, OperatingSystemAccessLevel minimumAccess)
    {
        Id = id ?? string.Empty;
        MinimumAccess = minimumAccess;
    }
}

public sealed class OperatingSystemGrantBlueprint
{
    public string Id;
    public OperatingSystemAccessLevel Access = OperatingSystemAccessLevel.Use;

    public OperatingSystemGrantBlueprint()
    {
    }

    public OperatingSystemGrantBlueprint(string id, OperatingSystemAccessLevel access)
    {
        Id = id ?? string.Empty;
        Access = access;
    }
}

public static class OperatingSystemGrantCatalog
{
    private static readonly Dictionary<string, OperatingSystemAccessLevel> _defaultAccessByGrantId =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
            [OperatingSystemGrantIds.Interactions.Move] = OperatingSystemAccessLevel.Use,
            [OperatingSystemGrantIds.Interactions.Build] = OperatingSystemAccessLevel.Write,
            [OperatingSystemGrantIds.Interactions.Talk] = OperatingSystemAccessLevel.Use,
            [OperatingSystemGrantIds.Interactions.Attack] = OperatingSystemAccessLevel.Use,
            [OperatingSystemGrantIds.Interactions.DuplicateEntity] = OperatingSystemAccessLevel.Write,
            [OperatingSystemGrantIds.Interactions.DeleteEntity] = OperatingSystemAccessLevel.Write,
            [OperatingSystemGrantIds.Interactions.DetachEntity] = OperatingSystemAccessLevel.Write,
            [OperatingSystemGrantIds.Interactions.PossessCharacter] = OperatingSystemAccessLevel.Admin,
        };

    private static readonly IReadOnlyList<OperatingSystemGrantBlueprint> _baselineGrants =
        new List<OperatingSystemGrantBlueprint>
        {
            new(OperatingSystemGrantIds.Interactions.Move, OperatingSystemAccessLevel.Use),
            new(OperatingSystemGrantIds.Interactions.Talk, OperatingSystemAccessLevel.Use),
            new(OperatingSystemGrantIds.Interactions.Attack, OperatingSystemAccessLevel.Use)
        };

    public static IReadOnlyList<OperatingSystemGrantBlueprint> BaselineGrants => _baselineGrants;

    public static OperatingSystemAccessLevel GetDefaultAccess(string grantId)
    {
        if (string.IsNullOrWhiteSpace(grantId))
            return OperatingSystemAccessLevel.None;

        return _defaultAccessByGrantId.TryGetValue(grantId, out var access)
            ? access
            : OperatingSystemAccessLevel.Use;
    }

    public static IReadOnlyList<OperatingSystemGrantBlueprint> NormalizeBlueprints(
        IEnumerable<OperatingSystemGrantBlueprint> grants)
    {
        var mergedAccessByGrantId = new Dictionary<string, OperatingSystemAccessLevel>(
            System.StringComparer.OrdinalIgnoreCase);

        foreach (var grant in grants ?? Enumerable.Empty<OperatingSystemGrantBlueprint>())
        {
            if (grant == null || string.IsNullOrWhiteSpace(grant.Id))
                continue;

            if (!mergedAccessByGrantId.TryGetValue(grant.Id, out var existingAccess) ||
                grant.Access > existingAccess)
            {
                mergedAccessByGrantId[grant.Id] = grant.Access;
            }
        }

        return mergedAccessByGrantId
            .Select(kvp => new OperatingSystemGrantBlueprint(kvp.Key, kvp.Value))
            .ToList();
    }

    public static IReadOnlyList<OperatingSystemGrantBlueprint> ConvertLegacyCapabilities(
        IEnumerable<string> capabilityIds)
    {
        return NormalizeBlueprints(
            (capabilityIds ?? Enumerable.Empty<string>())
            .Where(capabilityId => !string.IsNullOrWhiteSpace(capabilityId))
            .Select(capabilityId => new OperatingSystemGrantBlueprint(
                capabilityId,
                GetDefaultAccess(capabilityId))));
    }
}

public sealed class OperatingSystemGrant
{
    public string Id { get; private set; }
    public OperatingSystemAccessLevel Access { get; private set; }

    public OperatingSystemGrant()
    {
        Id = string.Empty;
        Access = OperatingSystemAccessLevel.None;
    }

    public OperatingSystemGrant(OperatingSystemGrantBlueprint blueprint)
    {
        ApplyBlueprint(blueprint);
    }

    public bool Allows(string grantId, OperatingSystemAccessLevel minimumAccess)
    {
        if (string.IsNullOrWhiteSpace(grantId))
            return false;

        return string.Equals(Id, grantId, System.StringComparison.OrdinalIgnoreCase) &&
               Access >= minimumAccess;
    }

    public OperatingSystemGrantBlueprint Save()
    {
        return new OperatingSystemGrantBlueprint(Id, Access);
    }

    public void ApplyBlueprint(OperatingSystemGrantBlueprint blueprint)
    {
        if (blueprint == null)
        {
            Id = string.Empty;
            Access = OperatingSystemAccessLevel.None;
            return;
        }

        Id = blueprint.Id ?? string.Empty;
        Access = blueprint.Access;
    }
}

public sealed class OperatingSystemGrantSet
{
    private readonly Dictionary<string, OperatingSystemAccessLevel> _grants;

    public OperatingSystemGrantSet(IEnumerable<OperatingSystemGrantBlueprint> grants)
    {
        _grants = new Dictionary<string, OperatingSystemAccessLevel>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var grant in OperatingSystemGrantCatalog.NormalizeBlueprints(grants))
        {
            _grants[grant.Id] = grant.Access;
        }
    }

    public bool Has(string grantId, OperatingSystemAccessLevel minimumAccess)
    {
        if (string.IsNullOrWhiteSpace(grantId))
            return false;

        return _grants.TryGetValue(grantId, out var access) && access >= minimumAccess;
    }

    public bool Has(string grantId)
    {
        return Has(grantId, OperatingSystemGrantCatalog.GetDefaultAccess(grantId));
    }

    public bool HasAll(IEnumerable<RequiredOperatingSystemGrant> grants)
    {
        if (grants == null)
            return true;

        foreach (var grant in grants)
        {
            if (!Has(grant.Id, grant.MinimumAccess))
                return false;
        }

        return true;
    }

    public bool HasAllGrantIds(IEnumerable<string> grantIds)
    {
        if (grantIds == null)
            return true;

        foreach (var grantId in grantIds)
        {
            if (!Has(grantId))
                return false;
        }

        return true;
    }

    public IReadOnlyList<string> ToGrantIds()
    {
        return _grants.Keys.ToList();
    }
}

public sealed class OperatingSystemModuleBlueprint
{
    public string Id;
    public bool Enabled = true;
    public List<string> Capabilities = new();
    public List<OperatingSystemGrantBlueprint> Grants = new();

    public OperatingSystemModuleBlueprint()
    {
    }

    public OperatingSystemModuleBlueprint(
        string id,
        bool enabled,
        IEnumerable<OperatingSystemGrantBlueprint> grants)
    {
        Id = id;
        Enabled = enabled;
        Grants = OperatingSystemGrantCatalog.NormalizeBlueprints(grants).ToList();
        Capabilities = Grants.Select(grant => grant.Id).Distinct().ToList();
    }

    public OperatingSystemModuleBlueprint(
        string id,
        bool enabled,
        IEnumerable<string> capabilities)
        : this(id, enabled, OperatingSystemGrantCatalog.ConvertLegacyCapabilities(capabilities))
    {
    }

    public static OperatingSystemModuleBlueprint CreateBuilding(bool enabled = true)
    {
        return new OperatingSystemModuleBlueprint(
            OperatingSystemModuleIds.Building,
            enabled,
            new[]
            {
                new OperatingSystemGrantBlueprint(
                    OperatingSystemGrantIds.Interactions.Build,
                    OperatingSystemAccessLevel.Write)
            });
    }

    public static OperatingSystemModuleBlueprint CreateAuthoring(bool enabled = true)
    {
        return new OperatingSystemModuleBlueprint(
            OperatingSystemModuleIds.Authoring,
            enabled,
            new[]
            {
                new OperatingSystemGrantBlueprint(
                    OperatingSystemGrantIds.Interactions.DuplicateEntity,
                    OperatingSystemAccessLevel.Write),
                new OperatingSystemGrantBlueprint(
                    OperatingSystemGrantIds.Interactions.DeleteEntity,
                    OperatingSystemAccessLevel.Write),
                new OperatingSystemGrantBlueprint(
                    OperatingSystemGrantIds.Interactions.DetachEntity,
                    OperatingSystemAccessLevel.Write),
                new OperatingSystemGrantBlueprint(
                    OperatingSystemGrantIds.Interactions.PossessCharacter,
                    OperatingSystemAccessLevel.Admin)
            });
    }
}

public sealed class OperatingSystemBlueprint
{
    public string Id;
    public List<OperatingSystemModuleBlueprint> Modules = new();
}

public sealed class OperatingSystemModule
{
    private IReadOnlyList<string> _capabilities;

    public string Id { get; private set; }
    public bool Enabled { get; private set; }
    public IReadOnlyList<string> Capabilities => _capabilities;
    public IReadOnlyList<OperatingSystemGrant> Grants { get; private set; }

    public OperatingSystemModule()
    {
        Id = string.Empty;
        Enabled = false;
        _capabilities = new List<string>();
        Grants = new List<OperatingSystemGrant>();
    }

    public OperatingSystemModule(OperatingSystemModuleBlueprint blueprint)
    {
        ApplyBlueprint(blueprint);
    }

    public bool HasCapability(string capabilityId)
    {
        return HasGrant(
            capabilityId,
            OperatingSystemGrantCatalog.GetDefaultAccess(capabilityId));
    }

    public bool HasGrant(string grantId, OperatingSystemAccessLevel minimumAccess)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(grantId))
            return false;

        return Grants.Any(grant => grant.Allows(grantId, minimumAccess));
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
    }

    public OperatingSystemModuleBlueprint Save()
    {
        return new OperatingSystemModuleBlueprint
        {
            Id = Id,
            Enabled = Enabled,
            Capabilities = _capabilities.ToList(),
            Grants = Grants.Select(grant => grant.Save()).ToList()
        };
    }

    public void ApplyBlueprint(OperatingSystemModuleBlueprint blueprint)
    {
        if (blueprint == null)
        {
            Id = string.Empty;
            Enabled = false;
            _capabilities = new List<string>();
            Grants = new List<OperatingSystemGrant>();
            return;
        }

        Id = blueprint.Id ?? string.Empty;
        Enabled = blueprint.Enabled;

        var normalizedGrants = OperatingSystemGrantCatalog.NormalizeBlueprints(
            (blueprint.Grants ?? new List<OperatingSystemGrantBlueprint>())
            .Concat(OperatingSystemGrantCatalog.ConvertLegacyCapabilities(blueprint.Capabilities ?? new List<string>())));

        Grants = normalizedGrants
            .Select(grantBlueprint => new OperatingSystemGrant(grantBlueprint))
            .ToList();

        _capabilities = Grants
            .Select(grant => grant.Id)
            .Distinct()
            .ToList();
    }
}

public sealed class OperatingSystem
{
    private readonly Dictionary<string, OperatingSystemModule> _modules = new();

    public string Id { get; private set; }
    public IReadOnlyCollection<OperatingSystemModule> Modules => _modules.Values;

    public OperatingSystem()
    {
    }

    public OperatingSystem(OperatingSystemBlueprint blueprint)
    {
        ApplyBlueprint(blueprint);
    }

    public bool IsId(string operatingSystemId)
    {
        return !string.IsNullOrWhiteSpace(operatingSystemId) &&
               string.Equals(Id, operatingSystemId, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool HasCapability(string capabilityId)
    {
        return HasGrant(
            capabilityId,
            OperatingSystemGrantCatalog.GetDefaultAccess(capabilityId));
    }

    public bool HasGrant(string grantId, OperatingSystemAccessLevel minimumAccess)
    {
        return _modules.Values.Any(module => module.HasGrant(grantId, minimumAccess));
    }

    public bool TryGetModule(string moduleId, out OperatingSystemModule module)
    {
        module = null;

        if (string.IsNullOrWhiteSpace(moduleId))
            return false;

        return _modules.TryGetValue(moduleId, out module);
    }

    public void SetModuleEnabled(string moduleId, bool enabled)
    {
        if (!_modules.TryGetValue(moduleId, out var module))
            return;

        module.SetEnabled(enabled);
    }

    public void UpsertModule(OperatingSystemModuleBlueprint blueprint)
    {
        if (blueprint == null || string.IsNullOrWhiteSpace(blueprint.Id))
            return;

        _modules[blueprint.Id] = new OperatingSystemModule(blueprint);
    }

    public OperatingSystemBlueprint Save()
    {
        return new OperatingSystemBlueprint
        {
            Id = Id,
            Modules = _modules.Values
                .Select(module => module.Save())
                .ToList()
        };
    }

    public void ApplyBlueprint(OperatingSystemBlueprint blueprint)
    {
        _modules.Clear();
        Id = blueprint?.Id ?? string.Empty;

        if (blueprint?.Modules == null)
            return;

        foreach (var moduleBlueprint in blueprint.Modules)
        {
            if (moduleBlueprint == null || string.IsNullOrWhiteSpace(moduleBlueprint.Id))
                continue;

            _modules[moduleBlueprint.Id] = new OperatingSystemModule(moduleBlueprint);
        }
    }
}
