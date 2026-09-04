using Xunit;

namespace Padelizou.Tests;

// O PRAZO PRECISA APARECER NO CARD DA LISTA, NÃO SÓ NA PÁGINA DO TORNEIO.
//
// 🗣️ Felipe, 04/09/2026, olhando a LISTA: "acho que é bom colocar até quando vai as inscrições
// de cada torneio também". O campo já existia, já vinha preenchido e já era mostrado — só que
// na página de dentro, que é depois do clique. O card é onde a pessoa decide se clica.
//
// ⚠️ É TESTE DE FONTE, escolha consciente: não há suíte de Razor neste projeto. Ele prende a
// invariante — as DUAS telas leem a mesma frase do serviço —, não a aparência.
public class PrazoNoCardDoTorneioTests
{
    private static string Fonte(params string[] caminho)
    {
        var pasta = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && pasta != null; i++)
        {
            var tentativa = Path.Combine(new[] { pasta, "Padelizou" }.Concat(caminho).ToArray());
            if (File.Exists(tentativa)) return File.ReadAllText(tentativa);
            pasta = Directory.GetParent(pasta)?.FullName;
        }
        throw new FileNotFoundException($"{string.Join("/", caminho)} não encontrado a partir do bin.");
    }

    [Fact]
    public void O_card_da_lista_mostra_o_prazo()
    {
        var card = Fonte("Views", "Shared", "_TorneioCard.cshtml");

        Assert.Contains("PrazoDaInscricaoNaTela.Frase(Model", card);
    }

    [Fact]
    public void As_duas_telas_leem_a_MESMA_frase()
    {
        // O motivo de o serviço existir. Se a página do torneio voltar a formatar a data por
        // conta própria, as duas passam a dizer coisas diferentes sobre o mesmo torneio — foi
        // exatamente isso que fez o DataDoTorneioNaTela nascer.
        var detalhe = Fonte("Views", "Torneios", "Details.cshtml");

        Assert.Contains("PrazoDaInscricaoNaTela.Frase(Model", detalhe);
        Assert.DoesNotContain("Model.PrevisaoEncerramentoInscricoes.Value.ToString", detalhe);
    }

    [Fact]
    public void O_bloco_da_pagina_do_torneio_pergunta_pela_FRASE_e_nao_pelo_campo()
    {
        // Com `!= null` no campo, o torneio de prazo VENCIDO e sem data de chaveamento
        // renderizava um parágrafo vazio com gap: o serviço calava lá dentro, mas o bloco
        // externo já tinha aberto.
        var detalhe = Fonte("Views", "Torneios", "Details.cshtml");

        Assert.DoesNotContain(
            "@if (Model.PrevisaoEncerramentoInscricoes != null || Model.PrevisaoChaveamento != null)",
            detalhe);
    }
}
