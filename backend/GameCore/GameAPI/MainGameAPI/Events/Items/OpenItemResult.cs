public sealed record OpenItemResult(
    Character SelfSnapshot, 
    Item ItemSnapshot) : TurnMutation;
