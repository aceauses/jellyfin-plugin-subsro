using System.Globalization;

namespace Jellyfin.Plugin.SubsRo;

/// <summary>
/// Encodes and decodes the opaque identifier handed to Jellyfin.
/// Format: {subtitleId} or {subtitleId}|{entryPath}.
/// </summary>
public static class SubtitleId
{
    private const char Separator = '|';

    /// <summary>
    /// Encodes a subtitle ID and optional archive entry path into an opaque identifier.
    /// </summary>
    /// <param name="subtitleId">The subtitle identifier from subs.ro.</param>
    /// <param name="entryPath">Optional path to an entry within a ZIP archive; may contain the separator character.</param>
    /// <returns>Encoded identifier in format "{subtitleId}" or "{subtitleId}|{entryPath}".</returns>
    public static string Encode(int subtitleId, string? entryPath)
        => string.IsNullOrEmpty(entryPath)
            ? subtitleId.ToString(CultureInfo.InvariantCulture)
            : string.Concat(subtitleId.ToString(CultureInfo.InvariantCulture), Separator, entryPath);

    /// <summary>
    /// Attempts to decode an opaque identifier back into subtitle ID and optional archive entry path.
    /// </summary>
    /// <param name="value">The encoded identifier string.</param>
    /// <param name="subtitleId">On success, contains the decoded subtitle ID; otherwise 0.</param>
    /// <param name="entryPath">On success, contains the decoded entry path if present; otherwise null.</param>
    /// <returns>True if decoding succeeded; false if the value is malformed or the ID part is not a valid integer.</returns>
    public static bool TryDecode(string value, out int subtitleId, out string? entryPath)
    {
        subtitleId = 0;
        entryPath = null;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var index = value.IndexOf(Separator);
        var idPart = index < 0 ? value : value[..index];

        if (!int.TryParse(idPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out subtitleId))
        {
            return false;
        }

        if (index >= 0 && index + 1 < value.Length)
        {
            entryPath = value[(index + 1)..];
        }

        return true;
    }
}
