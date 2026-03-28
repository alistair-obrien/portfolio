public partial class Rulebook
{
    internal class MovementRules : RulebookSection
    {
        internal const int EnergyPerPushTile = 5;
        internal const int MaxPushTiles = 3;

        public MovementRules(Rulebook rulebook) : base(rulebook)
        {
        }

        // CONVERT ENERGY INTO PUSH
        // Converts remaining energy into push.
        // Any non-zero energy results in at least push 1.
        // Each additional EnergyPerPushTile increases push by 1, capped at MaxPushTiles.
        internal int GetPushTilesFromEnergy(float energy)
        {
            if (energy <= 0)
                return 0;

            int level = Mathf.FloorToInt(energy / EnergyPerPushTile) + 1;
            return Mathf.Clamp(level, 0, MaxPushTiles);
        }
    }
}