using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// 10/09/2026 — A BARRA DE AÇÕES DA LINHA DE JOGO VAZAVA PRA FORA DO CARTÃO NO CELULAR.
//
// 🗣️ Felipe, com o Painel de Controle aberto no celular: *"a tela esta estourando aqui ao usar
// no mobile"* — e a captura mostrava o play verde do lado de FORA da borda esquerda do cartão.
//
// A causa é a combinação de duas regras que sozinhas estão certas: `.pdz-jl-acoes` é um flex
// que NÃO quebra linha, e abaixo de 576px ele ganha `width: 100%` + `justify-content: flex-end`.
// Quando os botões não cabem, o excedente escorre pro lado OPOSTO ao alinhamento — pra
// ESQUERDA, pra fora do cartão e pra fora da tela. E some em silêncio: não vira barra de
// rolagem horizontal (o navegador não cria área de rolagem à esquerda), então nada denuncia.
//
// ⚠️ Medido no Chromium com o site.css real, a 390px de largura: a barra pede 365px e a área
// útil do cartão tem 291px. O primeiro botão nascia em x = -21,5px. Aos 320px, -91,5px.
//
// ⚠️ O QUE ESTOUROU FOI A CONTA DE BOTÕES, e por isso o defeito apareceu agora: em 10/09 as
// setas ↑↓ da ordem entraram na barra e ela foi de 5 pra 7 botões. Cada `.btn-sm` daqui tem
// 48px (o `.btn { padding: .5rem 1.1rem }` do site vence o `.btn-sm` do Bootstrap), então
// 7 botões + os vãos pedem 365px — mais do que qualquer celular oferece dentro do cartão.
//
// ⚠️ E não adianta espremer: 7 alvos de 44px (o alvo de dedo que o resto do site respeita)
// já passam de 337px. Numa linha só, nesta largura, é impossível — a barra tem que quebrar.
//
// CSS não quebra build nem teste de comportamento: sem esta trava, `flex-wrap` some numa
// limpeza e o vazamento volta em silêncio, visível só pra quem abrir a tela no celular.
public class AcoesDoJogoNoCelularTests
{
    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }

    private static string Css() =>
        File.ReadAllText(Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "css", "site.css"));

    // ⚠️ A REGRA COBRADA É A DE BASE, e o `display: flex` no mesmo bloco é o que garante isso:
    // o `.pdz-jl-acoes` que mora dentro do `@media (max-width: 575.98px)` não tem `display`, e
    // sem essa âncora o teste passaria com a quebra escrita só lá — deixando de pé o vazamento
    // da faixa de 576px a ~700px, onde a barra também não cabe (medido: 32px pra fora aos 430px).
    [Fact]
    public void A_barra_de_acoes_da_linha_de_jogo_quebra_linha()
    {
        Assert.Matches(
            new Regex(@"\.pdz-jl-acoes\s*\{[^}]*display:\s*flex[^}]*flex-wrap:\s*wrap", RegexOptions.Singleline),
            Css());
    }

    // As setas ↑↓ são um formulário só (dois submits, o que muda é o `direcao`), e ele entra na
    // barra como UM item de flex. Sem quebrar linha por dentro, ↑ e ↓ nunca se separam — o par
    // vai inteiro pra linha de baixo. Separá-los seria o organizador procurando a seta gêmea
    // no fim da fila.
    [Fact]
    public void As_setas_da_ordem_seguem_juntas_e_nao_se_separam()
    {
        var setas = Regex.Match(Css(), @"\.pdz-jl-setas\s*\{[^}]*\}", RegexOptions.Singleline);

        Assert.True(setas.Success, "A regra .pdz-jl-setas sumiu do site.css.");
        Assert.DoesNotMatch(new Regex(@"flex-wrap:\s*wrap"), setas.Value);
    }
}
