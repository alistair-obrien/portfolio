public sealed class EntityMoverAPI : APIDomain
{
    private EntityLocationAPI EntityLocationResolver => GameAPI.Databases.EntityLocationAPI;
    private EntityAttachmentAPI EntityAttachmentOps => GameAPI.Databases.EntityAttachmentAPI;

    public EntityMoverAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    // ─────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────
    public CommandResult TryMoveEntity(ItemsAPI.Commands.MoveEntity request)
    {
        if (request == null)
            return Fail("MoveEntity was called with a null request.");

        return TryMoveEntityInternal(
            request.ActorId,
            request.EntityId,
            request.TargetLocation,
            request.AllowSwap);
    }

    // ─────────────────────────────────────────────────────────────
    // Core Logic
    // ─────────────────────────────────────────────────────────────

    public bool CanMoveItem(
        CharacterId characterId,
        ItemId itemId,
        IItemLocation target,
        bool allowOverlap)
    {

        if (!TryResolve(itemId, out Item item))
            return false;

        EntityLocationResolver.TryFindEntityLocation(itemId, out var source);

        if (source != null && source.Equals(target))
            return true;

        if (!EntityAttachmentOps.CanAttachToItem(characterId, itemId, target, allowOverlap))
            return false;

        return true;
    }

    private CommandResult TryMoveEntityInternal(
        CharacterId actorId,
        IGameDbId entityId,
        IGameModelLocation target,
        bool allowOverlap)
    {
        if (!EntityLocationResolver.TryFindEntityLocation(entityId, out var source))
            return Fail($"Could not find the current location of entity {entityId}.");

        if (!EntityAttachmentOps.TryDetachEntityFromLocation(actorId, source, entityId))
            return Fail($"Failed to detach entity {entityId} from {source}.");

        if (!EntityAttachmentOps.TryAttachEntityToLocation(
            actorId,
            target,
            entityId,
            allowOverlap))
        {
            if (source != null)
            {
                EntityAttachmentOps.TryAttachEntityToLocation(
                    actorId,
                    source,
                    entityId,
                    allowOverlap: false);
            }

            return Fail($"Failed to attach entity {entityId} to {target}.");
        }

        return Ok();
    }
}
