namespace GitTab.Core.Git;

/// <summary>Raised when a repository read operation fails or is attempted with no repo open.</summary>
public sealed class RepositoryException : Exception
{
    public RepositoryException(string message) : base(message) { }
    public RepositoryException(string message, Exception inner) : base(message, inner) { }
}
