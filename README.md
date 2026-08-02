# Wortshatzer

Wortshatzer is an Avalonia desktop application that captures words, translates them immediately, and shows the result in a compact floating popup.

## Current features

- manual word and short-phrase translation
- configurable source and target languages
- opt-in clipboard monitoring
- filtering for duplicate, multiline, and long clipboard content
- Windows global shortcuts for clipboard and screen-region capture
- shortcut capture from clipboard text or clipboard images
- drag-to-select screen-region OCR
- offline Tesseract OCR for German, English, Polish, and Russian
- floating translation popup that does not intentionally take focus
- in-memory demo dictionary when no online provider is configured
- optional DeepL API translation for arbitrary supported text
- declarative dictionary scraper profiles with CSS selectors, fallback selectors, custom fields, and extraction limits
- AngleSharp extraction engine with support for text, HTML, and attribute values
- bounded HTTP dictionary lookup with a 12-hour in-memory cache
- starter profiles for Cambridge German–English and Verbformen German

## Run the application

```powershell
dotnet restore .\Wortshatzer.slnx
dotnet run --project .\Wortshatzer.csproj
```

## Capture with global shortcuts

On Windows:

- `Ctrl + Alt + Z` reads a short word or phrase from the clipboard. If there is no suitable text, it tries OCR on a clipboard image.
- `Ctrl + Shift + O` opens a full-screen overlay. Drag around a word or short phrase; release to run OCR and translate it. Press `Esc` to cancel.

OCR uses the source language selected in the main window. Short OCR results are translated immediately and shown in the popup. Longer results are placed in the editor for correction.

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

The HTTP lookup layer reuses one client, rejects oversized pages, converts network/status errors into user-safe dictionary errors, and caches successful results for 12 hours. The built-in Cambridge and Verbformen profiles are editable starting points; selectors are deliberately kept in profile data because websites can change.

The profile Settings editor, JSON persistence, and live preview are the next integration stage.

## Architecture

- **Wortshatzer.Core** contains language, capture, shortcut, OCR, dictionary, word, and translation contracts.
- **Wortshatzer.Infrastructure** contains DeepL, Tesseract, AngleSharp, HTTP lookup, caching, and built-in profile implementations.
- **Wortshatzer** contains Avalonia UI and desktop clipboard, shortcut, screen-capture, and window integration.
- **Wortshatzer.Tests** validates domain rules and provider behavior.

Core does not reference Avalonia, HTTP, databases, OCR engines, HTML parsers, or operating-system APIs.
