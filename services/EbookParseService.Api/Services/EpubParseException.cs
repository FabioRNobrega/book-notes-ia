namespace EbookParseService.Api.Services;

public enum EpubParseErrorKind
{
    InvalidRequest,
    InvalidEpub,
    UnsupportedStructure,
    UnsupportedContent,
    OutputConflict
}

public sealed class EpubParseException : Exception
{
    public EpubParseException(EpubParseErrorKind kind, string publicMessage, Exception? innerException = null)
        : base(publicMessage, innerException)
    {
        Kind = kind;
        PublicMessage = publicMessage;
    }

    public EpubParseErrorKind Kind { get; }
    public string PublicMessage { get; }
}
