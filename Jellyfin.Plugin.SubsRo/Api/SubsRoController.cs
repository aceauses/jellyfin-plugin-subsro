using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SubsRo.Api;

/// <summary>
/// Server-side proxy for the subs.ro quota display on the plugin's configuration page.
/// The browser cannot call subs.ro directly: the configured API key must never leave the
/// server, and the subs.ro API sets no CORS headers anyway, so this endpoint fetches the
/// quota on the server and returns only the numbers the page needs.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("SubsRo")]
public class SubsRoController : ControllerBase
{
    private readonly SubsRoApiClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubsRoController"/> class.
    /// </summary>
    /// <param name="client">The subs.ro API client used to fetch the quota.</param>
    public SubsRoController(SubsRoApiClient client) => _client = client;

    /// <summary>
    /// Returns the current subs.ro download quota for the configured API key, without ever
    /// exposing the key itself to the caller.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>
    /// An anonymous object with <c>configured</c> (whether an API key is set), and, once a key
    /// is present, <c>reachable</c> plus <c>Remaining</c>/<c>Total</c> quota figures if subs.ro
    /// responded successfully.
    /// </returns>
    [HttpGet("Quota")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetQuota(CancellationToken cancellationToken)
    {
        var key = Plugin.Instance?.Configuration.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            return Ok(new { configured = false });
        }

        var quota = await _client.GetQuotaAsync(key, cancellationToken).ConfigureAwait(false);
        return quota is null
            ? Ok(new { configured = true, reachable = false })
            : Ok(new { configured = true, reachable = true, quota.Remaining, quota.Total });
    }
}
