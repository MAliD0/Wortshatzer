using System.Text;
using Wortshatzer.Core.Dictionary;

namespace Wortshatzer.ViewModels;

public static class DictionaryResultFormatter
{
    public static string Format(
        DictionaryLookupResult result,
        int maximumFields = 6,
        int maximumValuesPerField = 3)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (maximumFields < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumFields));
        }

        if (maximumValuesPerField < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumValuesPerField));
        }

        var text = new StringBuilder();

        foreach (var field in result.Fields
            .Take(maximumFields))
        {
            if (field.Value.Count == 0)
            {
                continue;
            }

            if (text.Length > 0)
            {
                text.AppendLine();
            }

            text.Append(field.Key);
            text.Append(": ");
            text.Append(string.Join(
                " • ",
                field.Value.Take(maximumValuesPerField)));
        }

        return text.ToString();
    }
}
