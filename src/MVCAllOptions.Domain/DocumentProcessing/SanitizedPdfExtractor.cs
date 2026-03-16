using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Volo.AIManagement.DocumentProcessing;

namespace MVCAllOptions.DocumentProcessing;

/// <summary>
/// Wraps the built-in <see cref="PdfExtractor"/> and strips null bytes and
/// control characters before the text reaches pgvector storage.
///
/// Root cause: Technical/scanned PDFs (e.g. NFPA standards) contain custom font
/// encodings whose glyph codes map to low Unicode control points (U+0000–U+001F).
/// PdfPig faithfully returns these code points, but PostgreSQL's JSON functions
/// hard-reject null bytes (U+0000) and several other control characters with
/// error 22P05 "unsupported Unicode escape sequence". A plain-text resume never
/// triggers this because its characters are all printable ASCII.
/// </summary>
public class SanitizedPdfExtractor : IDocumentTextExtractor
{
    private readonly PdfExtractor _inner;

    public SanitizedPdfExtractor(PdfExtractor inner)
    {
        _inner = inner;
    }

    public bool CanHandle(string contentType) => _inner.CanHandle(contentType);

    public async Task<string> ExtractTextAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var text = await _inner.ExtractTextAsync(content, contentType, cancellationToken);
        return SanitizeForJson(text);
    }

    /// <summary>
    /// Replaces characters that PostgreSQL JSON rejects with a space:
    ///   - U+0000 (null byte) — hard-rejected by PostgreSQL
    ///   - U+0001–U+001F except \t (\u0009), \n (\u000A), \r (\u000D)
    ///   - U+007F (DEL)
    ///   - U+0080–U+009F (C1 control characters)
    /// Everything else (printable BMP + supplementary characters) is kept as-is.
    /// </summary>
    private static string SanitizeForJson(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var sb = new StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\t' || ch == '\n' || ch == '\r'
                || (ch >= '\u0020' && ch < '\u007F')
                || ch > '\u009F')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append(' ');
            }
        }

        return sb.ToString();
    }
}
