public interface IRuntimeModule
{
    void Initialize();
    void Shutdown();
    void DoUpdate(float deltaTime);
}