using System.Collections.Generic;

public class DamageInstancePresentation
{
    public readonly DamageDomain DamageDomain;
    public readonly DamageType DamageType;
    public readonly int DamageSeverity;
    public readonly bool IsDamaged;
    public readonly IEnumerable<object> PossibleTreatments;

    public DamageInstancePresentation(DamageInstance damageComponentBase)
    {
        DamageDomain = damageComponentBase.DamageDomain;
        DamageType = damageComponentBase.DamageType;
        DamageSeverity = damageComponentBase.DamageSeverity;
        IsDamaged = true;
        //PossibleTreatments = itemApi.GetPossibleTreatmentsForDamage(damageComponentBase);
    }

    public DamageInstancePresentation(
        DamageDomain damageDomain)
    {
        DamageDomain = damageDomain;
        DamageType = DamageType.None;
        DamageSeverity = 1;
        IsDamaged = false;
        PossibleTreatments = System.Array.Empty<string>();
    }
}
