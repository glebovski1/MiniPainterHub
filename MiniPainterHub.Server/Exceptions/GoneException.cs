namespace MiniPainterHub.Server.Exceptions;

[System.Serializable]
public sealed class GoneException : System.Exception
{
    public GoneException(string message) : base(message)
    {
    }
}
