using Padelizou.Services;

namespace Padelizou.Tests;

// O PERFIL DIZIA "FASE DE GRUPOS" DE TORNEIO QUE NEM COMEÇOU — 07/10/2026.
//
// 🗣️ Felipe, com o print do próprio perfil: *"o histórico dos torneios tem torneio q nem
// começou dizendo (fase de grupo) deveria ter algo dizendo que esta inscrito"*. No print, o
// THE LAST DANCE de **12/11/2026** — cinco semanas no futuro — com a pílula "Fase de Grupos".
//
// 🕳️ `Dupla.UltimaFase` nasce "Grupos", e esse é o valor de *"ainda não passou de fase"* — ele
// não distingue **"caiu nos grupos"** de **"nem jogou ainda"**. O rótulo lia só a dupla; quem
// sabe se o torneio começou é o TORNEIO. Enquanto as inscrições estão abertas, ou fechadas
// esperando o sorteio, a verdade sobre a dupla é **"Inscrito"**.
//
// ⚠️ O ESTADO NÃO MORA NUMA COLUNA NOVA. "Chaves em Sorteio" já significa inscrição fechada
// (ver Services/PortaDaInscricao) e "Inscrições Abertas" fala por si: os dois são "antes do
// sorteio". Uma flag `JaComecou` seria a segunda resposta pra mesma pergunta — e um dia as
// duas discordariam.
public class RotuloDoHistoricoDoPerfilTests
{
    [Theory]
    [InlineData(PortaDaInscricao.Aberta)]
    [InlineData(PortaDaInscricao.Fechada)]
    public void Torneio_que_ainda_nao_sorteou_mostra_Inscrito(string statusDoTorneio)
    {
        // "Grupos" é o valor com que toda dupla nasce — aqui ele quer dizer "nem jogou ainda".
        Assert.Equal("Inscrito", EstatisticasService.RotuloFase("Grupos", statusDoTorneio));
    }

    [Fact]
    public void Torneio_em_andamento_volta_a_dizer_Fase_de_Grupos()
    {
        // Agora "Grupos" quer dizer o que sempre quis: a dupla está (ou parou) na fase de grupos.
        Assert.Equal("Fase de Grupos", EstatisticasService.RotuloFase("Grupos", "Fase de Grupos"));
    }

    [Fact]
    public void Torneio_acabado_continua_dizendo_ate_onde_a_dupla_foi()
    {
        Assert.Equal("Vice", EstatisticasService.RotuloFase("Final", "Finalizado"));
        Assert.Equal("Campeão", EstatisticasService.RotuloFase("Campeao", "Finalizado"));
        Assert.Equal("Semifinal", EstatisticasService.RotuloFase("Semifinal", "Finalizado"));
    }

    [Fact]
    public void Campeao_de_torneio_que_nao_acabou_na_base_nao_vira_Inscrito()
    {
        // Defesa contra o conserto virar o próximo defeito: o campeão é fato consumado, e um
        // torneio que ficou com o status atrasado não pode apagar o título da pílula.
        Assert.Equal("Campeão", EstatisticasService.RotuloFase("Campeao", PortaDaInscricao.Fechada));
    }

    [Fact]
    public void Torneio_cancelado_diz_que_foi_cancelado()
    {
        // Sem isto, o torneio cancelado aparece como "Inscrito" pra sempre — convidando a
        // pessoa a esperar um jogo que não vai acontecer.
        Assert.Equal("Cancelado", EstatisticasService.RotuloFase("Grupos", CancelamentoDoTorneio.Status));
    }

    [Fact]
    public void As_duas_telas_de_historico_perguntam_o_status_do_torneio()
    {
        // Gate de tela: a regra só serve se as telas a chamarem COM o status. A pública ainda
        // escrevia o valor cru do banco ("Final", "Campeao") — o que nunca foi rótulo de tela.
        foreach (var tela in new[]
        {
            Tela("Views", "Auth", "Perfil.cshtml"),
            Tela("Views", "Jogadores", "Perfil.cshtml"),
        })
        {
            Assert.Contains("RotuloFase(dupla.UltimaFase, dupla.Categoria.Torneio.Status)", tela);
        }
    }

    [Fact]
    public void Os_destaques_mostram_o_sobrenome_de_quem_jogou_com_voce()
    {
        // 🗣️ Felipe, no mesmo print: *"aqui tem q por pelo menos o ultimo sobrenome dos
        // adversarios"*. Eram quatro `Nome.Split(' ')[0]`: numa panelinha com dois Lucas, o
        // card "quem mais te venceu" não dizia qual deles.
        var tela = Tela("Views", "Shared", "_DestaquesJogador.cshtml");

        Assert.DoesNotContain("Split(' ')[0]", tela);
        Assert.Equal(4, ContarOcorrencias(tela, "NomeBonito.Curto("));
    }

    private static int ContarOcorrencias(string texto, string trecho)
    {
        int quantas = 0, i = 0;
        while ((i = texto.IndexOf(trecho, i, StringComparison.Ordinal)) >= 0) { quantas++; i += trecho.Length; }
        return quantas;
    }

    private static string Tela(params string[] caminho)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a pasta Views.");

        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "Padelizou" }.Concat(caminho).ToArray()));
    }
}
