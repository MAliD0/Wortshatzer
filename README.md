# Wortshatzer

Wortshatzer is an Avalonia desktop application that captures words, translates them immediately, and shows the result in a compact floating popup.

## Current features

- manual word and short-phrase translation
- configurable source and target languages
- opt-in clipboard monitoring
- filtering for duplicate, multiline, and long clipboard content
- Windows global shortcut `Ctrl + Alt + Z`
- shortcut capture from clipboard text or clipboard images
- offline Tesseract OCR for German, English, Polish, and Russian
- floating translation popup that does not intentionally take focus
- in-memory demo dictionary when no online provider is configured
- optional DeepL API translation for arbitrary supported text
- declarative dictionary scraper profiles with CSS selectors, fallback selectors, custom fields, and extraction limits
- AngleSharp extraction engine with support for text, HTML, and attribute values

## Run the application

```powershell
dotnet restore .\Wortshatzer.slnx
dotnet run --project .\Wortshatzer.csproj
```

## Capture with the global shortcut

On Windows, copy a short word, phrase, or image and press:

```text
Ctrl + Alt + Z
```

Text is translated immediately. If the clipboard contains an image instead, Wortshatzer runs OCR using the selected source language and then translates the recognized text.

OCR language data is downloaded only when a language is used for the first time. It is stored outside the repository under:

```text
%LOCALAPPDATA%\Wortshatzer\tessdata
```

The Tesseract Windows native binaries require the Microsoft Visual C++ 2022 runtime. The future installer will check this prerequisite.

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

## Flexible dictionary scraping

The scraper engine uses profiles instead of hardcoded website logic. A profile defines:

- a search URL containing `{word}`
- source and target languages
- an optional entry selector
- the fields the user wants to extract
- primary and fallback CSS selectors
- text, HTML, or attribute extraction
- first/all result behavior
- required fields, duplicate removal, and result limits
- custom user-defined fields

The profile Settings editor and live preview are the next integration stage.

## Architecture

- **Wortshatzer.Core** contains language, capture, shortcut, OCR, dictionary, word, and translation contracts.
- **Wortshatzer.Infrastructure** contains DeepL, Tesseract, and AngleSharp provider implementations.
- **Wortshatzer** contains Avalonia UI and desktop clipboard, shortcut, and window integration.
- **Wortshatzer.Tests** validates domain rules and provider behavior.

Core does not reference Avalonia, HTTP, databases, OCR engines, HTML parsers, or operating-system APIs.
