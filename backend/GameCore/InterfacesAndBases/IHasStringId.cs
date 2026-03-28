// This is used to indicate that the object will be treated as a dynamic look up from a reference database
// This is used to indicate that the object has a unique identifier
public interface IHasStringId
{
    public string Uid { get; protected set; }
}
