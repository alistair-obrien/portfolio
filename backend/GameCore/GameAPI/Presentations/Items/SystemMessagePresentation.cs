public enum MessageType { None, Warning, Error, Success }
public record MessagePayload(string Message, MessageType MessageType);

public class SystemMessagePresentation
{
    public readonly MessagePayload MessagePayload;

    public SystemMessagePresentation(MessagePayload message)
    {
        MessagePayload = message;
    }
}