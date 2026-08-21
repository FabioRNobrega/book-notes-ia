namespace EbookParseService.Api.Services;

public interface INumberToWordsConverter
{
    bool TryConvert(int number, string? language, out string words);
}
