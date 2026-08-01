# Wortshatzer

Wortshatzer is an Avalonia desktop application that captures words, translates them immediately, and shows the result in a compact floating popup.

## Current features

- manual word and short-phrase translation
- configurable source and target languages
- opt-in clipboard monitoring
- filtering for duplicate, multiline, and long clipboard content
- floating translation popup that does not intentionally take focus
- in-memory demo dictionary when no online provider is configured
- optional DeepL API translation for arbitrary supported text

## Run the application

```powershell
dotnet restore .\Wortshatzer.slnx
dotnet run --project .\Wortshatzer.csproj
```

## Configure DeepL

Wortshatzer never stores or commits an API key. It reads the key from the process environment.

For a DeepL API Free account:

```powershell
$env:WORTSHATZER_DEEPL_API_KEY = "your-api-key"
dotnet run --project .\Wortshatzer.csproj
```

The default endpoint is:

```text
https://api-free.deepl.com/
```

For a DeepL API Pro account, also set:

```powershell
$env:WORTSHATZER_DEEPL_API_URL = "https://api.deepl.com/"
dotnet run --project .\Wortshatzer.csproj
```

These PowerShell variables apply only to the current terminal session. Never put a real API key in source code, a commit, an issue, or a pull request.

When no key is present, the application automatically uses the demo dictionary.

## Architecture

- **Wortshatzer.Core** contains language, capture, word, and translation contracts.
- **Wortshatzer.Infrastructure** contains translation-provider implementations.
- **Wortshatzer** contains Avalonia UI and desktop clipboard/window integration.

Core does not reference Avalonia, HTTP, databases, or operating-system APIs.
