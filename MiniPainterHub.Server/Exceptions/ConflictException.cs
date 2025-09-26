namespace MiniPainterHub.Server.Exceptions;

[System.Serializable]
public sealed class ConflictException : System.Exception
{
    public ConflictException()
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, System.Exception inner)
        : base(message, inner)
    {
    }

    private ConflictException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}
