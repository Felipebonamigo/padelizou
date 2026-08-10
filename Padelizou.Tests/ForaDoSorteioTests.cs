using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// A regra sempre existiu dentro do GerarChaves, só que em silêncio: quem se inscreveu "ainda
// não tenho parceiro" descobria que ficou de fora quando a chave saía. Agora a tela avisa antes
// de sortear — e as duas leem daqui, senão a tela prometeria uma coisa e o sorteio faria outra.
public class ForaDoSorteioTests
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

    [Fact]
    public void Dupla_fechada_e_confirmada_entra_no_sorteio()
        => Assert.False(ForaDoSorteio.FicaDeFora(Inscricao(1, comParceiro: true)));

    [Fact]
    public void Sem_parceiro_fica_de_fora()
        => Assert.True(ForaDoSorteio.FicaDeFora(Inscricao(1, comParceiro: false)));

    [Fact]
    public void Na_lista_de_espera_fica_de_fora_mesmo_com_a_dupla_fechada()
        => Assert.True(ForaDoSorteio.FicaDeFora(Inscricao(1, comParceiro: true, naEspera: true)));

    [Fact]
    public void O_motivo_separa_o_que_cada_um_resolve()
    {
        // Sem parceiro é o jogador que resolve; espera é o organizador (ou uma desistência).
        Assert.Equal("sem parceiro", ForaDoSorteio.Motivo(Inscricao(1, comParceiro: false)));
        Assert.Equal("na lista de espera", ForaDoSorteio.Motivo(Inscricao(2, comParceiro: true, naEspera: true)));
    }

    [Fact]
    public void Time_nunca_fica_de_fora_por_estar_sem_parceiro()
    {
        // Time (categoria de times) não TEM parceiro: Jogador2Id nulo é a construção normal
        // dele. Sem esta regra, o sorteio pulava todos os times em silêncio.
        var time = Inscricao(9, comParceiro: false);
        time.NomeTime = "Nata Padel";

        Assert.False(ForaDoSorteio.FicaDeFora(time));
    }

    [Fact]
    public void Listar_traz_so_quem_fica_na_porta()
    {
        var duplas = new[]
        {
            Inscricao(1, comParceiro: true),                    // joga
            Inscricao(2, comParceiro: false),                   // sem parceiro
            Inscricao(3, comParceiro: true, naEspera: true),    // espera
            Inscricao(4, comParceiro: true),                    // joga
        };

        var fora = ForaDoSorteio.Listar(duplas);

        Assert.Equal(new[] { 2, 3 }, fora.Select(d => d.Id));
    }

    // ── As duas escritas da mesma régua ───────────────────────────────────────────────
    [Fact]
    public void A_versao_que_roda_no_banco_concorda_com_a_que_roda_em_memoria()
    {
        // ⚠️ ESTE É O TESTE QUE IMPEDE O DEFEITO CLÁSSICO DESTA CASA. `EstaNaChave` existe
        // porque o EF não traduz `FicaDeFora` (recebe entidade e lê `Completa`, propriedade
        // calculada), então a régua está escrita DUAS vezes — e no par irmão
        // `ContaNoRanking`/`DuplaContaNoRanking` a cópia escrita à mão já divergiu em
        // silêncio: foi assim que o Americano continuou pontuando no ranking oficial.
        //
        // Desde 10/08/2026 esta régua decide PONTO DE RANKING (RANKING.md, Trilha B), então
        // uma divergência aqui não some só do sorteio — ela paga, ou deixa de pagar, ponto.
        var naChave = ForaDoSorteio.EstaNaChave.Compile();

        var time = Inscricao(9, comParceiro: false);
        time.NomeTime = "Nata Padel";

        var timeNaEspera = Inscricao(10, comParceiro: false, naEspera: true);
        timeNaEspera.NomeTime = "Clube dos Feras";

        var todosOsCasos = new[]
        {
            Inscricao(1, comParceiro: true),                   // joga
            Inscricao(2, comParceiro: false),                  // sem parceiro
            Inscricao(3, comParceiro: true, naEspera: true),   // espera
            Inscricao(4, comParceiro: false, naEspera: true),  // os dois defeitos juntos
            time,                                              // time entra sem parceiro
            timeNaEspera,                                      // time, mesmo marcado na espera
        };

        foreach (var d in todosOsCasos)
            Assert.True(ForaDoSorteio.FicaDeFora(d) != naChave(d),
                $"As duas escritas discordam sobre a dupla {d.Id} "
                + $"(NomeTime={d.NomeTime ?? "null"}, Jogador2Id={d.Jogador2Id?.ToString() ?? "null"}, "
                + $"EmListaDeEspera={d.EmListaDeEspera}).");
    }
}
