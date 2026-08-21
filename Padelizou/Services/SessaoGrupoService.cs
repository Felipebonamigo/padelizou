using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

public class SessaoGrupoService : ISessaoGrupoService
{
    private readonly DbPadelContext _context;

    public SessaoGrupoService(DbPadelContext context)
    {
        _context = context;
    }

    public async Task<SessaoGrupo> ObterOuCriarSessaoAsync(GrupoPrivado grupo, DateTime? dataSolicitada)
    {
        var dataHora = dataSolicitada ?? ProximaOcorrencia(grupo.DiaSemanaFixo!.Value, grupo.HorarioFixo!.Value);

        var sessao = await _context.SessoesGrupo
            .Include(s => s.Confirmacoes).ThenInclude(c => c.Jogador)
            .FirstOrDefaultAsync(s => s.GrupoId == grupo.Id && s.DataHora == dataHora);

        if (sessao != null)
        {
            // ⚠️ A sessão nasce com os membros DAQUELE momento. Quem entra na panelinha depois
            // ficava sem linha de confirmação — e sem linha a tela não mostra os botões de
            // "Vou jogar / Não vou": a pessoa abria o jogo da semana e não tinha o que responder.
            //
            // Só mexe em sessão de hoje pra frente: reconciliar semana passada só sujaria o
            // histórico com "pendentes" de gente que nem era do grupo naquele dia.
            if (sessao.DataHora.Date >= DateTime.Today && await AdicionarMembrosQueFaltamAsync(sessao))
            {
                return await RecarregarAsync(sessao.Id);
            }
            return sessao;
        }

        sessao = new SessaoGrupo { GrupoId = grupo.Id, DataHora = dataHora };
        _context.SessoesGrupo.Add(sessao);
        await _context.SaveChangesAsync();

        await AdicionarMembrosQueFaltamAsync(sessao);

        return await RecarregarAsync(sessao.Id);
    }

    // Cria a confirmação dos membros do grupo que ainda não têm uma nesta sessão.
    // Devolve true se criou alguma.
    private async Task<bool> AdicionarMembrosQueFaltamAsync(SessaoGrupo sessao)
    {
        var jaTem = await _context.ConfirmacoesSessao
            .Where(c => c.SessaoId == sessao.Id)
            .Select(c => c.JogadorId)
            .ToListAsync();

        // ⚠️ `ExcluidoEm == null` — O ITEM MAIS SILENCIOSO DESTE ARQUIVO. Excluir a conta
        // ANONIMIZA em vez de apagar (Services/ExclusaoDeConta): o nome vira "Jogador removido"
        // e a senha some, mas a linha em `JogadoresGrupo` FICA — não existe lugar nenhum no
        // sistema que a remova. Sem este filtro, "Jogador removido" ganha linha nova toda semana,
        // entra na lista da quadra e no sorteio de duplas, e não consegue nem sair de lá: a conta
        // não loga mais. Antes da presunção isso era feio e inócuo (uma linha "Pendente" que
        // ninguém lia); agora seria a afirmação "essa pessoa vai jogar".
        var faltando = await _context.JogadoresGrupo
            .Where(jg => jg.GrupoId == sessao.GrupoId
                      && !jaTem.Contains(jg.JogadorId)
                      && jg.Jogador.ExcluidoEm == null)
            .Select(jg => jg.Jogador)
            .ToListAsync();

        if (faltando.Count == 0) return false;

        // ⚠️ A RÉGUA DE NASCIMENTO MORA AQUI DENTRO, e não no chamador. Dois caminhos chegam
        // neste método (sessão nova e reconciliação de quem entrou depois) e os dois precisam da
        // MESMA guarda de data: a tela da Semana tem "semana anterior", que manda data
        // arbitrária, e sem a guarda clicar duas vezes pra trás escreveria "o grupo inteiro
        // estava lá" num jogo de julho que ninguém jogou. Ver PresencaNaSessao.
        var status = PresencaNaSessao.StatusDeNascimento(sessao.DataHora, DateTime.Now);

        foreach (var membro in faltando)
        {
            _context.ConfirmacoesSessao.Add(new ConfirmacaoSessao
            {
                SessaoId = sessao.Id,
                JogadorId = membro.Id,
                Status = status,
                Lado = membro.LadoQuadra,
                Avulso = false
            });
        }
        await _context.SaveChangesAsync();
        return true;
    }

    private async Task<SessaoGrupo> RecarregarAsync(int sessaoId) =>
        await _context.SessoesGrupo
            .Include(s => s.Confirmacoes).ThenInclude(c => c.Jogador)
            .FirstAsync(s => s.Id == sessaoId);

    public static DateTime ProximaOcorrencia(int diaSemanaFixo, TimeSpan horarioFixo)
    {
        var hoje = DateTime.Today;
        var diasAteAlvo = (diaSemanaFixo - (int)hoje.DayOfWeek + 7) % 7;
        var data = hoje.AddDays(diasAteAlvo).Add(horarioFixo);
        if (data < DateTime.Now) data = data.AddDays(7);
        return data;
    }
}
