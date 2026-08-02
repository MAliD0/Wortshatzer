using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.Infrastructure.Dictionary;

public static class BuiltInScraperProfiles
{
    public static ScraperProfile CambridgeGermanEnglish { get; } =
        new(
            "Cambridge German–English",
            "https://dictionary.cambridge.org/dictionary/german-english/{word}",
            "de",
            "en",
            [
                new ScraperExtractionRule(
                    DictionaryField.Headword,
                    ".hw.dhw",
                    ScraperResultMode: ScraperResultMode.First,
                    isRequired: true,
                    fallbackSelectors: [".di-title", "h1"]),
                new ScraperExtractionRule(
                    DictionaryField.PartOfSpeech,
                    ".pos.dpos",
                    maximumResults: 5,
                    fallbackSelectors: [".pos"]),
                new ScraperExtractionRule(
                    DictionaryField.Pronunciation,
                    ".ipa.dipa",
                    maximumResults: 5,
                    fallbackSelectors: [".ipa"]),
                new ScraperExtractionRule(
                    DictionaryField.Definition,
                    ".def.ddef_d",
                    maximumResults: 12,
                    fallbackSelectors: [".def"]),
                new ScraperExtractionRule(
                    DictionaryField.Translation,
                    ".trans.dtrans",
                    maximumResults: 12,
                    isRequired: true,
                    fallbackSelectors: [".trans"]),
                new ScraperExtractionRule(
                    DictionaryField.Example,
                    ".examp.dexamp",
                    maximumResults: 12,
                    fallbackSelectors: [".examp"]),
                new ScraperExtractionRule(
                    DictionaryField.AudioUrl,
                    "source[type='audio/mpeg']",
                    ScraperValueSource.Attribute,
                    ScraperResultMode.First,
                    attributeName: "src",
                    fallbackSelectors: ["audio source", "audio"])
            ],
            ".entry-body");

    public static ScraperProfile VerbformenGerman { get; } =
        new(
            "Verbformen German",
            "https://www.verbformen.de/?w={word}",
            "de",
            "de",
            [
                new ScraperExtractionRule(
                    DictionaryField.Headword,
                    "h1",
                    ScraperResultMode.First,
                    isRequired: true),
                new ScraperExtractionRule(
                    DictionaryField.Conjugation,
                    "#stammformen",
                    ScraperResultMode.First,
                    fallbackSelectors:
                    [
                        "[id*='stammform']",
                        ".stammformen"
                    ]),
                new ScraperExtractionRule(
                    DictionaryField.Definition,
                    ".rInf",
                    maximumResults: 8,
                    fallbackSelectors:
                    [
                        "[class*='bedeutung']",
                        "[class*='meaning']"
                    ]),
                new ScraperExtractionRule(
                    DictionaryField.Example,
                    ".vGrnd",
                    maximumResults: 10,
                    fallbackSelectors:
                    [
                        "[class*='beispiel']",
                        "[class*='example']"
                    ])
            ]);

    public static IReadOnlyList<ScraperProfile> All { get; } =
    [
        CambridgeGermanEnglish,
        VerbformenGerman
    ];
}
