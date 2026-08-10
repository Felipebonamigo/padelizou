using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Padelizou.Controllers;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A CONSULTA DE CPF DA INSCRIÇÃO, depois da varredura de conformidade de 10/08/2026.
//
// Ela existe porque digitar CPF, nome, celular e cidade do parceiro num celular, no dia da
// inscrição, é o degrau mais alto do formulário — e é dado que o sistema já tem. O jeito como
// ela devolvia esse dado é que estava errado, em três frentes:
//
//  1. GET com o CPF na query string. O documento ia parar no log de acesso do servidor, no
//     histórico do navegador e no cabeçalho `Referer` do próximo link clicado. Nada disso é
//     lugar de CPF, e nada disso é apagável depois.
//  2. Sem trava nenhuma. "Exige o CPF inteiro" parecia a defesa — mas CPF válido se GERA: o
//     dígito verificador é uma conta de dois passos, não um segredo. Uma sessão logada varria
//     a base colhendo nome e telefone, sem limite.
//  3. Devolvia CELULAR e CIDADE de terceiro. A busca por NOME do mesmo formulário já não
//     fazia isso de propósito (ver Services/BuscaJogador, e o comentário na tela) — a busca
//     por CPF, ao lado dela, fazia.
//
// ⚠️ O contato de quem está LOGADO continua vindo: é o botão "Sou eu", que preenche o próprio
// cadastro da pessoa. Um teste aqui existe só pra isso não ser "consertado" junto.
public class BuscaPorCpfNaoVazaContatoTests
{
    // CPFs com dígito verificador de verdade: a ação recusa qualquer outro antes de consultar.
    private const string CpfDeQuemInscreve = "11144477735";
    private const string CpfDoParceiro = "52998224725";

    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador
            {
                Id = 1, Nome = "Quem Inscreve", Cpf = CpfDeQuemInscreve, SenhaHash = "hash",
                Celular = "51999990001", Cidade = "Porto Alegre", Estado = "RS",
            },
            new Jogador
            {
                Id = 2, Nome = "Parceiro Alheio", Cpf = CpfDoParceiro, SenhaHash = "hash",
                Apelido = "Foka", Celular = "51999990002", Cidade = "Gravataí", Estado = "RS",
            });
        ctx.SaveChanges();
        return ctx;
    }

    private static Task<JsonElement> ConsultarAsync(string cpf, int logadoComo = 1) =>
        ConsultarAsync(Cenario(), cpf, logadoComo);

    private static async Task<JsonElement> ConsultarAsync(DbPadelContext ctx, string cpf, int logadoComo)
    {
        var controller = TestInfra.NovoTorneiosController(ctx, usuarioLogadoId: logadoComo);
        var corpo = Assert.IsType<JsonResult>(await controller.BuscarJogadorPorCpf(cpf)).Value;

        // Serializar em vez de ler o objeto anônimo por reflexão: é exatamente o que o
        // navegador recebe, que é o que este arquivo inteiro está checando.
        return JsonDocument.Parse(JsonSerializer.Serialize(corpo)).RootElement;
    }

    private static MethodInfo Acao() =>
        typeof(TorneiosController).GetMethod(nameof(TorneiosController.BuscarJogadorPorCpf))!;

    // ===================== O CPF NÃO ANDA NA BARRA DE ENDEREÇO =====================

    [Fact]
    public void A_consulta_e_POST_e_nao_aceita_mais_GET()
    {
        Assert.NotEmpty(Acao().GetCustomAttributes<HttpPostAttribute>());

        Assert.Empty(Acao().GetCustomAttributes<HttpGetAttribute>());
    }

    [Fact]
    public void A_consulta_tem_trava_por_CONTA()
    {
        var trava = Assert.Single(Acao().GetCustomAttributes<EnableRateLimitingAttribute>());

        // Por conta, e não por IP: o Wi-Fi do clube em dia de torneio põe 30 pessoas no mesmo
        // endereço, cada uma consultando o CPF do parceiro (ver Services/TravaDeEntrada).
        Assert.Equal(TravaDeEntrada.PoliticaConsultaLogada, trava.PolicyName);
    }

    // ===================== CONTATO DE TERCEIRO NÃO SAI DO SERVIDOR =====================

    [Fact]
    public async Task O_CPF_de_OUTRA_pessoa_nao_devolve_celular_nem_cidade()
    {
        var achado = await ConsultarAsync(CpfDoParceiro);

        Assert.True(achado.GetProperty("encontrado").GetBoolean());
        Assert.Equal("", achado.GetProperty("celular").GetString());
        Assert.Equal("", achado.GetProperty("cidade").GetString());
        Assert.Equal("", achado.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task O_nome_continua_vindo_porque_e_publico()
    {
        // Nome e apelido já estão no ranking e na chave do torneio. Escondê-los aqui não
        // esconderia nada de ninguém — e a tela precisa confirmar que é quem se pensa.
        var achado = await ConsultarAsync(CpfDoParceiro);

        Assert.Equal("Parceiro Alheio", achado.GetProperty("nome").GetString());
        Assert.Equal("Foka", achado.GetProperty("apelido").GetString());
    }

    [Fact]
    public async Task A_tela_sabe_que_o_cadastro_dele_JA_TEM_o_contato_sem_receber_o_numero()
    {
        // É esse "sim" que deixa a tela trancar o campo vazio e tirar o `required`: quem
        // completa é o servidor, na gravação. Sem ele, o formulário pediria pra quem inscreve
        // digitar o telefone do parceiro — que é o dado que a gente acabou de esconder.
        var achado = await ConsultarAsync(CpfDoParceiro);

        Assert.True(achado.GetProperty("temCelular").GetBoolean());
        Assert.True(achado.GetProperty("temCidade").GetBoolean());
        Assert.True(achado.GetProperty("temEstado").GetBoolean());
    }

    [Fact]
    public async Task Cadastro_sem_celular_continua_deixando_digitar()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador
        {
            Id = 1, Nome = "Quem Inscreve", Cpf = CpfDeQuemInscreve, SenhaHash = "hash",
        });
        // Cadastro antigo, sem celular: se a tela trancasse o campo mesmo assim, a inscrição
        // empacaria num campo obrigatório que ninguém pode preencher.
        ctx.Jogadores.Add(new Jogador { Id = 2, Nome = "Sem Telefone", Cpf = CpfDoParceiro });
        ctx.SaveChanges();

        var achado = await ConsultarAsync(ctx, CpfDoParceiro, logadoComo: 1);

        Assert.False(achado.GetProperty("temCelular").GetBoolean());
    }

    // ===================== O PRÓPRIO CADASTRO CONTINUA VINDO =====================

    [Fact]
    public async Task Quem_digita_o_PROPRIO_CPF_recebe_o_proprio_contato()
    {
        // O botão "Sou eu" existe porque o dado é da pessoa e ela vai digitá-lo de qualquer
        // jeito. Cortar isto junto seria trocar um vazamento por um formulário pior.
        var meu = await ConsultarAsync(CpfDeQuemInscreve, logadoComo: 1);

        Assert.Equal("51999990001", meu.GetProperty("celular").GetString());
        Assert.Equal("Porto Alegre", meu.GetProperty("cidade").GetString());
        Assert.Equal("RS", meu.GetProperty("estado").GetString());
    }

    [Fact]
    public async Task O_mesmo_CPF_visto_por_OUTRA_conta_nao_devolve_nada()
    {
        // A régua é quem está logado, não qual CPF é: o cadastro do jogador 1 sai pra ele e
        // não sai pro jogador 2.
        var visto = await ConsultarAsync(CpfDeQuemInscreve, logadoComo: 2);

        Assert.Equal("", visto.GetProperty("celular").GetString());
        Assert.True(visto.GetProperty("temCelular").GetBoolean());
    }

    // ===================== A TELA TAMBÉM =====================
    // O servidor pode ser POST e a tela continuar chamando por GET: o [HttpPost] devolveria
    // 405 e a consulta simplesmente pararia de funcionar, calada — o `catch` da tela destranca
    // os campos e a inscrição segue. Ninguém veria defeito nenhum, e o CPF voltaria pra query
    // string no dia em que alguém copiasse este bloco pra uma tela nova.

    [Fact]
    public void A_tela_de_inscricao_manda_o_CPF_no_CORPO_da_requisicao()
    {
        var tela = Tela();

        Assert.DoesNotContain("urlBusca + '?cpf='", tela);
        Assert.Contains("body: 'cpf=' + cpf", tela);
    }

    [Fact]
    public void As_DUAS_consultas_da_tela_levam_o_carimbo_antifalsificacao()
    {
        // São dois blocos (inscrever a dupla e trocar o parceiro) e eles chamam a MESMA ação.
        // Sem o carimbo, o POST volta 400 e a tela fica "consultando" pra sempre.
        var tela = Tela();

        Assert.Equal(2, Regex.Matches(tela, @"urlBusca, \{").Count);
        Assert.Equal(2, Regex.Matches(tela, @"cabecalhoAntifalsificacao\(").Count);
    }

    private static string Tela() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
            {
                return Path.Combine(dir.FullName, "Padelizou");
            }
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
