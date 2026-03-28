public class CharacterMovedOnMapPresentation
{
    public readonly CharacterId CharacterUid;
    public readonly MoveResult MoveResult;
    public readonly float Speed;

    public CharacterMovedOnMapPresentation(MapsAPI.Events.CharacterMovedOnMap evt)
    {
        CharacterUid = evt.CharacterId;
        MoveResult = evt.MoveResult;
        Speed = 10;
    }
}
