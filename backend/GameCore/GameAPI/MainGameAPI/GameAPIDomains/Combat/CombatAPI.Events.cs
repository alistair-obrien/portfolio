using System.Collections.Generic;

public sealed partial class CombatAPI
{
    public class Events
    {
        public sealed record PropLayerDamagedEvent(
            MapChunkId MapId,
            PropId PropId,
            PropLayerId LayerId,
            DamageInstancePresentation DamageInstance,
            bool PropWasDestroyed,
            float PropRemainingHPPercentage
            ) : IGameEvent;

        public abstract record Attacked(
            CharacterId AttackerId,
            IReadOnlyList<HitRayPresentation> HitRays
        ) : IGameEvent;

        public record ShotWithGun(
            IReadOnlyList<HitRayPresentation> HitRays,
            CharacterId AttackerId,
            ItemId GunItemId,
            DamageType DamageType
        ) : Attacked(
            AttackerId, 
            HitRays);

        public sealed record ShotWithGunInDirection(
            Vec2 Direction,
            CharacterId AttackerId,
            ItemId GunId,
            MapChunkId MapId,
            IReadOnlyList<HitRayPresentation> HitRays,
            GunStatsPresentation GunStats
        ) : ShotWithGun(
            HitRays,
            AttackerId,
            GunId,
            GunStats.DamageType);
    }
}