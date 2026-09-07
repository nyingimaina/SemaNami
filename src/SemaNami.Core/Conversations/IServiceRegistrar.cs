namespace SemaNami.Core.Conversations;

public interface IServiceRegistrar
{
    void Install(string exePath);

    void Uninstall();
}
