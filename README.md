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
- optional always-visible popup with its own quick-translation input
- in-memory demo dictionary when no online provider is configured
- optional DeepL API translation for arbitrary supported text
- runtime translation-method selector for DeepL, web scraping, and the offline demo
- declarative dictionary scraper profiles with CSS selectors, fallback selectors, custom fields, and extraction limits
- AngleSharp extraction engine with support for text, HTML, and attribute values
- bounded HTTP dictionary lookup with a 12-hour in-memory cache
- starter profiles for Cambridge German–English and Verbformen German
- visual scraper settings editor with protected built-ins, custom profile cloning, and field-level selector configuration
- atomic JSON persistence and live test-word preview for custom scraper profiles
- active dictionary profile selection per source/target language pair
- asynchronous dictionary enrichment in the main result and floating popup

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

Enable **Keep translation popup open** in the main window to pin the popup near the working-area corner. The pinned popup can translate a word or short phrase directly; press Enter or select **Translate**. It reuses the source language, target language, and translation method selected in the main window. Closing the pinned popup turns the option off.

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

When no key is present, DeepL is omitted from the selector and the application starts with the demo dictionary. When a key is configured, DeepL is the default, while **Web scraper** and **Offline demo** remain selectable.

Choose the translation method from the main-window header. The choice applies to manual entry, clipboard capture, OCR, and floating popups because all inputs share the same translation pipeline.

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

Open **Dictionary settings** from the main window to create or clone a profile. Built-ins cannot be overwritten or deleted. Custom profiles are stored atomically under `%LOCALAPPDATA%\\Wortshatzer\\scraper-profiles.json`, and **Test profile** previews the current unsaved selectors against a real word. Cache keys include the complete profile configuration, so changing a selector always produces a fresh preview.

Use **Use for language pair** to choose the active profile for its source and target languages. Selections are stored under `%LOCALAPPDATA%\\Wortshatzer\\active-scraper-profiles.json`. If no selection exists, Wortshatzer uses the first matching built-in. Translation is displayed immediately; dictionary details load independently and then appear in the main result. A still-visible capture popup expands when the details arrive.

When **Web scraper** is the selected translation method, the active profile's first non-empty **Translation** value becomes the primary translation. Profiles used only for definitions or grammar can still enrich results, but cannot act as the translation method until a Translation field is configured.

## Architecture

- **Wortshatzer.Core** contains language, capture, shortcut, OCR, dictionary, word, and translation contracts.
- **Wortshatzer.Infrastructure** contains DeepL, Tesseract, AngleSharp, HTTP lookup, caching, and built-in profile implementations.
- **Wortshatzer** contains Avalonia UI and desktop clipboard, shortcut, screen-capture, and window integration.
- **Wortshatzer.Tests** validates domain rules and provider behavior.

Core does not reference Avalonia, HTTP, databases, OCR engines, HTML parsers, or operating-system APIs.
