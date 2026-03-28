public sealed class ItemStackAPI : APIDomain
{
    private ItemsAPI Items => GameAPI.Items;
    private EntityLocationAPI Locations => GameAPI.Databases.EntityLocationAPI;
    private EntityMoverAPI Mover => GameAPI.Databases.EntityMoverAPI;
    private EntityAttachmentAPI Attachments => GameAPI.Databases.EntityAttachmentAPI;

    public ItemStackAPI(GameInstance gameAPI) : base(gameAPI) { }

    // ─────────────────────────────────────────────
    // Validation
    // ─────────────────────────────────────────────

    internal bool CanMergeStacks(Character actor, Item source, Item target)
    {
        return source != target &&
               source.CurrentStackCount > 0 &&
               target.CurrentStackCount < target.MaxStackCount &&
               source.MaxStackCount > 1 &&
               target.MaxStackCount > 1 &&
               Items.ItemsAreSameType(source, target);
    }

    // ─────────────────────────────────────────────
    // Split
    // ─────────────────────────────────────────────

    internal CommandResult SplitStack(ItemsAPI.Commands.SplitStack req)
    {
        if (!TryResolve(req.ItemId, out Item source))
            return Fail($"Could not resolve source item {req.ItemId} to split.");

        if (source.MaxStackCount <= 1 || source.CurrentStackCount < 2)
            return Fail($"Item {req.ItemId} cannot be split because it does not have a splittable stack.");

        int amount = req.AmountToSplit ?? source.CurrentStackCount / 2;

        if (amount <= 0 || amount >= source.CurrentStackCount)
            return Fail($"Requested split amount {amount} is invalid for item {req.ItemId}.");

        if (!GameAPI.Databases.TryDuplicateModel(source.Id, out var weakId))
            return Fail($"Failed to duplicate stack source item {req.ItemId} for split.");

        var splitItemId = (ItemId)weakId;

        if (!GameAPI.Databases.TryResolve(splitItemId, out Item split))
        {
            GameAPI.Databases.TryRemoveModel(splitItemId);
            return Fail($"Split clone {splitItemId} could not be resolved after duplication.");
        }

        // mutate AFTER resolve succeeds
        source.DecreaseStackCount(amount);
        split.SetStackAmount(amount);

        var moveResult = Mover.TryMoveEntity(new ItemsAPI.Commands.MoveEntity(req.ActorId, splitItemId, req.targetLocation, false));
        if (!moveResult.Ok)
        {
            source.IncreaseStackCount(amount);
            GameAPI.Databases.TryRemoveModel(splitItemId);
            return Fail($"Failed to move split item {splitItemId} to {req.targetLocation}: {moveResult.ErrorMessage}");
        }

        RaiseItemUpdated(source.Id);
        return Ok();
    }

    // ─────────────────────────────────────────────
    // Merge
    // ─────────────────────────────────────────────

    internal CommandResult MergeStack(ItemsAPI.Commands.MergeStack req)
    {
        if (!TryResolve(req.SourceItemId, out Item source))
            return Fail($"Could not resolve source item {req.SourceItemId}.");

        if (!TryResolve(req.TargetItemId, out Item target))
            return Fail($"Could not resolve target item {req.TargetItemId}.");

        if (!TryResolve(req.ActorId, out Character actor))
            return Fail($"Could not resolve actor {req.ActorId} for stack merge.");

        if (!CanMergeStacks(actor, source, target))
            return Fail($"Items {req.SourceItemId} and {req.TargetItemId} cannot be merged.");

        int limit = req.MaxTransferAmount ?? target.MaxStackCount;
        int allowed = Mathf.Max(0, limit - target.CurrentStackCount);
        int move = Mathf.Min(allowed, source.CurrentStackCount);

        if (move == 0)
            return Fail($"No items can be transferred from {req.SourceItemId} into {req.TargetItemId}.");

        Locations.TryFindEntityLocation(source.Id, out var srcLoc);
        Locations.TryFindEntityLocation(target.Id, out var tgtLoc);

        if (srcLoc is not IItemLocation srcItemLoc)
            return Fail($"Source item {req.SourceItemId} is not in an item location.");

        if (tgtLoc is not IItemLocation tgtItemLoc)
            return Fail($"Target item {req.TargetItemId} is not in an item location.");

        if (!DetachIfNeeded(req.ActorId, srcItemLoc, source.Id))
            return Fail($"Failed to detach source item {req.SourceItemId} before merging.");

        target.IncreaseStackCount(move);
        source.DecreaseStackCount(move);

        if (source.CurrentStackCount == 0)
        {
            GameAPI.Databases.TryRemoveModel(source.Id);
            return Ok();
        }

        if (!ReattachIfNeeded(req.ActorId, srcItemLoc, source.Id))
        {
            // rollback
            target.DecreaseStackCount(move);
            source.IncreaseStackCount(move);
            ReattachIfNeeded(req.ActorId, srcItemLoc, source.Id);
            return Fail($"Failed to reattach source item {req.SourceItemId} after merge.");
        }

        RaiseItemUpdated(source.Id);
        RaiseItemUpdated(target.Id);
        return Ok();
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    private bool DetachIfNeeded(CharacterId actorId, IItemLocation loc, ItemId itemId)
        => loc == null || Attachments.TryDetachEntityFromLocation(actorId, loc, itemId);

    private bool ReattachIfNeeded(CharacterId actorId, IItemLocation loc, ItemId itemId)
        => loc == null || Attachments.TryAttachEntityToLocation(actorId, loc, itemId, false);

    private void RaiseItemUpdated(ItemId itemId)
    {
        RaiseEvent(new ItemsAPI.Events.ItemUpdated(
            itemId,
            new ItemPresentation(GameAPI, itemId)));
    }
}
