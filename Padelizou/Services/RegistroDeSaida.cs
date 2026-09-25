using Padelizou.Models;

namespace Padelizou.Services;

// COMO NASCE UMA LINHA DO HISTÓRICO DE SAÍDAS. Puro, sem EF: quem grava é o controller.
//
// ⚠️ EXISTE PORQUE SÃO QUATRO PORTAS DE SAÍDA — `Desistir`, `DesistirDoAmericano`,
// `RemoverDupla` e `TirarDuplaDoTorneioAsync`. Montar a linha em cada uma delas é como uma das
// cópias acaba esquecendo o motivo, ou marcando vaga aberta onde não abriu; é a mesma razão de
// `DesistenciaDeInscricao` existir pra decidir o efeito, e de a Mesa de Controle ter quebrado em
// 31/07 por causa de três cópias da mesma régua.
public static class RegistroDeSaida
{
    public static SaidaDoTorneio Montar(
        int torneioId, int categoriaId, int jogador1Id, int? jogador2Id, int? quemPediuId,
        MotivoDaSaida motivo, string? observacao, bool estavaPaga, bool abriuVaga, DateTime agora) =>
        new()
        {
            TorneioId = torneioId,
            CategoriaId = categoriaId,
            Jogador1Id = jogador1Id,
            Jogador2Id = jogador2Id,
            QuemPediuId = quemPediuId,
            Motivo = motivo,
            // ⚠️ Vazio vira NULO, e não string vazia. O campo é opcional, então o caminho comum
            // é vir em branco — gravar "" obrigaria cada tela a distinguir "não disse nada" de
            // "disse nada", e duas formas do mesmo estado é como nasce o `if` que uma delas
            // esquece.
            Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
            EstavaPaga = estavaPaga,
            AbriuVaga = abriuVaga,
            SaiuEm = agora,
        };

    // O título e a frase do aviso ao organizador. ⚠️ Ficam aqui, e não no controller, porque a
    // MESMA saída é anunciada de quatro lugares — e porque o texto muda com o dinheiro: quando
    // a inscrição estava paga há estorno a decidir, e isso não pode sumir da frase.
    public static (string Titulo, string Frase) AvisoAoOrganizador(
        SaidaDoTorneio saida, string quemSaiu, string nomeDoTorneio)
    {
        var titulo = saida.EstavaPaga ? "Saiu do torneio uma inscrição PAGA" : "Abriu uma vaga no torneio";

        var abertura = saida.Motivo == MotivoDaSaida.NaoPagou
            ? $"A inscrição de {quemSaiu} em {nomeDoTorneio} saiu por não ter sido paga no prazo."
            : $"{quemSaiu} cancelou a inscrição em {nomeDoTorneio}.";

        var dinheiro = saida.EstavaPaga
            ? " Ela estava paga: o estorno não é automático, e se for o caso de devolver, faça em Pagamentos → Meus."
            : " A vaga voltou pra fila.";

        var porque = saida.Observacao is { } motivo ? $" Motivo que a pessoa deu: \"{motivo}\"." : "";

        return (titulo, abertura + dinheiro + porque);
    }
}
