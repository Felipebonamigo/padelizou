using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// "CAIU NA CHAVE" = FOI ELIMINADO NA FASE DE GRUPOS (a "chave" do torneio de clube).
//
// 🗣️ Felipe, 13/10/2026, com o print do próprio perfil (quartas no NATA, vice na 2ª etapa do
// EPT, inscrito no The Last Dance): *"aqui está marcando como caiu na chave, mas eu nunca caí na
// chave"*. O card mostrava 2 — contava as vezes que ele PASSOU da chave (quartas, semi, final),
// o avesso do que o nome diz.
//
// ⚠️ `UltimaFase = "Grupos"` sozinho NÃO é "caiu": é o valor de nascimento da dupla. Ele quer dizer
// "caiu nos grupos" só quando o torneio TERMINOU — antes disso é inscrito, está jogando os
// grupos, ou passou pro mata-mata e ainda não perdeu (o carimbo de fase só vai pra quem PERDE).
// E Americano não tem chave: é rodízio, e cada rodada vira uma dupla "Grupos".
public class CaiuNaChaveTests
{
    private static async Task<int> CaiuNaChaveDe(params (string status, string fase, string formato)[] participacoes)
    {
        using var ctx = TestInfra.NovoContexto();
        var eu = new Jogador { Nome = "Felipe Bonamigo", Cpf = "11144477735" };
        ctx.Jogadores.Add(eu);
        ctx.SaveChanges();

        int n = 0;
        foreach (var (status, fase, formato) in participacoes)
        {
            n++;
            var torneio = new Torneio
            {
                Nome = $"T{n}", Codigo = $"T{n}", DataInicio = new DateTime(2026, 9, n), Status = status, Formato = formato,
            };
            var categoria = new Categoria { Nome = "4ª Categoria Masculina", Codigo = $"C{n}", Torneio = torneio };
            ctx.AddRange(torneio, categoria);
            var parceiro = new Jogador { Nome = $"Parceiro {n}", Cpf = $"8880000{n:00}1" };
            ctx.Jogadores.Add(parceiro);
            ctx.SaveChanges();

            ctx.Duplas.Add(new Dupla
            {
                CategoriaId = categoria.Id, Jogador1Id = eu.Id, Jogador2Id = parceiro.Id, UltimaFase = fase,
            });
            ctx.SaveChanges();
        }

        return (await new EstatisticasService(ctx).ObterResumoJogadorAsync(eu.Id)).CaiuNaChave;
    }

    // O perfil do print: quartas, vice e um inscrito. Nunca caiu na chave.
    [Fact]
    public async Task Quem_sempre_passou_da_chave_nunca_caiu_nela()
    {
        Assert.Equal(0, await CaiuNaChaveDe(
            ("Finalizado", "Quartas de Final", FormatoDoTorneio.Padrao),
            ("Finalizado", "Final", FormatoDoTorneio.Padrao),
            (PortaDaInscricao.Aberta, "Grupos", FormatoDoTorneio.Padrao)));
    }

    [Fact]
    public async Task Torneio_terminado_com_a_dupla_ainda_em_Grupos_e_caiu_na_chave()
    {
        Assert.Equal(2, await CaiuNaChaveDe(
            ("Finalizado", "Grupos", FormatoDoTorneio.Padrao),
            ("Finalizado", "Grupos", FormatoDoTorneio.Padrao),
            ("Finalizado", "Semifinal", FormatoDoTorneio.Padrao)));
    }

    // Torneio que não terminou: "Grupos" ainda não é veredito — inclusive no mata-mata, onde quem
    // segue vivo continua "Grupos" até perder.
    [Theory]
    [InlineData(PortaDaInscricao.Aberta)]
    [InlineData(PortaDaInscricao.Fechada)]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    [InlineData(CancelamentoDoTorneio.Status)]
    public async Task Torneio_que_nao_terminou_nao_conta(string status)
    {
        Assert.Equal(0, await CaiuNaChaveDe((status, "Grupos", FormatoDoTorneio.Padrao)));
    }

    // Americano é rodízio: não tem chave pra cair.
    [Theory]
    [InlineData(FormatoDoTorneio.Americano)]
    [InlineData(FormatoDoTorneio.AmericanoDeDuplas)]
    public async Task Americano_nao_tem_chave(string formato)
    {
        Assert.Equal(0, await CaiuNaChaveDe(("Finalizado", "Grupos", formato)));
    }
}
