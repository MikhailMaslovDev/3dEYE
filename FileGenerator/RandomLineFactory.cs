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

    private readonly Random _random;

    public RandomLineFactory(int seed)
    {
        _random = new Random(seed);
    }

    public string CreateLine()
    {
        var number = _random.Next(0, int.MaxValue);
        var text = IsDuplicateLine()
            ? DuplicateTexts[_random.Next(DuplicateTexts.Length)]
            : CreateRandomText();

        return $"{number}.{text}";
    }

    private bool IsDuplicateLine()
    {
        return _random.Next(DuplicateProbabilityDenominator) == 0;
    }

    private string CreateRandomText()
    {
        var wordCount = _random.Next(1, 7);
        var words = new string[wordCount];

        for (var index = 0; index < wordCount; index++)
        {
            var wordLength = _random.Next(3, 25);
            words[index] = GetRandomString(wordLength);
        }

        return string.Join(' ', words);
    }

    private string GetRandomString(int length)
    {
        return string.Create(length, _random, static (characters, random) =>
        {
            random.GetItems(AllowedCharacters, characters);
        });
    }
}
