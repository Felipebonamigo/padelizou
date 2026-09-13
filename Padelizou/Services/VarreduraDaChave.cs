using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A CHAVE QUE FICOU PRA TRÁS É MONTADA DEPOIS, SOZINHA.
//
// 🕳️ O INCIDENTE QUE FEZ ISTO EXISTIR (ER PADEL TOUR, 12/09/2026, com o torneio em quadra).
// Duas categorias ficaram com a fase de grupos TODA fechada e o mata-mata **não montado** — em
// silêncio, sem erro em lugar nenhum, até o Felipe reparar olhando a tela: *"é que eles ja não
// são mais prévias, no momento que elas passaram de chave ele se torna real e não prévia"*.
//
// A causa não era o chaveamento — reproduzi as duas formas em teste e o robô monta certo. Era a
// ENTREGA: `RoboDoChaveamento.MontarMataMataDosGruposAsync` só roda no INSTANTE em que um jogo
// de grupo é finalizado (`EncerramentoDaPartida`), e **nada tentava de novo** se aquela chamada
// se perdesse. Bateu com os dois restarts de deploy do dia: o placar gravou, o processo morreu
// antes do robô, e a categoria ficou parada esperando um evento que não volta.
//
// ⚠️ O SINTOMA É DOS MAIS CAROS QUE EXISTEM: não há exceção, não há log, não há teste vermelho.
// A tela mostra a prévia — que é uma tela legítima — e só quem conhece o torneio percebe que
// aquilo já devia ter virado chave. Mesma família do "sistema mudo" de 11/09.
//
// ✅ NENHUMA RÉGUA NOVA. A varredura chama os MESMOS dois robôs, que já são guardados contra
// rodar duas vezes (`mataMataJaGerado` no primeiro, o contador `jaCriados` nos dois). O que
// muda é só passar a chamá-los de novo — é conserto de entrega, não de chaveamento.
public class VarreduraDaChave
{
    private readonly DbPadelContext _context;
    private readonly RoboDoChaveamento _robo;
    private readonly ILogger<VarreduraDaChave> _logger;

    // ⚠️ O ROBÔ É CONSTRUÍDO AQUI, e não injetado: `RoboDoChaveamento` NÃO está registrado no
    // contêiner — quem precisa dele faz `new`, com o contexto e o ranking na mão (é o que o
    // `EncerramentoDaPartida` faz, na linha 40). Pedi-lo por injeção compilou, passou na suíte
    // inteira e quebrou no CI, no passo que valida o contêiner: *"Unable to resolve service for
    // type 'RoboDoChaveamento'"*. A suíte não monta o service provider, então esta família de
    // defeito é invisível aqui — só o `dotnet ef` do CI a enxerga.
    public VarreduraDaChave(DbPadelContext context, IEstatisticasService estatisticas,
        ILogger<VarreduraDaChave> logger)
    {
        _context = context;
        _robo = new RoboDoChaveamento(context, estatisticas);
        _logger = logger;
    }

    private static bool EhDeGrupo(string fase) =>
        fase == "Fase de Grupos" || fase.StartsWith("Grupo ", StringComparison.Ordinal);

    /// <summary>
    /// Uma passada: monta o que ficou pra trás e devolve quantas coisas destravou.
    /// </summary>
    public async Task<int> PassarAsync(CancellationToken ct)
    {
        // ⚠️ EXCLUI O QUE PRECISA SER EXCLUÍDO, EM VEZ DE ADIVINHAR O NOME DO STATUS
        // (12/09/2026). A primeira versão varria só `Status == "Fase de Grupos"` — e qualquer
        // outro estado de torneio em andamento (o histórico "Mata-Mata", por exemplo) saía da
        // varredura EM SILÊNCIO, que é exatamente o defeito que esta classe existe pra matar.
        // Chave não publicada não tem mata-mata pra montar; torneio finalizado e cancelado são
        // passado. O resto entra — os robôs são guardados, então varrer à toa não faz nada.
        var torneios = await _context.Torneios
            .Where(t => t.Status != AprovacaoDeChaves.Pendente
                     && t.Status != "Finalizado"
                     && t.Status != "Inscrições Abertas"
                     && !t.Status.StartsWith("Cancelado"))
            .Select(t => t.Id)
            .ToListAsync(ct);

        int destravou = 0;

        foreach (var torneioId in torneios)
        {
            if (ct.IsCancellationRequested) break;

            // 🔒 A MESMA TRAVA DO ENCERRAMENTO, e ela não é opcional: a varredura e um placar
            // sendo lançado na Mesa no mesmo segundo montariam a fase DUAS vezes — exatamente o
            // buraco que essa trava existe pra fechar (ver EncerramentoDaPartida).
            using var trava = await EncerramentoDaPartida.UmDeCadaVezPorTorneioAsync(torneioId);

            // Uma consulta só pro torneio inteiro, projetada: a decisão de quem precisa do robô
            // é feita em memória, e só quem precisa paga uma ida ao banco.
            // ⚠️ PELO CAMINHO `Categoria.Torneio`, E NÃO POR `Partida.TorneioId` — o mesmo
            // motivo que `AprovacaoDeChaves.Publicada` já traz escrito ao lado: aquele campo é
            // ANULÁVEL (jogo avulso não tem torneio) e a categoria é obrigatória. A primeira
            // versão filtrava por `p.TorneioId`, e uma partida de torneio com o campo frouxo
            // sumia da varredura — a categoria ficava travada pra sempre, em silêncio.
            var partidas = await _context.Partidas
                .Where(p => p.Categoria.TorneioId == torneioId)
                .Select(p => new { p.CategoriaId, p.Fase, p.Status })
                .ToListAsync(ct);

            foreach (var daCategoria in partidas.GroupBy(p => p.CategoriaId))
            {
                int categoriaId = daCategoria.Key;
                var deGrupo = daCategoria.Where(p => EhDeGrupo(p.Fase)).ToList();
                var doMataMata = daCategoria.Where(p => !EhDeGrupo(p.Fase)).ToList();

                try
                {
                    // 1. A fase de grupos fechou e o mata-mata não nasceu — o caso do ER.
                    if (deGrupo.Count > 0 && deGrupo.All(p => p.Status == "Finalizada")
                        && doMataMata.Count == 0)
                    {
                        await _robo.MontarMataMataDosGruposAsync(categoriaId, torneioId);
                        destravou++;
                        _logger.LogWarning(
                            "Varredura da chave: montei o mata-mata da categoria {CategoriaId} "
                            + "(torneio {TorneioId}) — a chamada do encerramento tinha se perdido.",
                            categoriaId, torneioId);
                        continue;
                    }

                    // 2. Uma fase do mata-mata fechou inteira e a seguinte não nasceu. Mesmo
                    //    buraco, outro robô — e ele é guardado pelo contador `jaCriados`, então
                    //    chamar à toa não cria nada.
                    foreach (var fase in doMataMata.GroupBy(p => p.Fase))
                    {
                        if (ChaveamentoMataMata.ProximaFase(fase.Key) == null) continue;
                        if (!fase.All(p => p.Status == "Finalizada")) continue;

                        await _robo.AvancarFaseAsync(categoriaId, torneioId, fase.Key);
                    }
                }
                catch (Exception ex)
                {
                    // Uma categoria torta não pode travar as outras: o valor desta varredura é
                    // justamente rodar quando algo já deu errado.
                    _logger.LogError(ex,
                        "Varredura da chave falhou na categoria {CategoriaId} (torneio {TorneioId})",
                        categoriaId, torneioId);
                }
            }
        }

        return destravou;
    }
}
