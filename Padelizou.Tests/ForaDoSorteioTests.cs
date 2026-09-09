using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// QUEM O SORTEIO DEIXA DE FORA — e, desde 09/09/2026, só isso.
//
// 🗣️ Felipe: "tem q manter o Paulo, ele vai colocar o parceiro dele depois". Quem se inscreve
// sozinho passou a ENTRAR na chave, ocupando a vaga dele com a segunda posição em aberto; o
// segundo nome entra depois, até a dupla ter o primeiro jogo com placar. Se nunca entrar, a
// dupla leva W.O. — risco assumido, e o organizador é avisado na hora de sortear.
//
// ⚠️ ESTA RÉGUA JÁ RESPONDEU DUAS PERGUNTAS AO MESMO TEMPO, e é a coisa mais importante a se
// saber sobre este arquivo. Até aqui ela dizia "entra no sorteio?" E "conta pro ranking?" —
// então mudar o sorteio pagaria ponto de participação a quem não jogou, incharia o peso da
// categoria (subindo o ponto até do campeão) e faria isso RETROATIVAMENTE, porque a régua não
// tem data de corte. A metade do ranking mudou de casa: Services/InscricaoQueConta, com a
// semântica de sempre e o teste dela. Aqui ficou só o sorteio.
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
    public void Sem_parceiro_ENTRA_no_sorteio_com_a_vaga_em_aberto()
    {
        // A regra virou de lado em 09/09/2026. Antes: "sem parceiro fica de fora". Agora a
        // vaga é dele, e o parceiro entra depois.
        Assert.False(ForaDoSorteio.FicaDeFora(Inscricao(1, comParceiro: false)));
    }

    [Fact]
    public void Na_lista_de_espera_fica_de_fora_mesmo_com_a_dupla_fechada()
        => Assert.True(ForaDoSorteio.FicaDeFora(Inscricao(1, comParceiro: true, naEspera: true)));

    [Fact]
    public void Sem_parceiro_E_na_espera_fica_de_fora_pela_espera()
    {
        // Inscrição sozinha PODE estar na lista de espera (a vaga acabou antes de ela fechar
        // a dupla). O que a mantém fora agora é só a espera — se ela for chamada, entra
        // mesmo sem parceiro.
        Assert.True(ForaDoSorteio.FicaDeFora(Inscricao(1, comParceiro: false, naEspera: true)));
    }

    [Fact]
    public void O_motivo_de_ficar_de_fora_agora_e_um_so()
    {
        // Sobrou uma razão só: sem vaga. "Sem parceiro" deixou de tirar ninguém do sorteio.
        Assert.Equal("na lista de espera", ForaDoSorteio.Motivo(Inscricao(1, comParceiro: true, naEspera: true)));
        Assert.Equal("na lista de espera", ForaDoSorteio.Motivo(Inscricao(2, comParceiro: false, naEspera: true)));
    }

    [Fact]
    public void Time_nunca_fica_de_fora_por_estar_sem_parceiro()
    {
        // Time (categoria de times) não TEM parceiro: Jogador2Id nulo é a construção normal
        // dele. Continua valendo, e agora pelo mesmo motivo de todo mundo.
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
            Inscricao(2, comParceiro: false),                   // joga, com a vaga em aberto
            Inscricao(3, comParceiro: true, naEspera: true),    // espera
            Inscricao(4, comParceiro: true),                    // joga
        };

        var fora = ForaDoSorteio.Listar(duplas);

        Assert.Equal(new[] { 3 }, fora.Select(d => d.Id));
    }

    [Fact]
    public void Quem_entra_com_a_vaga_em_aberto_e_listado_a_parte()
    {
        // O organizador perdeu o aviso antigo (a lista de "fica de fora") justamente pra quem
        // ele mais precisa ver: quem entra incompleto é quem pode virar W.O. Esta lista é o
        // que substitui aquele aviso na tela do sorteio.
        var duplas = new[]
        {
            Inscricao(1, comParceiro: true),                     // fechada
            Inscricao(2, comParceiro: false),                    // entra com vaga aberta
            Inscricao(3, comParceiro: false, naEspera: true),    // não entra: espera
            Inscricao(4, comParceiro: false),                    // entra com vaga aberta
        };

        var comVagaAberta = ForaDoSorteio.ComVagaEmAberto(duplas);

        Assert.Equal(new[] { 2, 4 }, comVagaAberta.Select(d => d.Id));
    }

    [Fact]
    public void Time_nunca_aparece_como_vaga_em_aberto()
    {
        // Jogador2Id nulo num time não é vaga nenhuma — é a construção normal dele. Listá-lo
        // faria o organizador procurar um parceiro que não existe.
        var time = Inscricao(9, comParceiro: false);
        time.NomeTime = "Nata Padel";

        Assert.Empty(ForaDoSorteio.ComVagaEmAberto(new[] { time }));
    }
}
