public partial class Rulebook
{
    internal class TurnsRules : RulebookSection
    {
        public TurnsRules(Rulebook rulebook) : base(rulebook) { }

        internal bool CanAddCharacterToTurnQueue(Character character)
        {
            if (Rulebook.CombatSection.IsCharacterDead(character)) { return false; }
            return true;
        }
    }
}