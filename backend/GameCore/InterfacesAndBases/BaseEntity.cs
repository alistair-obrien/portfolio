public interface IGameSerializer
{
    string Serialize(object obj);
    T Deserialize<T>(string data);
}

public abstract class BaseEntity { }
