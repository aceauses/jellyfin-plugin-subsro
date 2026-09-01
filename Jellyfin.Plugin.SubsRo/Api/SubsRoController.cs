using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.SubsRo.Api;

[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("SubsRo")]
public class SubsRoController : ControllerBase
{
    private readonly SubsRoApiClient _client;

    public SubsRoController(SubsRoApiClient client) => _client = client;

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
