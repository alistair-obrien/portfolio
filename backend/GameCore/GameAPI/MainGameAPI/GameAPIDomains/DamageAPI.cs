using System.Collections.Generic;

public sealed class DamageAPI : APIDomain
{
    public DamageAPI(GameInstance gameAPI) : base(gameAPI)
    {
    }

    internal override void RegisterHandlers(CommandRouter router)
    {

    }

    public IEnumerable<string> GetPossibleTreatmentsForDamage(DamageInstance damageInstance)
    {
        return Rulebook.InjuriesAndDamageSection.GetPossibleTreatmentsForInjury(damageInstance);
    }

    public IEnumerable<InteractionRequest> GetAvailableActionsOnDamageInstance(
        CharacterId sourceCharacterId,
        CharacterId targetCharacterId,
        DamageInstance damageInstance,
        int index)
    {
        if (!TryResolve(sourceCharacterId, out Character self)) { yield break; }

        switch (damageInstance.DamageDomain)
        {
            case var type when type == DamageDomain.Organic:
                //if (!TryResolve(targetCharacterId, out Character targetCharacter)) { yield break; }

                //if (!targetCharacter.Anatomy.TryResolveSlot(i.OrganicBodySlotPath, out var bodyPart)) { yield break; }

                //if (!bodyPart.TryGetInjuryFromIndex(index, out var injury)) yield break;

                //foreach (var treatment in GetPossibleTreatmentsForDamage(injury))
                //{
                //    if (treatment != "Rest") // HACK
                //    {
                //        yield return new InteractionRequest(treatment, new TreatInjuryRequest(
                //            sourceCharacterId,
                //            targetCharacterId,
                //            i.OrganicBodySlotPath,
                //            index,
                //            treatment));
                //    }
                //}
                break;
        }
    }
}