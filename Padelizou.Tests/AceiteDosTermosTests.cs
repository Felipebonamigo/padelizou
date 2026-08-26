using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// A PROVA DE QUE A PESSOA ACEITOU — que até 10/08/2026 não existia.
//
// O cadastro não dizia uma palavra sobre os Termos nem sobre a Política (os dois só existiam
// num link do rodapé) e não guardava nada. O art. 8º §2 da LGPD põe o ônus de PROVAR o
// consentimento em quem controla os dados: sem registro, a resposta a "você concordou com
// isso?" era encolher os ombros.
//
// Duas metades, e uma sozinha não prova nada:
//   · a tela DIZ, na hora de decidir, com link pros dois documentos;
//   · a linha da pessoa guarda QUANDO e QUAL VERSÃO (Jogador.TermosAceitosEm).
public class AceiteDosTermosTests
{
    // ===================== A TELA DIZ =====================

    [Fact]
    public void O_cadastro_mostra_os_dois_documentos_antes_do_botao()
    {
        var tela = Tela(Path.Combine("Views", "Auth", "Cadastro.cshtml"));

        Assert.Matches(@"asp-action=""Termos""", tela);
        Assert.Matches(@"asp-action=""Privacy""", tela);
        Assert.Contains("Ao finalizar, você aceita", tela);

        // Antes do botão, não depois: aviso embaixo do "Finalizar Cadastro" é aviso que
        // ninguém lê, porque a decisão já foi tomada quando os olhos chegam lá.
        Assert.True(tela.IndexOf("Ao finalizar, você aceita", StringComparison.Ordinal)
                    < tela.IndexOf("Finalizar Cadastro", StringComparison.Ordinal),
            "A frase do aceite foi parar DEPOIS do botão de finalizar.");
    }

    // ===================== UMA VERSÃO SÓ, NUM LUGAR SÓ =====================

    [Theory]
    [InlineData("Privacy.cshtml")]
    [InlineData("Termos.cshtml")]
    public void As_paginas_leem_a_data_da_versao_em_vez_de_escreve_la_a_mao(string arquivo)
    {
        // A data escrita à mão nas duas páginas é o mesmo defeito do preço cravado nos Termos
        // (ver TermosDeUsoTests) — mas pior: a página diria uma versão e o banco guardaria
        // outra, e a prova viraria contradição.
        var pagina = Tela(Path.Combine("Views", "Home", arquivo));

        Assert.Contains("VersaoDosDocumentos.EmVigorDesdeNaTela", pagina);

        var dataCravada = Regex.Match(pagina, @"Em vigor desde \d");
        Assert.False(dataCravada.Success,
            $"{arquivo} voltou a trazer a data da versão escrita à mão. Ela mora em "
            + "Services/VersaoDosDocumentos, que é a MESMA string gravada em "
            + "Jogador.VersaoDosTermosAceita.");
    }

    [Fact]
    public void A_versao_atual_e_a_data_em_vigor_contam_a_mesma_coisa()
    {
        // O nome da versão É a data em que ela passou a valer. Mudar uma e esquecer a outra
        // faria a tela dizer um dia e o registro guardar outro.
        Assert.Equal(VersaoDosDocumentos.Atual, VersaoDosDocumentos.EmVigorDesde.ToString("yyyy-MM-dd"));
    }

    // ===================== A LINHA DA PESSOA GUARDA =====================

    [Fact]
    public async Task Quem_cria_conta_fica_com_a_data_e_a_versao_gravadas()
    {
        using var ctx = TestInfra.NovoContexto();
        var controller = TestInfra.NovoAuthController(ctx);

        await Cadastrar(controller, cpf: "11144477735", login: "novato");

        var criado = ctx.Jogadores.Single(j => j.Cpf == "11144477735");
        Assert.NotNull(criado.TermosAceitosEm);
        Assert.Equal(VersaoDosDocumentos.Atual, criado.VersaoDosTermosAceita);
    }

    [Fact]
    public async Task Quem_ASSUME_um_pre_cadastro_tambem_fica_com_o_aceite()
    {
        // ⚠️ O caso que um `if` no ramo errado deixaria de fora. A linha dele já existia (um
        // organizador o inscreveu numa dupla por CPF), então o cadastro CAI NO OUTRO RAMO —
        // e é agora, com a tela dos dois documentos na frente dele, que ele aceita.
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Id = 9, Nome = "Parceiro Sem Conta", Cpf = "52998224725" });
        ctx.SaveChanges();

        var controller = TestInfra.NovoAuthController(ctx);
        await Cadastrar(controller, cpf: "52998224725", login: "assumiu");

        var assumida = ctx.Jogadores.Single(j => j.Id == 9);
        Assert.False(assumida.EhPreCadastro);
        Assert.NotNull(assumida.TermosAceitosEm);
        Assert.Equal(VersaoDosDocumentos.Atual, assumida.VersaoDosTermosAceita);
    }

    [Fact]
    public void Conta_antiga_fica_com_NULO_em_vez_de_uma_data_inventada()
    {
        // Nulo quer dizer "não temos registro", que é diferente de "não aceitou". Preencher
        // as 154 contas existentes com a data de hoje seria fabricar consentimento — o
        // oposto exato do que este arquivo existe pra garantir.
        var antiga = new Jogador { Id = 1, Nome = "Quem Ja Existia", Cpf = "1", SenhaHash = "hash" };

        Assert.Null(antiga.TermosAceitosEm);
        Assert.Null(antiga.VersaoDosTermosAceita);
    }

    // ===================== A PESSOA VÊ O QUE ACEITOU =====================

    [Fact]
    public void As_preferencias_mostram_o_recibo_do_aceite()
    {
        // Prova guardada só do nosso lado não serve: quem aceitou tem direito de saber o quê
        // e quando (LGPD, art. 9º). E a tela precisa dar conta do nulo sem mentir.
        var tela = Tela(Path.Combine("Views", "Auth", "Preferencias.cshtml"));

        Assert.Contains("Model.TermosAceitosEm", tela);
        Assert.Contains("Você aceitou os", tela);
        Assert.Contains("não temos essa data guardada", tela);
    }

    // ---- Bastidores ----

    private static async Task Cadastrar(padelizou.Controllers.AuthController controller, string cpf, string login)
    {
        var resultado = await controller.Cadastro(
            nome: "Fulano de Tal", cpf: cpf, login: login, email: $"{login}@exemplo.com",
            senha: "segredo123", celular: null, isProfessor: false, foto: null!,
            ladoQuadra: null, lateralidade: null, instagram: null,
            notificarEmail: false, notificarWhatsApp: false,
            categoriasSelecionadas: null, clubesSelecionados: null, diasHorariosSelecionados: null,
            sexo: SexoDoJogador.Masculino);

        // Cadastro recusado volta pro formulário (ViewResult) com a explicação no ViewBag —
        // sem esta conferência, um teste de aceite passaria "provando" que ninguém aceitou.
        Assert.IsNotType<ViewResult>(resultado);
    }

    private static string Tela(string caminhoRelativo) =>
        Regex.Replace(File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo)),
            @"@\*.*?\*@", "", RegexOptions.Singleline);

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
