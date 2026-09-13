namespace FileGenerator;

internal sealed class RandomLineFactory
{
    private const int DuplicateProbabilityDenominator = 8;

    private static readonly char[] AllowedCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.-_!?"
            .ToCharArray();

    private static readonly string[] DuplicateTexts =
    [
        "shared value",
        "same text",
        "repeated.item",
        "common-value"
    ];

    public string CreateLine()
    {
        var number = Random.Shared.Next(0, int.MaxValue);
        var text = IsDuplicateLine()
            ? DuplicateTexts[Random.Shared.Next(DuplicateTexts.Length)]
            : CreateRandomText();

        return $"{number}.{text}";
    }

    private bool IsDuplicateLine()
    {
        return Random.Shared.Next(DuplicateProbabilityDenominator) == 0;
    }

    private string CreateRandomText()
    {
        var wordCount = Random.Shared.Next(1, 7);
        var words = new string[wordCount];

        for (var index = 0; index < wordCount; index++)
        {
            var wordLength = Random.Shared.Next(3, 25);
            words[index] = GetRandomString(wordLength);
        }

        return string.Join(' ', words);
    }

    private string GetRandomString(int length)
    {
        return new string(Random.Shared.GetItems(AllowedCharacters, length));
    }
}
