public class CharacterStatsSaveData
{
    public int Physique;
    public int Reflexes;
    public int Technical;
    public int Grit;
    public int Charisma;

    public float Hunger;
    public float Thirst;
    public float Fatigue;
    public float Intoxication;
    public float Humanity;

    public CharacterStatsSaveData()
    {

    }
}


public class CharacterStatsComponent : BaseEntity
{
    internal CharacterStatsSaveData Save()
    {
        return new CharacterStatsSaveData
        {
            Physique = Physique,
            Reflexes = Reflexes,
            Technical = Technical,
            Grit = Grit,
            Charisma = Charisma,

            Hunger = Hunger,
            Thirst = Thirst,
            Fatigue = Fatigue,
            Intoxication = Intoxication,
            Humanity = Humanity
        };
    }

    internal static CharacterStatsComponent Load(CharacterStatsSaveData data)
    {
        return new CharacterStatsComponent(data);
    }

    // === Attributes ===
    public int Physique { get; private set; }
    // Max HP, Melee Damage and Unarmed Damage, Heavy Weapon Handling, Knockback and Stagger Resistance, Carry Weight & Recoil Control
    public int Reflexes { get; private set; }
    // Dodge Chance, Weapon Accuracy, Stealth Movement Noise
    public int Technical { get; private set; }
    // Weapon and armor modding, Crafting and Repair Chance, anti virus
    public int Grit { get; private set; }
    // Willpower, resistance to cyberpsychosis, resistance to toxins, drug tolerance
    public int Charisma { get; private set; }
    // Increased style, intimidation, charm, social engineering

    // === Organic Tick Vars ===
    public float Hunger { get; private set; } // Eat food to prevent hunger imposing negative effects
    public float Thirst { get; private set; } // Drink fluids to prevent thirst imposing negative effects
    public float Fatigue { get; private set; } // Rest/Sleep to prevent fatigue imposing negative effects
    public float Intoxication { get; private set; } // How much bio toxins are in the body
    public float Humanity { get; private set; }

    public CharacterStatsComponent()
    {

    }

    public CharacterStatsComponent(CharacterStatsSaveData data)
    {
        Physique = data.Physique;
        Reflexes = data.Reflexes;
        Technical = data.Technical;
        Grit = data.Grit;
        Charisma = data.Charisma;

        Hunger = data.Hunger;
        Thirst = data.Thirst;
        Fatigue = data.Fatigue;
        Intoxication = data.Intoxication;
        Humanity = data.Humanity;
    }

    // Cybernetic
    public float GetHardwareEfficiency() => 0;
    public float GetSystemVulnerability() => 0; // How much your hardware is jeopardized by a virus
    public float GetTemperature() => 20;

    // These are all calculated from attributes and gear
    // Combat Stats
    public float GetCyberpsychosis() => 0; // Engage in human activities. Conversations, sex, eating food and other human things to keep this low.

    // Resistances
    public float GetHungerResistance() => 0;
    public float GetToxinResistance() => 0;
    public float GetCyberResistance() => 0;

    internal void Tick()
    {
        float onePercent = 0.01f;

        int ticksPerHungerIncrement = 25;
        int ticksPerThirstIncrement = 20;
        int ticksPerFatigueIncrement = 50;

        Hunger = Mathf.Clamp01(Hunger + onePercent / ticksPerHungerIncrement);
        Thirst = Mathf.Clamp01(Thirst + onePercent / ticksPerThirstIncrement);
        Fatigue = Mathf.Clamp01(Fatigue + onePercent / ticksPerFatigueIncrement);
    }
}