using System.Collections.Generic;

namespace MiniPainterHub.Server.Exceptions;

[System.Serializable]
public sealed class DomainValidationException : System.Exception
{
    public DomainValidationException()
        : this("One or more validation failures occurred.", new Dictionary<string, string[]>())
    {
    }

    public DomainValidationException(string message)
        : this(message, new Dictionary<string, string[]>())
    {
    }

    public DomainValidationException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public DomainValidationException(string message, System.Exception inner)
        : base(message, inner)
    {
        Errors = new Dictionary<string, string[]>();
    }

    private DomainValidationException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        : base(info, context)
    {
        Errors = (IDictionary<string, string[]>)info.GetValue(nameof(Errors), typeof(IDictionary<string, string[]>))!;
    }

    public IDictionary<string, string[]> Errors { get; }

    public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
    {
        base.GetObjectData(info, context);
        info.AddValue(nameof(Errors), Errors);
    }
}
