using System.Collections.Generic;
using System.Linq;

public sealed class OperatingSystemsAPI : APIDomain
{
    public sealed class Commands
    {
        public sealed record SetModuleEnabled(
            CharacterId CharacterId,
            string ModuleId,
            bool Enabled) : IGameCommand;
    }

    public sealed class Events
    {
        public sealed record ModuleEnabledChanged(
            CharacterId CharacterId,
            string ModuleId,
            bool Enabled) : IGameEvent;
    }

    public OperatingSystemsAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    internal override void RegisterHandlers(CommandRouter router)
    {
        router.Register<Commands.SetModuleEnabled>(HandleSetModuleEnabled);
    }

    public bool HasCapability(CharacterId actorId, string capabilityId)
    {
        return ResolveGrantSet(actorId).Has(capabilityId);
    }

    public bool HasGrant(
        CharacterId actorId,
        string grantId,
        OperatingSystemAccessLevel minimumAccess)
    {
        return ResolveGrantSet(actorId).Has(grantId, minimumAccess);
    }

    public bool HasAllCapabilities(
        CharacterId actorId,
        IEnumerable<string> capabilityIds)
    {
        return ResolveGrantSet(actorId).HasAllGrantIds(capabilityIds);
    }

    public bool HasAllGrants(
        CharacterId actorId,
        IEnumerable<RequiredOperatingSystemGrant> grants)
    {
        return ResolveGrantSet(actorId).HasAll(grants);
    }

    public IReadOnlyList<string> GetCapabilities(CharacterId actorId)
    {
        return ResolveGrantSet(actorId).ToGrantIds();
    }

    public OperatingSystemGrantSet ResolveGrantSet(CharacterId actorId)
    {
        var grants = new List<OperatingSystemGrantBlueprint>(
            OperatingSystemGrantCatalog.BaselineGrants);

        if (!GameAPI.Databases.TryGetModel(actorId, out Character actor))
            return new OperatingSystemGrantSet(grants);

        foreach (var module in actor.OperatingSystem.Modules)
        {
            if (!module.Enabled)
                continue;

            grants.AddRange(module.Grants.Select(grant => grant.Save()));
        }

        return new OperatingSystemGrantSet(grants);
    }

    private CommandResult HandleSetModuleEnabled(Commands.SetModuleEnabled command)
    {
        if (!GameAPI.Databases.TryGetModel(command.CharacterId, out Character actor))
            return Fail($"Could not resolve character {command.CharacterId} to toggle module '{command.ModuleId}'.");

        if (!actor.TrySetOperatingSystemModuleEnabled(command.ModuleId, command.Enabled))
            return Fail($"Character {command.CharacterId} does not have module '{command.ModuleId}' or it could not be set to {command.Enabled}.");

        RaiseEvent(new Events.ModuleEnabledChanged(command.CharacterId, command.ModuleId, command.Enabled));
        return Ok();
    }
}
