using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// FECHAR um desafio: gravar que ele valeu e congelar quanto valeu.
//
// Existe como serviço porque um desafio fecha por DOIS caminhos — a outra dupla confirma o
// placar, ou o prazo de 72h passa e o relógio confirma sozinho — e os dois precisam gravar
// exatamente a mesma coisa. Duas cópias divergiriam no dia em que só uma soubesse do
// anti-farm, e aí o mesmo jogo valeria diferente conforme alguém tivesse clicado ou não.
//
// ⚠️ OS PONTOS SÃO CONGELADOS AQUI, e nunca recalculados na leitura. É o mesmo padrão do
// `PrecoPorJogoCotado`: sem isso, mexer na fórmula em novembro reescreveria o ranking de
// agosto inteiro, em silêncio. O ranking (fase 2) só soma o que está gravado.
public class FechamentoDoDesafio
{
    private readonly DbPadelContext _context;

    public FechamentoDoDesafio(DbPadelContext context)
    {
        _context = context;
    }

    // confirmadoPorId nulo = foi o relógio, não uma pessoa. A distinção fica no banco de
    // propósito: "quem confirmou este placar?" é a primeira pergunta de qualquer discussão.
    public async Task FecharAsync(Desafio desafio, int? confirmadoPorId, DateTime agora,
        CancellationToken cancelationToken = default)
    {
        var niveis = await NiveisAsync(desafio, cancelationToken);

        var expectativaDoDesafiante = PontosDoDesafio.Expectativa(
            niveis.GetValueOrDefault(desafio.DesafianteJogador1Id),
            niveis.GetValueOrDefault(desafio.DesafianteJogador2Id),
            niveis.GetValueOrDefault(desafio.DesafiadoJogador1Id),
            niveis.GetValueOrDefault(desafio.DesafiadoJogador2Id));

        var repeticoes = await ConfrontosAnterioresNoMesAsync(desafio, agora, cancelationToken);

        var (desafiante, desafiado) = PontosDoDesafio.Do(
            EstadoDoDesafio.LadoVencedor(desafio), expectativaDoDesafiante, repeticoes);

        desafio.PontosDesafiante = desafiante;
        desafio.PontosDesafiado = desafiado;
        desafio.Status = Desafio.Confirmado;
        desafio.ConfirmadoEm = agora;
        desafio.ConfirmadoPorId = confirmadoPorId;

        await _context.SaveChangesAsync(cancelationToken);
    }

    // Quantas vezes ESTAS DUAS DUPLAS já fecharam um desafio no mês corrente. É o número que
    // desliga o farm (ver PontosDoDesafio.ComDescontoDeRepeticao).
    //
    // A consulta filtra por UM jogador e a comparação de dupla é feita na memória: casar dois
    // pares de ids em SQL sairia como uma cadeia de OR de dezesseis termos, ilegível e fácil
    // de errar. O conjunto é pequeno por natureza — são os desafios de uma pessoa num mês.
    public async Task<int> ConfrontosAnterioresNoMesAsync(Desafio desafio, DateTime agora,
        CancellationToken cancelationToken = default)
    {
        var inicioDoMes = new DateTime(agora.Year, agora.Month, 1);
        var eu = desafio.DesafianteJogador1Id;

        var candidatos = await _context.Desafios
            .AsNoTracking()
            .Where(x => x.Id != desafio.Id
                && x.Status == Desafio.Confirmado
                && x.ConfirmadoEm >= inicioDoMes
                && (x.DesafianteJogador1Id == eu || x.DesafianteJogador2Id == eu
                    || x.DesafiadoJogador1Id == eu || x.DesafiadoJogador2Id == eu))
            .ToListAsync(cancelationToken);

        return candidatos.Count(x => MesmoConfronto(x, desafio));
    }

    // O mesmo confronto, não importa quem desafiou quem: a dupla que desafiou na terça é a
    // desafiada no sábado, e para o anti-farm isso é o mesmo jogo de novo.
    public static bool MesmoConfronto(Desafio a, Desafio b)
    {
        var duplasDeA = new[]
        {
            ChaveDaDupla.De(a.DesafianteJogador1Id, a.DesafianteJogador2Id),
            ChaveDaDupla.De(a.DesafiadoJogador1Id, a.DesafiadoJogador2Id)
        };
        var duplasDeB = new[]
        {
            ChaveDaDupla.De(b.DesafianteJogador1Id, b.DesafianteJogador2Id),
            ChaveDaDupla.De(b.DesafiadoJogador1Id, b.DesafiadoJogador2Id)
        };

        return duplasDeA.OrderBy(c => c).SequenceEqual(duplasDeB.OrderBy(c => c));
    }

    private async Task<Dictionary<int, int?>> NiveisAsync(Desafio desafio, CancellationToken cancelationToken)
    {
        var ids = desafio.Envolvidos.Distinct().ToList();

        return await _context.Jogadores
            .AsNoTracking()
            .Where(j => ids.Contains(j.Id))
            .ToDictionaryAsync(j => j.Id, j => j.Padelimetro, cancelationToken);
    }
}
