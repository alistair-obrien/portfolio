
public partial class Rulebook
{
    internal readonly GameInstance GameAPI;

    internal readonly ArmorRules ArmorSection;
    internal readonly CombatRules CombatSection;
    internal readonly InjuriesAndDamageRules InjuriesAndDamageSection;
    internal readonly InteractionRules InteractionSection;
    internal readonly MovementRules MovementSection;
    internal readonly StatsRules StatsSection;
    internal readonly StyleRules StyleSection;
    internal readonly WeaponsRules WeaponsSection;
    internal readonly TurnsRules TurnsSection;

    internal Rulebook(GameInstance gameAPI)
    {
        GameAPI = gameAPI;

        ArmorSection = new(this);
        CombatSection = new(this);
        InjuriesAndDamageSection = new(this);
        InteractionSection = new(this);
        MovementSection = new(this);
        StatsSection = new(this);
        StyleSection = new(this);
        WeaponsSection = new(this);
        TurnsSection = new(this);
    }
}