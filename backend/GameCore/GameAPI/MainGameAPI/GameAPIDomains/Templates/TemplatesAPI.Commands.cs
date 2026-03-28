using System;
using System.Collections.Generic;

public partial class TemplatesAPI
{
    public class Commands
    {
        public record DeleteTemplate(ITemplateDbId TemplateId) : IGameCommand;

        public record CreateOrUpdateTemplate(string TemplateId, List<IBlueprint> BlueprintsGraph) : IGameCommand;

        public record SaveAllTemplates() : IGameCommand;

        public record LoadAllTemplates() : IGameCommand;

        // OLD
        public record CreateNewTemplateFromModel(
            IGameDbId ModelRootId, 
            ITemplateDbId TemplateId
        ) : IGameCommand;

        public record SpawnModelFromTemplate(
            ITemplateDbId TemplateDbId, 
            IGameDbId InstanceId
        ) : IGameCommand;
    }
}