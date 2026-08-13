using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using padelizou.Models;
using Xunit;

namespace Padelizou.Tests;

// 13/08/2026 — o bloco do Pix pedia o comprovante e não dava caminho nenhum.
//
// "Mande o comprovante pra ele depois de pagar" é a frase que estava na tela, sem dizer PRA
// QUEM nem COMO. Quem já tinha o WhatsApp do organizador resolvia por fora; quem não tinha
// ficava com a inscrição pendurada esperando alguém conferir um pagamento que ninguém viu —
// e o Padelizou não recebe nem confere esse dinheiro, então não há a quem recorrer aqui.
public class ComprovanteDoPixDoOrganizadorTests
{
    // `CobraPeloSite` é calculada a partir da FORMA de recebimento — não dá pra ligar e
    // desligar na mão, e é bom que não dê: a forma é o que o jogador leu quando se inscreveu.
    private static Torneio TorneioPorFora() => new()
    {
        Nome = "NATA PADEL TOUR",
        Codigo = "PIXTST",
        FormaPagamento = FormaDePagamentoDoTorneio.Externo,
        ChavePixOrganizador = "51994643580",
        PrecoInscricao = 150m,
    };

    [Fact]
    public void O_bloco_do_Pix_aparece_no_por_fora_com_chave_e_preco()
    {
        Assert.True(PixDoOrganizador.Aparece(TorneioPorFora()));
    }

    [Fact]
    public void Torneio_que_cobra_pelo_site_NAO_mostra_chave_Pix()
    {
        var t = TorneioPorFora();
        t.FormaPagamento = FormaDePagamentoDoTorneio.TodasAsFormas;

        // Dois caminhos pro mesmo dinheiro: quem pagasse o Pix ficaria fora do controle do
        // checkout, e o organizador teria duas listas de quem pagou.
        Assert.True(t.CobraPeloSite);
        Assert.False(PixDoOrganizador.Aparece(t));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_chave_cadastrada_nao_ha_bloco(string? chave)
    {
        var t = TorneioPorFora();
        t.ChavePixOrganizador = chave;

        Assert.False(PixDoOrganizador.Aparece(t));
    }

    [Fact]
    public void Torneio_de_graca_nao_pede_comprovante_de_nada()
    {
        var t = TorneioPorFora();
        t.PrecoInscricao = 0m;

        Assert.False(PixDoOrganizador.Aparece(t));
    }

    // ⚠️ A ARMADILHA DESTE ARQUIVO. Nada no cadastro impede um torneio de ter mais de um
    // "Criador" — o painel admin já registra isso. Sem uma ordem TOTAL, qual deles aparece
    // muda de um carregamento pro outro, e o jogador manda o comprovante pra pessoas
    // diferentes a cada vez: o organizador A jura que não recebeu, o B não sabe de nada.
    [Fact]
    public async Task Com_DOIS_criadores_o_comprovante_vai_sempre_pro_mesmo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneioId, primeiro, _) = await MontarComDoisCriadoresAsync(ctx);

        for (int i = 0; i < 5; i++)
        {
            var quem = await PixDoOrganizador.QuemRecebeOComprovanteAsync(ctx, torneioId);
            Assert.Equal(primeiro, quem!.Id);
        }
    }

    [Fact]
    public async Task Ajudante_organiza_mas_NAO_e_quem_recebe_o_comprovante()
    {
        using var ctx = TestInfra.NovoContexto();

        var criador = new Jogador { Nome = "Dona do caixa", Cpf = "88800000001", Login = "caixa" };
        var ajudante = new Jogador { Nome = "Quem ajuda na mesa", Cpf = "88800000002", Login = "mesa" };
        ctx.Jogadores.AddRange(criador, ajudante);
        await ctx.SaveChangesAsync();

        var torneio = TorneioPorFora();
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        // O ajudante entra com Id MENOR de propósito: se a consulta esquecesse o NivelAcesso,
        // a ordenação por Id entregaria justamente ele.
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = ajudante.Id, NivelAcesso = "Ajudante",
        });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = criador.Id, NivelAcesso = "Criador",
        });
        await ctx.SaveChangesAsync();

        var quem = await PixDoOrganizador.QuemRecebeOComprovanteAsync(ctx, torneio.Id);

        Assert.Equal(criador.Id, quem!.Id);
    }

    [Fact]
    public async Task Torneio_sem_criador_nenhum_devolve_nada_em_vez_de_estourar()
    {
        using var ctx = TestInfra.NovoContexto();
        var torneio = TorneioPorFora();
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        Assert.Null(await PixDoOrganizador.QuemRecebeOComprovanteAsync(ctx, torneio.Id));
    }

    // O botão só nasce com número utilizável. Sem isso a tela abriria uma conversa com um
    // número quebrado — pior que não ter botão, porque parece que o recado foi.
    [Theory]
    [InlineData("51994643580", true)]   // celular com o 9
    [InlineData("5133334444", true)]    // fixo
    [InlineData("(51) 99464-3580", true)]
    [InlineData("51", false)]
    [InlineData("-", false)]
    [InlineData(null, false)]
    public void O_botao_do_WhatsApp_exige_numero_de_verdade(string? celular, bool esperado)
    {
        Assert.Equal(esperado, WhatsAppLinkHelper.NumeroValido(celular));
    }

    [Fact]
    public void A_mensagem_diz_de_qual_torneio_e_avisa_que_o_comprovante_vem_junto()
    {
        var texto = PixDoOrganizador.Mensagem("NATA PADEL TOUR", "Rafael Harff");

        Assert.Contains("NATA PADEL TOUR", texto);
        Assert.Contains("Rafael Harff", texto);
        // O wa.me não anexa arquivo: a mensagem precisa anunciar o que vem em seguida, senão
        // o organizador recebe um "oi" solto.
        Assert.Contains("comprovante", texto);
    }

    [Fact]
    public void Sem_nome_do_jogador_a_mensagem_nao_fica_com_buraco()
    {
        var texto = PixDoOrganizador.Mensagem("NATA PADEL TOUR", null);

        Assert.DoesNotContain("Sou .", texto);
        Assert.DoesNotContain("  ", texto);
        Assert.Contains("NATA PADEL TOUR", texto);
    }

    private static async Task<(int torneioId, int primeiro, int segundo)> MontarComDoisCriadoresAsync(
        DbPadelContext ctx)
    {
        var a = new Jogador { Nome = "Criador A", Cpf = "88800000003", Login = "cria" };
        var b = new Jogador { Nome = "Criador B", Cpf = "88800000004", Login = "crib" };
        ctx.Jogadores.AddRange(a, b);
        await ctx.SaveChangesAsync();

        var torneio = TorneioPorFora();
        ctx.Torneios.Add(torneio);
        await ctx.SaveChangesAsync();

        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = b.Id, NivelAcesso = "Criador",
        });
        ctx.TorneioOrganizadores.Add(new TorneioOrganizador
        {
            TorneioId = torneio.Id, JogadorId = a.Id, NivelAcesso = "Criador",
        });
        await ctx.SaveChangesAsync();

        var primeiro = Math.Min(a.Id, b.Id);
        return (torneio.Id, primeiro, Math.Max(a.Id, b.Id));
    }
}
