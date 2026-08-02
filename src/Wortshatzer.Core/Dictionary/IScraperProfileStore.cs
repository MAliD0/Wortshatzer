namespace Wortshatzer.Core.Dictionary;

public interface IScraperProfileStore
{
    Task<IReadOnlyList<ScraperProfile>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyCollection<ScraperProfile> profiles,
        CancellationToken cancellationToken = default);
}

public sealed class ScraperProfilePersistenceException : Exception
{
    public ScraperProfilePersistenceException(string message)
        : base(message)
    {
    }

    public ScraperProfilePersistenceException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}
