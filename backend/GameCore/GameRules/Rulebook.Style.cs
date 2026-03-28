
public partial class Rulebook
{
    internal class StyleRules : RulebookSection
    {
        public StyleRules(Rulebook rulebook) : base(rulebook)
        {
        }

        class StyleIds
        {
            //How the player presents themselves to the world
            class Aethetic 
            {
                const string Dandy = "dandy";
                const string Street = "street";
                const string Corporate = "corporate";
                const string Noir = "noir";
                const string Techwear = "techwear";
                const string Militarized = "militarized";
                const string Uniformed = "uniformed";
            }

            //What the clothes let you do
            class Traits
            {
                const string Authority = "authority";          // Police, corps, military  - Issue commands, bypass security, detain NPCs
                const string Threatening = "threatening";      // Fear-based dialogue      - Combat openers, crowd control
                const string Wealthy = "wealthy";              // High-status, luxury      - Bribes, exclusive vendors, luxury services
                const string Shabby = "shabby";                // Worn, patched, old-world - Blend in, avoid attention, street sympathy
                const string Professional = "professional";    // Clean, reliable          - Reduced cooldowns, higher success rates
                const string Experimental = "experimental";    // Prototype tech           - Unstable but powerful abilities
                const string Minimalist = "minimalist";        // Clean lines, GitS        - Efficiency buffs, stealth, lower detection
                const string Flashy = "flashy";                // Neon, bold colors        - Aggro manipulation, morale effects, distractions
                const string Cool = "cool";
                const string Seductive = "seductive";
            }

            // Who the clothes signal loyalty to. NOTE: Probably we dont need these. We will use the id of the actual faction
            // This is just a placeholder while designing the system
            class Affiliation
            {
                const string Corpo = "corpo";
                const string Gang = "gang";
                const string Military = "military";
                const string Police = "police";
                const string Underground = "underground";
                const string Brand = "brand";
            }
        }
    }
}