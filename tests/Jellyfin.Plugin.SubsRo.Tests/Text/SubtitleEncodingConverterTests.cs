using System.Text;
using Jellyfin.Plugin.SubsRo.Text;
using Xunit;

namespace Jellyfin.Plugin.SubsRo.Tests.Text;

public class SubtitleEncodingConverterTests
{
    // Original text with comma-below characters (as would appear in UTF-8 files)
    private const string SampleWithCommaBelow = "Ce faci, șefu'? Ține-te bine în această după-amiază.";

    // Windows-1250 can only represent cedilla characters, not comma-below.
    // This simulates a real subtitle file encoded in Windows-1250.
    private const string SampleWithCedilla = "Ce faci, şefu'? Tine-te bine în aceasta dupa-amiaza.";

    public SubtitleEncodingConverterTests() => SubtitleEncodingConverter.RegisterProviders();

    [Fact]
    public void ToUtf8_AlreadyUtf8_RoundTrips()
    {
        var input = new UTF8Encoding(false).GetBytes(SampleWithCommaBelow);

        var result = Encoding.UTF8.GetString(SubtitleEncodingConverter.ToUtf8(input));

        Assert.Equal(SampleWithCommaBelow, result);
    }

    [Fact]
    public void ToUtf8_Utf8WithBom_StripsBomAndRoundTrips()
    {
        var input = new UTF8Encoding(true).GetPreamble()
            .Concat(new UTF8Encoding(false).GetBytes(SampleWithCommaBelow)).ToArray();

        var output = SubtitleEncodingConverter.ToUtf8(input);

        Assert.NotEqual(0xEF, output[0]);
        Assert.Equal(SampleWithCommaBelow, Encoding.UTF8.GetString(output));
    }

    [Fact]
    public void ToUtf8_Windows1250_ProducesCorrectDiacritics()
    {
        // Input is already Windows-1250 encoded bytes (simulates a real subtitle file in that encoding)
        var input = Encoding.GetEncoding(1250).GetBytes(SampleWithCedilla);

        var result = Encoding.UTF8.GetString(SubtitleEncodingConverter.ToUtf8(input));

        // Windows-1250 does not support comma-below characters (ș/ț), only cedilla variants (ş/ţ).
        // When a Windows-1250 subtitle file is decoded, the cedilla forms are preserved and
        // converted to UTF-8. The test verifies that real Romanian diacritics are present, not replacement characters.
        Assert.Contains("ş", result, StringComparison.Ordinal);
    }

    [Fact]
    public void ToUtf8_Empty_ReturnsEmpty()
    {
        Assert.Empty(SubtitleEncodingConverter.ToUtf8(Array.Empty<byte>()));
    }
}
