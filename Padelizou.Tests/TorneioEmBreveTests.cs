using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Criar o torneio com as inscrições ainda FECHADAS (08/08/2026, pedido do Felipe).
//
// Quem monta o torneio em duas sentadas era obrigado a deixar o formulário aceitando gente
// enquanto ainda mexia em categoria, quadra e preço — e a inscrição que entra no meio disso
// pega o torneio pela metade.
//
// Na vitrine ele vai pra seção "Em breve": não é "aberto" (ninguém consegue entrar) nem "em
// andamento" (não começou nada), e sem seção própria ele era anunciado como acontecendo.
public class TorneioEmBreveTests
{
    private static Torneio Com(string status) =>
        new() { Nome = "NATA PADEL TOUR", Codigo = "NATA1", Status = status };

    [Fact]
    public void Fechado_e_vazio_e_um_torneio_em_breve()
    {
        Assert.True(PortaDaInscricao.NuncaAbriu(
            Com(PortaDaInscricao.Fechada), temInscrito: false, jaSorteou: false));
    }

    [Fact]
    public void Fechado_com_gente_dentro_nao_e_em_breve()
    {
        // Esse encerrou de verdade: teve inscrição. O lugar dele é "Em Andamento".
        Assert.False(PortaDaInscricao.NuncaAbriu(
            Com(PortaDaInscricao.Fechada), temInscrito: true, jaSorteou: false));
    }

    [Fact]
    public void Cancelado_vazio_NAO_aparece_como_em_breve()
    {
        // ⚠️ A régua exige o status FECHADA, e não só "não está aberto": cancelado também não
        // está aberto e, sem ninguém dentro, apareceria na vitrine como um torneio que está
        // pra abrir — justamente o que ele nunca vai fazer.
        Assert.False(PortaDaInscricao.NuncaAbriu(
            Com(CancelamentoDoTorneio.Status), temInscrito: false, jaSorteou: false));
    }

    [Fact]
    public void Finalizado_vazio_NAO_aparece_como_em_breve()
    {
        Assert.False(PortaDaInscricao.NuncaAbriu(
            Com("Finalizado"), temInscrito: false, jaSorteou: false));
    }

    [Fact]
    public void Aberto_nao_e_em_breve()
    {
        Assert.False(PortaDaInscricao.NuncaAbriu(
            Com(PortaDaInscricao.Aberta), temInscrito: false, jaSorteou: false));
    }

    [Fact]
    public void Um_torneio_em_breve_pode_ser_aberto_pelo_botao()
    {
        // É o par do "criar fechado": sem isso o torneio nasceria num beco.
        Assert.Null(PortaDaInscricao.PorQueNaoPodeAbrir(
            Com(PortaDaInscricao.Fechada), jaSorteou: false));
    }
}
