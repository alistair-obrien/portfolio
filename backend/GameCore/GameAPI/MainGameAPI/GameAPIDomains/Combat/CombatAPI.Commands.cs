public sealed partial class CombatAPI
{
    public class Commands
    {
        public record AttackInDirection(
            CharacterId AttackerId,
            ItemId WeaponItemId,
            float directionX,
            float directionY)
        : IGameCommand;

        public sealed record AttackTarget(
            CharacterId AttackerId,
            CharacterId TargetId,
            ItemId WeaponItemId)
        : IGameCommand;
    }
}
