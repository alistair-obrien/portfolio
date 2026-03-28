public class HealthPresentation
{
    public readonly int Current;
    public readonly int Max;
    public readonly DamageDomain Domain;

    public HealthPresentation(int current, int max, DamageDomain damageDomain)
    {
        Current = current;
        Max = max;
        Domain = damageDomain;
    }

}