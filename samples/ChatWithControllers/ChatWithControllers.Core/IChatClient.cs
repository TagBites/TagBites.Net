namespace ChatWithControllers;

public interface IChatClient
{
    void OnMessage(string userName, string message);
}
