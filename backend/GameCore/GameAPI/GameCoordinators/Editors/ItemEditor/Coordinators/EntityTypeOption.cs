using System;

public record EntityTypeOption(
    string DisplayName,
    Type ModelType,
    Type TemplateIdType
);