using System.Collections.Generic;

public enum FiringStyle
{
    Default,
    SMG,
    Shotgun
}

// This is saved as JSON and used to re build a Gun
// So this data is very important as a transient state
// Can be used for front end when editing objects too
public record GunSaveData
{
    public ItemId? LoadedAmmoItemId;
    public int ProjectileCountPerShot = 1;
    public float SpreadAngleDeg = 0f;
    public FiringStyle FiringStyle = FiringStyle.Default;
    public float ShotRadiusInTiles = 1f;
    public bool RequiresBothHands = false;
    public int ClipSize = 6;

    public int ControlRequirement = 0;
    public int AdditionalShotEnergy = 0;
    public float CloseRangeEnergyMultiplier = 1f;
    public float MidRangeEnergyMultiplier = 1f;
    public float LongRangeEnergyMultiplier = 1f;

    public GunSaveData() { }
}

public class GunComponent : ItemComponent, IHasGameDbResolvableReferences
{
    public GunSaveData Save()
    {
        return new GunSaveData
        {
            LoadedAmmoItemId = LoadedAmmoItemId,
            ProjectileCountPerShot = ProjectileCountPerShot,
            SpreadAngleDeg = SpreadAngleDeg,
            FiringStyle = FiringStyle,
            ShotRadiusInTiles = ShotRadiusInTiles,
            RequiresBothHands = RequiresBothHands,
            ClipSize = ClipSize,
            ControlRequirement = ControlRequirement,
            AdditionalShotEnergy = AdditionalShotEnergy,
            CloseRangeEnergyMultiplier = CloseRangeEnergyMultiplier,
            MidRangeEnergyMultiplier = MidRangeEnergyMultiplier,
            LongRangeEnergyMultiplier = LongRangeEnergyMultiplier
        };
    }

    public ItemId? LoadedAmmoItemId { get; private set; }
    public int ProjectileCountPerShot { get; private set; } // Some guns shoot multiple bullets which could be distributed across tiles
    public float SpreadAngleDeg { get; private set; } // Thinking maybe to use spread to turn some guns into AoE combined with RoF
    public FiringStyle FiringStyle { get; private set; }
    public float ShotRadiusInTiles { get; private set; }
    public bool RequiresBothHands { get; private set; } // Not yet implemented
    public int ClipSize { get; private set; }

    // BALLISTICS
    public int ControlRequirement { get; private set; } = 0;
    // When upgrading or whatnot
    public int AdditionalShotEnergy { get; private set; } = 0;
    public float CloseRangeEnergyMultiplier { get; private set; } = 1f;
    public float MidRangeEnergyMultiplier { get; private set; } = 1f;
    public float LongRangeEnergyMultiplier { get; private set; } = 1f;

    public GunComponent() { }
    public GunComponent(GunSaveData data)
    {
        ProjectileCountPerShot = data.ProjectileCountPerShot;
        ClipSize = data.ClipSize;
        SpreadAngleDeg = data.SpreadAngleDeg;
        FiringStyle = data.FiringStyle;
        ShotRadiusInTiles = data.ShotRadiusInTiles;
        RequiresBothHands = data.RequiresBothHands;
        AdditionalShotEnergy = data.AdditionalShotEnergy;
        ControlRequirement = data.ControlRequirement;
        CloseRangeEnergyMultiplier = data.CloseRangeEnergyMultiplier;
        MidRangeEnergyMultiplier = data.MidRangeEnergyMultiplier;
        LongRangeEnergyMultiplier = data.LongRangeEnergyMultiplier;
    }

    internal HashSet<ISlotId> GetCompatibleSlotPaths()
    {
        return new HashSet<ISlotId>
        {
            SlotIds.Loadout.PrimaryWeapon,
        };
    }

    public bool HasItemLoadedAsAmmo(ItemId itemId)
    {
        return LoadedAmmoItemId != null && LoadedAmmoItemId == itemId;
    }

    internal bool CanLoadWithAmmo(GameInstance gameApi, Item item)
    {
        if (item == null || !item.GetIsAmmo() || item.Ammo == null)
            return false;

        // --- If gun already has ammo loaded ---
        if (TryGetLoadedAmmo(out var loadedUid))
        {
            if (!gameApi.Databases.TryGetModel(loadedUid, out Item loaded))
                return false;

            // --- SAME TYPE → MERGE INTO EXISTING CLIP ---
            if (loaded.Name == item.Name)
            {
                // Remaining space in clip
                int remaining = ClipSize - loaded.CurrentStackCount;
                if (remaining <= 0) { return false; }
                return true;
            }

            // --- DIFFERENT TYPE → SWAP ---
            else
            {
                return true;
            }
        }

        // --- NO AMMO LOADED YET ---
        else
        {
            return true;
        }
    }

    internal bool TryUnloadAmmo(Item item)
    {
        if (item == null || LoadedAmmoItemId == null)
            return false;

        if (LoadedAmmoItemId != item.Id)
            return false;

        LoadedAmmoItemId = null;
        return true;
    }

    internal bool HasLoadedAmmo() =>
        GetLoadedAmmoCount() > 0;

    internal int GetLoadedAmmoCount()
    {
        return 0;

        //if (LoadedAmmoitemId?.Value?.Ammo == null)
        //    return 0;

        //return Mathf.Max(0, LoadedAmmoitemId.Value.CurrentStackCount);
    }

    internal void ReduceAmmo(int requested, out int amountReduced)
    {
        amountReduced = 0;

        if (!HasLoadedAmmo())
            return;

        //var loadedAmmoItem = LoadedAmmoitemId.Value;
        //int available = Mathf.Max(0, loadedAmmoItem.CurrentStackCount);

        //amountReduced = Mathf.Min(requested, available);
        //loadedAmmoItem.DecreaseStackCount(amountReduced);
    }

    public bool TryGetLoadedAmmo(out ItemId ammoItemId)
    {
        ammoItemId = default;

        if (!LoadedAmmoItemId.HasValue)
            return false;

        ammoItemId = LoadedAmmoItemId.Value;
        return true;
    }

    internal bool TryLoadAmmo(ItemId ammoItemId)
    {
        if (LoadedAmmoItemId.HasValue)
            return false;

        LoadedAmmoItemId = ammoItemId;
        return true;
    }

    public List<IGameDbId> GetChildIdReferences()
    {
        if (LoadedAmmoItemId.HasValue)
        {
            return new List<IGameDbId>
            {
                LoadedAmmoItemId.Value
            };
        }

        return new List<IGameDbId>();
    }

    public void RemapIds(Dictionary<ITypedStringId, ITypedStringId> idMap)
    {
        if (!LoadedAmmoItemId.HasValue)
            return;

        LoadedAmmoItemId = (ItemId)idMap[LoadedAmmoItemId];
    }
}