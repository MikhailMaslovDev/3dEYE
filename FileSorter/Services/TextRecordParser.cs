using System.Globalization;

namespace FileSorter;

internal sealed class TextRecordParser
{
    public bool TryParse(
        string originalLine,
        long sourceSequence,
        out TextRecord record,
        out string errorMessage)
    {
        record = default;
        errorMessage = string.Empty;

        var separatorIndex = originalLine.IndexOf('.');

        if (separatorIndex <= 0)
        {
            errorMessage = "Missing numeric prefix or '.' separator.";
            return false;
        }

        if (separatorIndex == originalLine.Length - 1)
        {
            errorMessage = "String part is empty.";
            return false;
        }

        if (!int.TryParse(
                originalLine.AsSpan(0, separatorIndex),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number))
        {
            errorMessage = "Number must be a non-negative Int32.";
            return false;
        }

        record = new TextRecord(
            originalLine,
            separatorIndex + 1,
            number,
            sourceSequence);

        return true;
    }

}
