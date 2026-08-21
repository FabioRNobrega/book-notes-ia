using EbookParseService.Api.Services;

namespace EbookParseService.Tests;

public sealed class NumberToWordsConverterTests
{
    private readonly NumberToWordsConverter _converter = new();

    [Theory]
    [InlineData(0, "Zero")]
    [InlineData(1, "One")]
    [InlineData(11, "Eleven")]
    [InlineData(19, "Nineteen")]
    [InlineData(20, "Twenty")]
    [InlineData(24, "Twenty-four")]
    [InlineData(40, "Forty")]
    [InlineData(99, "Ninety-nine")]
    [InlineData(100, "One hundred")]
    [InlineData(101, "One hundred one")]
    [InlineData(115, "One hundred fifteen")]
    [InlineData(124, "One hundred twenty-four")]
    [InlineData(200, "Two hundred")]
    [InlineData(999, "Nine hundred ninety-nine")]
    public void TryConvert_UsesNaturalEnglishCardinalWords(int number, string expected)
    {
        var converted = _converter.TryConvert(number, "en", out var words);

        Assert.True(converted);
        Assert.Equal(expected, words);
        Assert.DoesNotContain(" and ", words, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, "Zero")]
    [InlineData(1, "Um")]
    [InlineData(11, "Onze")]
    [InlineData(19, "Dezenove")]
    [InlineData(20, "Vinte")]
    [InlineData(24, "Vinte e quatro")]
    [InlineData(99, "Noventa e nove")]
    [InlineData(100, "Cem")]
    [InlineData(101, "Cento e um")]
    [InlineData(115, "Cento e quinze")]
    [InlineData(200, "Duzentos")]
    [InlineData(300, "Trezentos")]
    [InlineData(400, "Quatrocentos")]
    [InlineData(500, "Quinhentos")]
    [InlineData(600, "Seiscentos")]
    [InlineData(700, "Setecentos")]
    [InlineData(800, "Oitocentos")]
    [InlineData(900, "Novecentos")]
    [InlineData(999, "Novecentos e noventa e nove")]
    public void TryConvert_UsesNaturalPortugueseCardinalWords(int number, string expected)
    {
        var converted = _converter.TryConvert(number, "pt", out var words);

        Assert.True(converted);
        Assert.Equal(expected, words);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("pt")]
    public void TryConvert_SupportsEveryNumberFromZeroThrough999(string language)
    {
        for (var number = 0; number <= 999; number++)
        {
            Assert.True(_converter.TryConvert(number, language, out var words));
            Assert.False(string.IsNullOrWhiteSpace(words));
        }
    }

    [Theory]
    [InlineData(-1, "en")]
    [InlineData(1000, "en")]
    [InlineData(1, "eng")]
    [InlineData(1, "pt-BR")]
    [InlineData(1, "")]
    [InlineData(1, null)]
    public void TryConvert_RejectsUnsupportedInput(int number, string? language)
    {
        var converted = _converter.TryConvert(number, language, out var words);

        Assert.False(converted);
        Assert.Equal(string.Empty, words);
    }
}
