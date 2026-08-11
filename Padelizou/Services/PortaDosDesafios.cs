using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Quem pode abrir os Desafios — uma régua, um lugar.
//
// Existe como serviço, e não como um `if` copiado em cada ação, porque a resposta é a mesma
// pro menu, pro mural, pro anúncio e pra cada POST. Duas cópias de uma regra de permissão
// divergem no dia em que só uma delas é atualizada — e aí o módulo "invisível" aparece por
// uma porta que ninguém lembrou de fechar.
//
// ⚠️ Esconder o link do menu é CORTESIA. A trava de verdade é o `PodeUsarAsync` repetido em
// toda ação do controller: sem ele, /Desafios continuaria respondendo pra quem digitasse o
// endereço na barra.
//
// ⚠️ E aqui NÃO há a divisão ver × editar do resto do sistema. Isto não é um perfil de
// permissão: é uma feature que ainda não nasceu, e um módulo em construção com meia porta
// aberta é pior do que fechado. Enquanto `Desafios__Habilitado` for false, quem entra são os
// dois admins — a mesma régua do ModuloDoBar, e de propósito SEM o assistente: o desafio é
// atividade de jogador (publicar anúncio, aceitar, lançar placar), e deixá-lo entrar num
// módulo em obra seria convidá-lo a escrever, que é justamente o que a flag dele proíbe.
public class PortaDosDesafios
{
    private readonly DbPadelContext _context;
    private readonly DesafiosSettings _settings;

    public PortaDosDesafios(DbPadelContext context, Microsoft.Extensions.Options.IOptions<DesafiosSettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    // Em construção = só admin do Padelizou. É isto que mantém o módulo invisível sem branch
    // separada (ver DesafiosSettings).
    public bool EmConstrucao => !_settings.Habilitado;

    public async Task<bool> PodeUsarAsync(int? usuarioId)
    {
        if (usuarioId == null) return false;
        if (!EmConstrucao) return true;

        return await EhAdminDoPadelizouAsync(usuarioId);
    }

    // Mesma pergunta, com o nome do que a tela faz com a resposta.
    public Task<bool> MostrarNoMenuAsync(int? usuarioId) => PodeUsarAsync(usuarioId);

    // As mesmas duas flags de PoderesNoSistema.PodeEditarTudo. Consultadas direto pra não
    // arrastar a linha inteira do jogador numa pergunta que roda em toda requisição.
    private Task<bool> EhAdminDoPadelizouAsync(int? usuarioId) =>
        _context.Jogadores.AnyAsync(j => j.Id == usuarioId && (j.IsAdminGeral || j.IsAdminRaiz));
}
