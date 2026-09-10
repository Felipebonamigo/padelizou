using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A RÉGUA DO RANKING, SEPARADA DA RÉGUA DO SORTEIO (09/09/2026).
//
// 🗣️ Felipe: "tem q manter o Paulo, ele vai colocar o parceiro dele depois" — a dupla sem
// parceiro passou a ENTRAR no sorteio, com a segunda vaga em aberto.
//
// 💥 O QUE ISSO IA QUEBRAR, e é a razão deste arquivo existir: até aqui uma régua só respondia
// DUAS perguntas — "entra no sorteio?" e "conta pro ranking?" —, e as duas escritas
// (`ForaDoSorteio.FicaDeFora`, em memória, e `ForaDoSorteio.EstaNaChave`, em SQL) eram
// complementares por teste. Mudar o sorteio arrastaria o ranking junto, e aí:
//
//   1. `Dupla.UltimaFase` NASCE valendo "Grupos" e `PontosDoTorneio.Pontos` paga
//      participação × peso assim que o torneio começa, sem olhar UMA partida. Quem entrasse
//      na chave sem parceiro e levasse W.O. levaria ponto por não aparecer — reabrindo o
//      buraco que a RANKING.md fechou em 10/08/2026 ("o ponto só nasce quando a bola rola").
//   2. A mesma régua é o CONTADOR do peso da categoria (EstatisticasService). A dupla
//      incompleta incharia a categoria e subiria o ponto de TODO MUNDO nela — do campeão
//      inclusive, que não tem nada a ver com isso.
//   3. E seria RETROATIVO: a régua não tem data de corte, então reescreveria o ranking de
//      todo torneio que já teve inscrito sozinho.
//
// Por isso as duas perguntas se separaram: `ForaDoSorteio` responde só pelo SORTEIO, e
// `InscricaoQueConta` guarda — intacta — a semântica que o ranking, o peso da categoria, o
// MVP e a enquete sempre tiveram.
public class InscricaoQueContaTests
{
    private static Dupla Inscricao(int id, bool comParceiro, bool naEspera = false) =>
        new()
        {
            Id = id,
            Codigo = $"D{id}",
            Jogador1Id = 1,
            Jogador2Id = comParceiro ? 2 : null,
            EmListaDeEspera = naEspera,
        };

    private static Dupla Time(int id, bool naEspera = false)
    {
        var time = Inscricao(id, comParceiro: false, naEspera: naEspera);
        time.NomeTime = "Nata Padel";
        return time;
    }

    // ── O QUE O RANKING SEMPRE FEZ, E CONTINUA FAZENDO ────────────────────────────────────

    [Fact]
    public void Dupla_fechada_conta() => Assert.True(InscricaoQueConta.Vale(Inscricao(1, comParceiro: true)));

    [Fact]
    public void Sem_parceiro_nao_conta() => Assert.False(InscricaoQueConta.Vale(Inscricao(1, comParceiro: false)));

    [Fact]
    public void Na_lista_de_espera_nao_conta()
        => Assert.False(InscricaoQueConta.Vale(Inscricao(1, comParceiro: true, naEspera: true)));

    [Fact]
    public void Time_conta_mesmo_sem_parceiro() => Assert.True(InscricaoQueConta.Vale(Time(9)));

    [Fact]
    public void Time_conta_mesmo_marcado_na_lista_de_espera()
    {
        // ⚠️ Parece contraintuitivo e é de propósito: `EhTime` vem ANTES da espera nas duas
        // escritas, então um time marcado na espera conta. Time não passa por lista de espera —
        // quem monta a lista é o organizador —, e a coluna só existe porque time e dupla
        // dividem a mesma tabela.
        //
        // Isto está escrito como TESTE, e não como comentário no meio do array de casos, porque
        // ali eu tinha deixado o comentário INVERTIDO ("time na espera não conta"): o assert de
        // concordância compara as duas escritas entre si, então ele passava do mesmo jeito. Numa
        // base em que comentário é memória, um comentário errado é convite pra alguém "consertar"
        // a régua — e mexer nesta régua é mexer em ponto de ranking, retroativamente.
        Assert.True(InscricaoQueConta.Vale(Time(10, naEspera: true)));
        Assert.True(InscricaoQueConta.Expressao.Compile()(Time(10, naEspera: true)));
    }

    // ── A SEPARAÇÃO, QUE É O CORAÇÃO DA MUDANÇA ───────────────────────────────────────────

    [Fact]
    public void Sem_parceiro_ENTRA_no_sorteio_mas_NAO_conta_no_ranking()
    {
        // Este é o teste que trava a decisão inteira. Se um dia alguém "simplificar" as duas
        // réguas de volta numa só, é aqui que estoura — e o estrago que ele evita é ponto de
        // ranking pago a quem não jogou, retroativo sobre a história toda.
        var solo = Inscricao(1, comParceiro: false);

        Assert.False(ForaDoSorteio.FicaDeFora(solo));   // entra na chave: a vaga é dele
        Assert.False(InscricaoQueConta.Vale(solo));     // mas não pontua e não incha o peso
    }

    [Fact]
    public void Na_lista_de_espera_fica_de_fora_dos_DOIS()
    {
        // A espera é o caso em que as duas réguas continuam concordando: quem não tem vaga
        // não entra no sorteio nem conta pro ranking.
        var esperando = Inscricao(2, comParceiro: true, naEspera: true);

        Assert.True(ForaDoSorteio.FicaDeFora(esperando));
        Assert.False(InscricaoQueConta.Vale(esperando));
    }

    // ── AS DUAS ESCRITAS DA MESMA RÉGUA ───────────────────────────────────────────────────

    [Fact]
    public void A_versao_que_roda_no_banco_concorda_com_a_que_roda_em_memoria()
    {
        // ⚠️ O MESMO GUARDRAIL QUE O ForaDoSorteioTests TINHA, herdado junto com a régua: o EF
        // não traduz `Vale` (recebe entidade e lê `Completa`, propriedade calculada), então ela
        // está escrita DUAS vezes — e no par irmão `ContaNoRanking`/`DuplaContaNoRanking` a
        // cópia à mão já divergiu em silêncio, que foi como o Americano continuou pontuando.
        //
        // Aqui a divergência não some só de uma tela: esta régua PAGA, ou deixa de pagar, ponto.
        var expressao = InscricaoQueConta.Expressao.Compile();

        var todosOsCasos = new[]
        {
            Inscricao(1, comParceiro: true),                   // conta
            Inscricao(2, comParceiro: false),                  // sem parceiro
            Inscricao(3, comParceiro: true, naEspera: true),   // espera
            Inscricao(4, comParceiro: false, naEspera: true),  // os dois de uma vez
            Time(9),                                           // time conta sem parceiro
            Time(10, naEspera: true),                          // time conta mesmo marcado na espera
        };

        foreach (var d in todosOsCasos)
            Assert.True(InscricaoQueConta.Vale(d) == expressao(d),
                $"As duas escritas discordam sobre a dupla {d.Id} "
                + $"(NomeTime={d.NomeTime ?? "null"}, Jogador2Id={d.Jogador2Id?.ToString() ?? "null"}, "
                + $"EmListaDeEspera={d.EmListaDeEspera}).");
    }
}
