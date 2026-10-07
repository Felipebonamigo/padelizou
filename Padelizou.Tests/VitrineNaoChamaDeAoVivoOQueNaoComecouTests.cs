using Padelizou.Services;

namespace Padelizou.Tests;

// "ACONTECENDO AGORA" COM SELO VERMELHO "CHAVES EM APROVAÇÃO" — 07/10/2026.
//
// 🗣️ Felipe, com o print da Home: *"nao pode ter isso, tem q ficar apenas inscrições
// encerradas"*. No print, o NATA PADEL TOUR dentro de "Acontecendo agora", com um selo vermelho
// de transmissão ao vivo escrito **"Chaves em Aprovação"** e o botão "Acompanhar".
//
// 🕳️ A Home montava "em andamento" como *tudo que não é Inscrições Abertas*. Isso inclui os DOIS
// estados de ANTES do jogo — inscrição fechada esperando o sorteio e chave sorteada esperando
// aprovação — e a tela os tratava como torneio rolando: título "Acontecendo agora", selo vermelho
// de ao vivo, botão "Acompanhar". Pior: "Chaves em Aprovação" é um passo INTERNO do organizador
// (ver Services/AprovacaoDeChaves — a chave só é pública depois de aprovada), e vazava crua pra
// quem olha a vitrine.
//
// ⚠️ A TRADUÇÃO É SÓ DE EXIBIÇÃO, e é uma função NOVA em vez de mexer na `Nome`: a tela de gestão
// do torneio (Details) também usa `Nome`, e lá o organizador PRECISA ler "Chaves em Aprovação" —
// é onde ele aprova. Quem olha de fora lê só "encerradas".
public class VitrineNaoChamaDeAoVivoOQueNaoComecouTests
{
    [Theory]
    [InlineData(PortaDaInscricao.Fechada)]
    [InlineData(AprovacaoDeChaves.Pendente)]
    public void Antes_do_jogo_a_vitrine_diz_so_que_as_inscricoes_encerraram(string status)
    {
        Assert.Equal("Inscrições Encerradas", StatusDoTorneioNaTela.NomePublico(status));
    }

    [Theory]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    [InlineData("Inscrições Abertas")]
    public void Os_outros_status_saem_como_estao(string status)
    {
        Assert.Equal(status, StatusDoTorneioNaTela.NomePublico(status));
    }

    [Fact]
    public void Status_nulo_nao_estoura()
    {
        Assert.Equal("", StatusDoTorneioNaTela.NomePublico(null));
    }

    [Theory]
    [InlineData(PortaDaInscricao.Fechada, true)]
    [InlineData(AprovacaoDeChaves.Pendente, true)]
    [InlineData("Fase de Grupos", false)]
    [InlineData("Mata-Mata", false)]
    [InlineData("Inscrições Abertas", false)]
    public void So_e_ao_vivo_o_que_ja_tem_chave_publicada(string status, bool aindaNaoComecou)
    {
        Assert.Equal(aindaNaoComecou, StatusDoTorneioNaTela.AindaNaoComecou(status));
    }

    [Fact]
    public void O_status_do_organizador_continua_inteiro_na_tela_de_gestao()
    {
        // Quem aprova a chave precisa ler que ela está em aprovação.
        Assert.Equal("Chaves em Aprovação", StatusDoTorneioNaTela.Nome(AprovacaoDeChaves.Pendente));
    }

    [Fact]
    public void A_Home_nao_escreve_mais_o_status_cru_nem_chama_tudo_de_ao_vivo()
    {
        var home = Tela("Views", "Home", "Index.cshtml");

        // Os dois selos (o do "Seus torneios" e o da seção de baixo) leem o nome PÚBLICO.
        Assert.DoesNotContain("StatusDoTorneioNaTela.Nome(", home);
        Assert.Contains("StatusDoTorneioNaTela.NomePublico(", home);

        // E "rolando" deixou de ser "tudo que não é Inscrições Abertas".
        Assert.DoesNotContain("Status != \"Inscrições Abertas\"", home);
        Assert.Contains("StatusDoTorneioNaTela.AindaNaoComecou(", home);
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
