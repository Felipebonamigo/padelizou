using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;

namespace Padelizou.Services;

// O RECORTE DA TELA SOBREVIVE AO CLIQUE (12/09/2026).
//
// 🗣️ Felipe, com o check-in por jogador no ar: *"Ao marcar de confirmar na tela, ele sai da tela,
// ele tem q sempre se manter na tela da alteracao"*.
//
// 🕳️ Toda ação do organizador é POST → redirect → GET, e o redirect voltava pra `Details` NU. Com
// a lista filtrada numa categoria (o normal num torneio de 12), marcar uma chegada devolvia a
// grade INTEIRA por cima do recorte. Reproduzido no navegador: "Agendadas (2)" virava
// "Agendadas (3)" no clique. E a rolagem restaurada no mesmo pixel piora — mesmo lugar da tela,
// outra lista embaixo.
//
// ⚠️ LISTA FECHADA DE CHAVES, e é o ponto inteiro deste arquivo: o `filtros` chega por CAMPO DE
// FORMULÁRIO, que é editável. Repassar a query inteira pro `RedirectToAction` deixaria o cliente
// montar a rota — trocar o `id` do torneio, apontar `controller`/`action`/`area` pra outro lugar.
// Aqui só passam os SEIS filtros que `Details`/`Jogos` já recebem por parâmetro; o resto é jogado
// fora sem cerimônia. É a mesma régua do `voltarPara` (TorneiosController.MarcarCheckIn).
public static class FiltrosDaListaDeJogos
{
    // Exatamente os parâmetros de `TorneiosController.Details`/`Jogos`, menos o `id` — que é do
    // torneio e nunca vem do formulário.
    private static readonly string[] Aceitas =
    [
        "timeFiltroId",
        "categoriaFiltroIds",
        "soMeusJogos",
        "clubeFiltroId",
        "quadraFiltro",
        "faseFiltro",
    ];

    // Teto de tamanho: campo escondido é campo que dá pra editar, e uma query de 200 KB viraria
    // uma URL de redirect que nenhum navegador aceita — o organizador levaria um erro no lugar
    // da lista. O maior filtro real (12 categorias marcadas) não passa de ~300 caracteres.
    private const int TetoDeCaracteres = 1000;

    // O que a TELA manda no formulário: a query de agora, já peneirada. Mandar o resto seria
    // carregar peso que o servidor vai descartar de qualquer jeito.
    public static string Atual(HttpRequest requisicao)
    {
        var pedacos = requisicao.Query
            .Where(par => Aceitas.Contains(par.Key, StringComparer.Ordinal))
            .SelectMany(par => par.Value.Where(v => !string.IsNullOrEmpty(v))
                .Select(v => $"{par.Key}={Uri.EscapeDataString(v!)}"))
            .ToList();

        return pedacos.Count == 0 ? "" : "?" + string.Join("&", pedacos);
    }

    // O que o SERVIDOR reaproveita: as chaves aceitas viram valores de rota pro
    // `RedirectToAction`. Chave repetida (o filtro de categoria é múltipla escolha) volta como
    // array — voltar só com a primeira seria devolver outro recorte, não o dele.
    public static RouteValueDictionary Reaproveitar(string? filtros)
    {
        var rota = new RouteValueDictionary();
        if (string.IsNullOrWhiteSpace(filtros) || filtros.Length > TetoDeCaracteres) return rota;

        var consulta = QueryHelpers.ParseQuery(filtros.StartsWith('?') ? filtros : "?" + filtros);
        foreach (var chave in Aceitas)
        {
            if (!consulta.TryGetValue(chave, out var valores)) continue;

            var limpos = valores.Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToArray();
            if (limpos.Length == 0) continue;

            rota[chave] = limpos.Length == 1 ? limpos[0] : limpos;
        }
        return rota;
    }
}
