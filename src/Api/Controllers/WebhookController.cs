[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private readonly ICampaignRepository _repo;
    private readonly IConfiguration _config;

    public WebhookController(ICampaignRepository repo, IConfiguration config)
    {
        _repo = repo;
        _config = config;
    }

    // Meta chama este GET para verificar o endpoint na primeira configuração
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        var secret = _config["WhatsApp:WebhookVerifyToken"];
        if (mode == "subscribe" && token == secret)
            return Ok(challenge);

        return Forbid();
    }

    // Meta chama este POST para cada evento
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] WebhookPayload payload)
    {
        foreach (var entry in payload.Entry)
            foreach (var change in entry.Changes)
                foreach (var status in change.Value.Statuses ?? [])
                {
                    var msgStatus = status.Status switch
                    {
                        "sent" => MessageStatus.Sent,
                        "delivered" => MessageStatus.Delivered,
                        "read" => MessageStatus.Read,
                        _ => MessageStatus.Failed
                    };

                    await _repo.UpdateMessageStatusAsync(status.Id, msgStatus);
                }

        return Ok();  // Meta exige 200 rápido, processe de forma assíncrona
    }
}