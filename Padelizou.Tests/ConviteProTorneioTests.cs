using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// "CONVIDAR" — O BOTÃO QUE MANDA O LINK DE INSCRIÇÃO PRA QUEM AINDA NÃO ESTÁ NO TORNEIO.
//
// Pedido do Felipe (04/09/2026), num print da página do torneio, com a área ao lado do
// "Cartaz pra divulgar" marcada de verde: *"crie um botão 'convidar' e que é um botão que
// direciona o link para a pessoa se inscrever no torneio"*.
//
// O cartaz já existia e resolve a divulgação ABERTA (story, feed, mural do clube). O que
// faltava era a divulgação DIRIGIDA: o organizador que quer chamar a Thayse e a Carol, uma a
// uma, no privado. Pro cartaz ele precisa baixar a imagem, achar a conversa e anexar; pro
// convite ele precisa de um toque.
//
// ⚠️ ESTE ARQUIVO TESTA A REGRA E O TEXTO. Quem desenha o botão é o Details.cshtml, e o teste
// dele é o `ConvidarNaPaginaDoTorneioTests` — a suíte não renderiza Razor.
public class ConviteProTorneioTests
{
    private static Torneio Torneio(string status, string nome = "Americano das gurias",
                                   DateTime? inicio = null, DateTime? fim = null) =>
        new() { Id = 7, Nome = nome, Status = status, DataInicio = inicio, DataFim = fim };

    private const string Link = "https://padelizou.com.br/Torneios/Details/7#inscricao";

    // ───────────────────────── QUANDO O BOTÃO EXISTE ─────────────────────────

    [Fact]
    public void Torneio_com_inscricoes_abertas_ganha_o_botao()
    {
        Assert.True(ConviteProTorneio.PodeConvidar(PortaDaInscricao.Aberta));
    }

    // ⚠️ A RÉGUA NÃO É A DO CARTAZ, e a diferença é o ponto deste arquivo.
    //
    // `DivulgacaoDoTorneio.PodeDivulgar` deixa passar o torneio EM ANDAMENTO — cartaz de
    // torneio rolando ainda convida a ASSISTIR, e por isso ele troca o selo pra "ACOMPANHE AO
    // VIVO". O convite não tem essa saída: ele promete inscrição, e a aba "Inscreva-se" só
    // existe enquanto o status é "Inscrições Abertas" (Details.cshtml). Reusar `PodeDivulgar`
    // aqui mandaria a pessoa pra uma página onde o botão prometido não existe.
    [Theory]
    [InlineData(PortaDaInscricao.Fechada)]      // Chaves em Sorteio
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    [InlineData("Finalizado")]
    [InlineData("Cancelado")]
    [InlineData(null)]
    public void Torneio_sem_inscricao_aberta_nao_ganha_o_botao(string? status)
    {
        Assert.False(ConviteProTorneio.PodeConvidar(status));
    }

    // ───────────────────────── O TEXTO ─────────────────────────

    // O link é a razão de a mensagem existir: sem ele o convite vira aviso, e quem recebe
    // fica sabendo do torneio sem ter como entrar.
    [Fact]
    public void O_texto_carrega_o_link()
    {
        var texto = ConviteProTorneio.Texto(Torneio(PortaDaInscricao.Aberta), "Paladino", Link);

        Assert.Contains(Link, texto);
    }

    [Fact]
    public void O_texto_carrega_o_nome_do_torneio()
    {
        var texto = ConviteProTorneio.Texto(
            Torneio(PortaDaInscricao.Aberta, "Americano das gurias - 2ª edição"), "Paladino", Link);

        Assert.Contains("Americano das gurias - 2ª edição", texto);
    }

    [Fact]
    public void Data_e_local_entram_quando_existem()
    {
        var texto = ConviteProTorneio.Texto(
            Torneio(PortaDaInscricao.Aberta, inicio: new DateTime(2026, 9, 7)), "Paladino", Link);

        Assert.Contains("07/09/2026", texto);
        Assert.Contains("Paladino", texto);
    }

    // ⚠️ "Data a definir" é boa no CARD da listagem (ali a pessoa compara torneios e precisa
    // saber que este ainda não tem dia) e ruim numa mensagem de WhatsApp: quem recebe o
    // convite lê "Data a definir" como "ainda não vale a pena olhar". Sem data, o convite não
    // fala de data — o link leva pra página, que diz tudo.
    [Fact]
    public void Torneio_sem_data_nao_anuncia_data_a_definir()
    {
        var texto = ConviteProTorneio.Texto(Torneio(PortaDaInscricao.Aberta), "Paladino", Link);

        Assert.DoesNotContain("a definir", texto);
        Assert.Contains("Paladino", texto);
    }

    // Só um dos dois: o separador não aparece, porque não há o que separar.
    [Fact]
    public void Sem_local_o_texto_nao_deixa_separador_solto()
    {
        var texto = ConviteProTorneio.Texto(
            Torneio(PortaDaInscricao.Aberta, inicio: new DateTime(2026, 9, 7)), null, Link);

        Assert.Contains("07/09/2026", texto);
        Assert.DoesNotContain("·", texto);
    }

    // ⚠️ ESTE É O TESTE QUE TRAVA A GUARDA `contexto.Count > 0`, e ele existe porque a primeira
    // versão NÃO travava: eu tinha escrito `Assert.DoesNotContain("·")` achando que a guarda
    // impedia um separador pendurado, e apagar a guarda deixou a suíte VERDE. `string.Join`
    // nunca deixa separador solto — o que ela impede é o `\n` de uma linha do meio VAZIA, que
    // abre um buraco entre o nome do torneio e o link.
    [Fact]
    public void Sem_data_e_sem_local_a_mensagem_nao_ganha_linha_vazia()
    {
        var texto = ConviteProTorneio.Texto(Torneio(PortaDaInscricao.Aberta), null, Link);

        Assert.Contains("Americano das gurias", texto);
        Assert.Contains(Link, texto);
        Assert.DoesNotContain("\n\n", texto);
        Assert.Equal(2, texto.Split('\n').Length);
    }

    // Local em branco não é local. Um `""` vindo de um campo não preenchido cairia na mesma
    // linha vazia que o `null` cairia sem a checagem.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Local_em_branco_conta_como_ausente(string local)
    {
        var texto = ConviteProTorneio.Texto(Torneio(PortaDaInscricao.Aberta), local, Link);

        Assert.DoesNotContain("\n\n", texto);
        Assert.Equal(2, texto.Split('\n').Length);
    }
}
