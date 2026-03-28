using System;

[AttributeUsage(AttributeTargets.Field)]
public class AttachmentAttribute : Attribute
{
    public string DisplayName { get; }

    public AttachmentAttribute(string displayName = null)
    {
        DisplayName = displayName;
    }
}