using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A ÚLTIMA porta que criava clube sem saber onde ele fica: o "cadastre um local novo" que
// aparece em Criar Aviso, Configurações do grupo e Registrar Jogo. As outras duas
// (cadastro/preferências e criação de torneio) já perguntavam a cidade desde 11/08/2026.
//
// ⚠️ Esta porta era TRÊS telas com o mesmo JavaScript copiado byte a byte, mais uma quarta
// cópia morta no site.js. Enquanto fossem quatro, a cidade teria que ser lembrada quatro
// vezes — e o dia em que alguém lembrasse de três, o local nasceria sem cidade numa tela só,
// sem erro nenhum.
public class CadastroDeLocalPorAjaxTests
{
    private static ClubesController Controller(DbPadelContext ctx) => new(ctx);

    [Fact]
    public async Task Local_cadastrado_pelo_grupo_nasce_com_a_cidade()
    {
        var ctx = TestInfra.NovoContexto();

        var resposta = await Controller(ctx).Criar("Quadra do Zé", null, "Gravataí", "rs") as JsonResult;

        Assert.NotNull(resposta);
        var clube = await ctx.Clubes.Include(c => c.Cidade).FirstAsync();
        Assert.Equal("Gravataí", clube.Cidade!.Nome);
        Assert.Equal("RS", clube.Cidade.Estado);   // normalizado, mesmo digitado "rs"
    }

    // A cidade é OPCIONAL aqui de propósito: o local costuma ser "onde a gente jogou", e
    // exigir endereço no meio de registrar um jogo é atrito na hora errada.
    [Fact]
    public async Task Sem_cidade_o_local_e_criado_do_mesmo_jeito()
    {
        var ctx = TestInfra.NovoContexto();

        var resposta = await Controller(ctx).Criar("Quadra do Zé") as JsonResult;

        Assert.NotNull(resposta);
        Assert.Null((await ctx.Clubes.FirstAsync()).CidadeId);
    }

    // Mesma regra das outras portas: nome repetido devolve o que já existe (não duplica), e
    // clube antigo sem cidade recebe a que acabou de ser informada.
    [Fact]
    public async Task Nome_repetido_nao_duplica_e_ainda_preenche_a_cidade_que_faltava()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Clubes.Add(new Clube { Id = 1, Nome = "Radar", Endereco = "", Contato = "" });
        await ctx.SaveChangesAsync();

        await Controller(ctx).Criar("radar", null, "Porto Alegre", "RS");

        Assert.Equal(1, await ctx.Clubes.CountAsync());
        Assert.NotNull((await ctx.Clubes.FirstAsync()).CidadeId);
    }

    // ⚠️ A régua passou de 120 pra 80 (a do CatalogoLocais) porque era ele quem CORTAVA em 80
    // silenciosamente. Recusar é melhor que gravar pela metade e a pessoa nunca entender.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Nome_vazio_e_recusado(string nome)
    {
        var ctx = TestInfra.NovoContexto();
        Assert.IsType<BadRequestResult>(await Controller(ctx).Criar(nome));
    }

    [Fact]
    public async Task Nome_maior_que_o_limite_e_recusado_em_vez_de_cortado()
    {
        var ctx = TestInfra.NovoContexto();
        var comprido = new string('a', CatalogoLocais.TamanhoMaximoNome + 1);

        Assert.IsType<BadRequestResult>(await Controller(ctx).Criar(comprido));
        Assert.Equal(0, await ctx.Clubes.CountAsync());
    }

    // ── AS TRÊS TELAS ────────────────────────────────────────────────────────────────────
    // Guardas de ligação: o defeito aqui seria mudo — a tela abre, o botão aparece, e só não
    // faz nada (ou cadastra sem cidade).

    [Theory]
    [InlineData("Avisos", "Criar.cshtml")]
    [InlineData("Grupos", "Configuracoes.cshtml")]
    [InlineData("Grupos", "RegistrarJogo.cshtml")]
    public void As_tres_telas_usam_o_arquivo_compartilhado_e_pedem_a_cidade(string pasta, string arquivo)
    {
        var tela = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", pasta, arquivo));

        Assert.Contains("~/js/adicionar-local.js", tela);
        // Sem o carimbo de versão, quem já tem o app instalado continua rodando a versão
        // velha do script — que não manda cidade nenhuma.
        Assert.Matches(new Regex(@"adicionar-local\.js""\s+asp-append-version=""true"""), tela);

        Assert.Contains("id=\"novoClubeCidade\"", tela);
        Assert.Contains("id=\"novoClubeEstado\"", tela);

        // A cópia inline não pode voltar: era ela que fazia a mesma regra viver em 3 lugares.
        Assert.DoesNotContain("function adicionarClubeSelect", tela);
    }

    // ⚠️ Ela sumiu de propósito (código morto, zero chamadores). Se voltar, volta uma cópia do
    // POST sem cidade e sem conferir `res.ok`.
    [Fact]
    public void A_copia_morta_do_site_js_nao_voltou()
    {
        var siteJs = File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "site.js"));
        Assert.DoesNotContain("function adicionarClube(", siteJs);
    }

    // O erro mudo que existia: `res.json()` direto quebra a promessa quando o servidor recusa,
    // e pra quem clicou o botão simplesmente não faz nada.
    [Fact]
    public void O_cadastro_por_ajax_confere_a_resposta_do_servidor()
    {
        var js = File.ReadAllText(Path.Combine(PastaDoProjeto(), "wwwroot", "js", "adicionar-local.js"));

        Assert.Contains("res.ok", js);
        Assert.Contains(".catch(", js);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
