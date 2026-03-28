public partial class TemplatesAPI
{
    public class Events
    {
        public sealed record TemplateAdded(ITemplateDbId Id, GameModelTemplatePresentation Presentation) : IGameEvent;
        public sealed record TemplateUpdated(ITemplateDbId Id, GameModelTemplatePresentation Presentation) : IGameEvent;
        public sealed record TemplateRemoved(ITemplateDbId Id) : IGameEvent;
    }
}