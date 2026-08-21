namespace EbookParseService.Api.Services;

public sealed class NumberToWordsConverter : INumberToWordsConverter
{
    private static readonly string[] EnglishBelowTwenty =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen",
        "eighteen", "nineteen"
    ];

    private static readonly string[] EnglishTens =
    [
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
    ];

    private static readonly string[] PortugueseBelowTwenty =
    [
        "zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove",
        "dez", "onze", "doze",
        "treze", "quatorze", "quinze",
        "dezesseis", "dezessete", "dezoito", "dezenove"
    ];

    private static readonly string[] PortugueseTens =
    [
        "", "", "vinte", "trinta", "quarenta", "cinquenta", "sessenta", "setenta", "oitenta", "noventa"
    ];

    public bool TryConvert(int number, string? language, out string words)
    {
        words = string.Empty;
        if (number is < 0 or > 999)
        {
            return false;
        }

        var converted = language switch
        {
            "en" => ConvertEnglish(number),
            "pt" => ConvertPortuguese(number),
            _ => null
        };
        if (converted is null)
        {
            return false;
        }

        words = char.ToUpperInvariant(converted[0]) + converted[1..];
        return true;
    }

    private static string ConvertEnglish(int number)
    {
        if (number < 100)
        {
            return ConvertEnglishBelowHundred(number);
        }

        var hundreds = EnglishBelowTwenty[number / 100] + " hundred";
        var remainder = number % 100;
        return remainder == 0
            ? hundreds
            : hundreds + " " + ConvertEnglishBelowHundred(remainder);
    }

    private static string ConvertEnglishBelowHundred(int number)
    {
        if (number < 20)
        {
            return EnglishBelowTwenty[number];
        }

        var tens = EnglishTens[number / 10];
        var units = number % 10;
        return units == 0 ? tens : tens + "-" + EnglishBelowTwenty[units];
    }

    private static string ConvertPortuguese(int number)
    {
        if (number < 100)
        {
            return ConvertPortugueseBelowHundred(number);
        }

        if (number == 100)
        {
            return "cem";
        }

        var hundreds = ConvertPortugueseHundreds(number / 100);
        var remainder = number % 100;
        return remainder == 0
            ? hundreds
            : hundreds + " e " + ConvertPortugueseBelowHundred(remainder);
    }

    private static string ConvertPortugueseBelowHundred(int number)
    {
        if (number < 20)
        {
            return PortugueseBelowTwenty[number];
        }

        var tens = PortugueseTens[number / 10];
        var units = number % 10;
        return units == 0 ? tens : tens + " e " + PortugueseBelowTwenty[units];
    }

    private static string ConvertPortugueseHundreds(int hundreds) => hundreds switch
    {
        1 => "cento",
        2 => "duzentos",
        3 => "trezentos",
        4 => "quatrocentos",
        5 => "quinhentos",
        6 => "seiscentos",
        7 => "setecentos",
        8 => "oitocentos",
        9 => "novecentos",
        _ => throw new ArgumentOutOfRangeException(nameof(hundreds))
    };
}
