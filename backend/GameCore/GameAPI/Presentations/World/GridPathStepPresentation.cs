public class GridPathStepPresentation : BaseEntity
{
    public readonly Vec2Int From;
    public readonly Vec2Int To;

    public GridPathStepPresentation(Vec2Int from, Vec2Int to)
    {
        From = from;
        To = to;
    }
}