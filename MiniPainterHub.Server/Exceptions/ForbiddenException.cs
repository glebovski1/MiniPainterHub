namespace MiniPainterHub.Server.Exceptions;

[System.Serializable]
public sealed class ForbiddenException : System.Exception
{
    public ForbiddenException()
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, System.Exception inner)
        : base(message, inner)
    {
    }
}
