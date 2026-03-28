using System.Collections.Generic;


public record ComputedArmorStats
    (IReadOnlyList<ResistanceData> Resistances);

public record GunStatsPresentation
    (int ControlRequired,
    int ControlFromWielder,
    float ControlPercent,
    int AdditionalShotPower,
    int BulletsPerShot,
    FiringStyle firingStyle,
    float SpreadAngleDeg,
    float CloseRangeMultiplier,
    int CloseRangeEnergyOutput,
    float MidRangeMultiplier,
    int MidRangeEnergyOutput,
    float LongRangeMultiplier,
    int LongRangeEnergyOutput,
    int CurrentAmmo,
    int MaxAmmo,
    DamageType DamageType,
    LoadedAmmoPresentation LoadedAmmo);
