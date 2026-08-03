using System.Text.Json;
using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.Infrastructure.Dictionary;

public sealed class JsonActiveScraperProfileStore :
    IActiveScraperProfileStore
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions =
        new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, string>? _selections;

    public JsonActiveScraperProfileStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public async Task<string?> GetActiveProfileNameAsync(
        string sourceLanguageCode,
        string targetLanguageCode,
        CancellationToken cancellationToken = default)
    {
        var key = CreateKey(
            sourceLanguageCode,
            targetLanguageCode);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            await EnsureLoadedAsync(cancellationToken);

            return _selections!.TryGetValue(
                    key,
                    out var profileName)
                ? profileName
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetActiveProfileNameAsync(
        string sourceLanguageCode,
        string targetLanguageCode,
        string profileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);

        var key = CreateKey(
            sourceLanguageCode,
            targetLanguageCode);

        await _gate.WaitAsync(cancellationToken);

        try
        {
            await EnsureLoadedAsync(cancellationToken);
            _selections![key] = profileName.Trim();
            await SaveCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(
        CancellationToken cancellationToken)
    {
        if (_selections is not null)
        {
            return;
        }

        if (!File.Exists(_filePath))
        {
            _selections = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            return;
        }

        try
        {
            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

            var document =
                await JsonSerializer.DeserializeAsync<SelectionFile>(
                    stream,
                    SerializerOptions,
                    cancellationToken)
                ?? throw new InvalidDataException(
                    "The active-profile file is empty.");

            if (document.SchemaVersion != CurrentSchemaVersion)
            {
                throw new InvalidDataException(
                    $"Unsupported active-profile schema version {document.SchemaVersion}.");
            }

            _selections = new Dictionary<string, string>(
                document.Selections,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ScraperProfilePersistenceException(
                $"Could not load active dictionary profiles from '{_filePath}'.",
                exception);
        }
    }

    private async Task SaveCoreAsync(
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new ScraperProfilePersistenceException(
                "The active-profile path has no directory.");
        var temporaryPath =
            _filePath + $".{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(directory);

            var document = new SelectionFile
            {
                SchemaVersion = CurrentSchemaVersion,
                Selections = new Dictionary<string, string>(
                    _selections!,
                    StringComparer.OrdinalIgnoreCase)
            };

            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    document,
                    SerializerOptions,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(
                temporaryPath,
                _filePath,
                overwrite: true);
        }
        catch (OperationCanceledException)
        {
            TryDelete(temporaryPath);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(temporaryPath);
            throw new ScraperProfilePersistenceException(
                $"Could not save active dictionary profiles to '{_filePath}'.",
                exception);
        }
    }

    private static string CreateKey(
        string sourceLanguageCode,
        string targetLanguageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            sourceLanguageCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(
            targetLanguageCode);

        return $"{sourceLanguageCode.Trim().ToLowerInvariant()}->{targetLanguageCode.Trim().ToLowerInvariant()}";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A stale temporary file is harmless.
        }
    }

    private sealed class SelectionFile
    {
        public int SchemaVersion { get; set; }

        public Dictionary<string, string> Selections { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
