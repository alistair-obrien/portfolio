using System.Collections.Generic;

public record MapEditorPresentation(
    MapChunkId? MapChunkId,
    MapChunkTemplateId? TemplateId,
    MapPresentation MapPresentation,
    IReadOnlyList<Tool> AvailableTools,
    ToolDescription CurrentMode,
    ToolDescription CurrentShape);
