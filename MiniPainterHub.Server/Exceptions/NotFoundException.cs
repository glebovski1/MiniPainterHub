namespace MiniPainterHub.Server.Exceptions;

[System.Serializable]
public sealed class NotFoundException : System.Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, System.Exception inner)
        : base(message, inner)
    {
    }

    private NotFoundException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
    }
}
