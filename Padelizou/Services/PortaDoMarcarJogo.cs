using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using System.Security.Claims;

namespace Padelizou.Services;

// Quem pode marcar quadra pelo Padelizou — uma régua, um lugar.
//
// Existe como serviço, e não como um `if` copiado em cada tela, porque a mesma resposta serve
// o menu, a home, o painel do clube e cada ação do controller. Duas cópias de uma regra dessas
// divergem no dia em que só uma é atualizada — e aí o módulo "escondido" reaparece por uma
// porta que ninguém lembrou de fechar (é a mesma armadilha do PortaDosDesafios).
//
// ⚠️ Esconder o link do menu é CORTESIA. A trava de verdade é o `PodeUsarAsync` no começo das
// ações de ENTRADA do MarcarJogoController: sem ele, /MarcarJogo continuaria respondendo pra
// quem digitasse o endereço na barra.
//
// ⚠️ Escondido = só os dois admins do Padelizou, e de propósito SEM o assistente — mesma régua
// do ModuloDoBar e do PortaDosDesafios. Marcar quadra é atividade de jogador (reservar, pagar,
// cancelar), e deixar entrar quem só pode olhar seria convidá-lo justamente ao que a flag dele
// proíbe.
public class PortaDoMarcarJogo
{
    private readonly DbPadelContext _context;
    private readonly MarcarJogoSettings _settings;

    public PortaDoMarcarJogo(DbPadelContext context, Microsoft.Extensions.Options.IOptions<MarcarJogoSettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    // Escondido = só admin do Padelizou. É isto que segura o módulo fora do ar sem branch
    // separada nem código apagado (ver MarcarJogoSettings).
    public bool Escondido => !_settings.Habilitado;

    public async Task<bool> PodeUsarAsync(int? usuarioId)
    {
        if (usuarioId == null) return false;
        if (!Escondido) return true;

        return await EhAdminDoPadelizouAsync(usuarioId);
    }

    // Pra quem tem o usuário na mão e não o id: as views (`User`) e os view components.
    public Task<bool> PodeUsarAsync(ClaimsPrincipal? usuario) => PodeUsarAsync(IdDe(usuario));

    // Mesma pergunta, com o nome do que a tela faz com a resposta.
    public Task<bool> MostrarNoMenuAsync(ClaimsPrincipal? usuario) => PodeUsarAsync(usuario);

    // ⚠️ Vitrine é OUTRA pergunta. `PodeUsarAsync` responde não pra quem não está logado — é o
    // certo pro menu e pro controller ([Authorize]), mas erraria na home do visitante: o card
    // "Marcar quadra" aparece pra deslogado e cai no login, que é o funil. Se a vitrine usasse
    // a régua de uso, o card sumiria pro visitante mesmo DEPOIS de o módulo reabrir — e ninguém
    // notaria, porque some calado. Aqui a pergunta é "o módulo existe?", não "esta pessoa pode?".
    public async Task<bool> MostrarNaVitrineAsync(ClaimsPrincipal? usuario) =>
        !Escondido || await PodeUsarAsync(usuario);

    private static int? IdDe(ClaimsPrincipal? usuario)
    {
        var claim = usuario?.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(claim, out var id) ? id : null;
    }

    // As mesmas duas flags de PoderesNoSistema.PodeEditarTudo. Consultadas direto pra não
    // arrastar a linha inteira do jogador numa pergunta que roda em toda requisição.
    private Task<bool> EhAdminDoPadelizouAsync(int? usuarioId) =>
        _context.Jogadores.AnyAsync(j => j.Id == usuarioId && (j.IsAdminGeral || j.IsAdminRaiz));
}
