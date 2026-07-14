using MiniPainterHub.Common.DTOs;

namespace MiniPainterHub.Server.Exceptions;

[System.Serializable]
public sealed class HobbyProjectLinkConflictException : System.Exception
{
    public HobbyProjectLinkConflictException(string message, HobbyProjectReferenceDto currentProject)
        : base(message)
    {
        CurrentProject = currentProject;
    }

    public HobbyProjectReferenceDto CurrentProject { get; }
}
