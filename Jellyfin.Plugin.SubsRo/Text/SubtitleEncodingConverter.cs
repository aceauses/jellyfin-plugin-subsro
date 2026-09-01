using System.Text;

namespace Jellyfin.Plugin.SubsRo.Text;

/// <summary>Detects the encoding of a subtitle payload and converts it to UTF-8.</summary>
public static class SubtitleEncodingConverter
{
    private static bool _registered;

    /// <summary>Registers legacy code pages. .NET omits them by default.</summary>
    public static void RegisterProviders()
    {
        if (_registered)
        {
            return;
        }

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _registered = true;
    }

    /// <summary>Detects and converts subtitle bytes to UTF-8 encoding.
    /// Applies detection in order: UTF-8 BOM, strict UTF-8 decode, Windows-1250 fallback.</summary>
    /// <param name="input">Raw subtitle bytes, potentially in any legacy encoding.</param>
    /// <returns>The input converted to UTF-8 bytes, with any BOM stripped.</returns>
    public static byte[] ToUtf8(byte[] input)
    {
        if (input.Length == 0)
        {
            return input;
        }

        RegisterProviders();

        // 1. Byte-order mark wins outright.
        if (input.Length >= 3 && input[0] == 0xEF && input[1] == 0xBB && input[2] == 0xBF)
        {
            return input[3..];
        }

        // 2. Strict UTF-8 decode: succeeds only if the bytes really are UTF-8.
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        try
        {
            _ = strict.GetString(input);
            return input;
        }
        catch (DecoderFallbackException)
        {
            // Not UTF-8; fall through.
        }

        // 3. Windows-1250 is the common fallback for Romanian subtitles.
        var text = Encoding.GetEncoding(1250).GetString(input);
        return new UTF8Encoding(false).GetBytes(text);
    }
}
