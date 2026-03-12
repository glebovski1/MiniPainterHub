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

    public IDictionary<string, string[]> Errors { get; }
}
