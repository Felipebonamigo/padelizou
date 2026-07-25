using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Controllers;

public record SubscriptionDto(string Endpoint, string P256dh, string Auth);

[Authorize]
[Route("Push")]
public class PushController : Controller
{
    private readonly DbPadelContext _context;
    private readonly VapidSettings _vapidSettings;

    public PushController(DbPadelContext context, IOptions<VapidSettings> vapidOptions)
    {
        _context = context;
        _vapidSettings = vapidOptions.Value;
    }

    [HttpGet("PublicKey")]
    public IActionResult PublicKey() => Ok(new { publicKey = _vapidSettings.PublicKey });

    [HttpPost("Subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscriptionDto dto)
    {
        var jogadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var existente = await _context.PushSubscriptionsJogador
            .FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);

        if (existente != null)
        {
            existente.JogadorId = jogadorId;
            existente.P256dh = dto.P256dh;
            existente.Auth = dto.Auth;
        }
        else
        {
            _context.PushSubscriptionsJogador.Add(new PushSubscriptionJogador
            {
                JogadorId = jogadorId,
                Endpoint = dto.Endpoint,
                P256dh = dto.P256dh,
                Auth = dto.Auth,
            });
        }

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("Unsubscribe")]
    public async Task<IActionResult> Unsubscribe([FromBody] SubscriptionDto dto)
    {
        var subscription = await _context.PushSubscriptionsJogador
            .FirstOrDefaultAsync(s => s.Endpoint == dto.Endpoint);

        if (subscription != null)
        {
            _context.PushSubscriptionsJogador.Remove(subscription);
            await _context.SaveChangesAsync();
        }

        return Ok();
    }
}
