using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// 10/09/2026 — DA LISTA DE JOGOS PRO CHAVEAMENTO, EM UM TOQUE.
//
// 🗣️ Deivid Santos, do 2ª Etapa ER PADEL TOUR, com o print da tela /Torneios/Jogos aberta:
// *"Eu to nessa tela"* … *"Ta ruim de achar o chaveamento"*.
//
// 🕳️ A página dedicada de jogos é a PORTA DE ENTRADA de quatro avisos diferentes (ver
// Torneio.ChavesAvisadasEm e QuadraAtrasadaBackgroundService) — quem toca no push cai nela.
// E ela não tinha uma única saída: nem pro torneio, nem pras chaves. As abas mãe só existem
// no /Torneios/Details, e quem chegou pelo aviso não tem como saber disso.
public class CaminhoParaAsChavesNaListaDeJogosTests
{
    [Theory]
    [InlineData("Fase de Grupos")]
    [InlineData("Mata-Mata")]
    [InlineData("Finalizado")]
    public void A_aba_de_chaves_existe_com_o_torneio_em_andamento(string status)
    {
        Assert.True(AbaDeChavesEGrupos.Existe(new Torneio { Status = status }, podeAprovarChaves: false));
    }

    [Fact]
    public void Chave_ainda_em_aprovacao_so_pra_quem_aprova()
    {
        var emAprovacao = new Torneio { Status = AprovacaoDeChaves.Pendente };

        // Os jogos existem no banco, mas não são públicos: pro jogador é como se a chave ainda
        // não tivesse saído — e um botão levaria a uma aba que não está lá pra ele.
        Assert.False(AbaDeChavesEGrupos.Existe(emAprovacao, podeAprovarChaves: false));
        Assert.True(AbaDeChavesEGrupos.Existe(emAprovacao, podeAprovarChaves: true));
    }

    [Theory]
    [InlineData("Inscrições Abertas")]
    [InlineData("Chaves em Sorteio")]
    [InlineData("Cancelado")]
    public void Sem_chave_sorteada_nao_ha_aba_pra_apontar(string status)
    {
        Assert.False(AbaDeChavesEGrupos.Existe(new Torneio { Status = status }, podeAprovarChaves: true));
    }

    [Fact]
    public void A_pagina_de_jogos_leva_pro_torneio_e_pras_chaves()
    {
        var fonte = PaginaDeJogos();

        // A volta pro torneio: sem ela, quem chegou pelo aviso fica sem as abas mãe.
        Assert.Contains("asp-action=\"Details\"", fonte);
        // E o atalho direto pra aba de chaves — a hash é o que a abre (ver o script no fim do
        // Details.cshtml), então o link precisa carregá-la.
        Assert.Contains("asp-fragment=\"grupos\"", fonte);
        Assert.Contains("Chaves e Grupos", fonte);
        // Só quando a aba existe de verdade: link que cai numa aba inexistente é pior que
        // link nenhum, porque some sem dizer o que houve.
        Assert.Contains("AbaDeChavesEGrupos.Existe", fonte);
    }

    [Fact]
    public void A_aba_do_Details_usa_a_MESMA_regua()
    {
        // Duas cópias da lista de status é como as duas telas passam a discordar: o botão da
        // lista de jogos prometendo uma aba que o Details não desenha.
        Assert.Contains("AbaDeChavesEGrupos.Existe", Details());
    }

    private static string PaginaDeJogos() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "jogos.cshtml"));

    private static string Details() =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var alvo = Path.Combine(dir.FullName, "Padelizou", "Views");
            if (Directory.Exists(alvo)) return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
