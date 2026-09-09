using Padelizou.Models;

namespace Padelizou.Services;

public static class CobrancaDaDupla
{
    // A cobrança ATIVA de uma dupla — a que ainda dá pra estornar (mesmos status que
    // PagamentosController.Estornar aceita). O vínculo por ReferenciaId só existe depois que o
    // pagamento CONFIRMA: PagamentoInscricaoService.EfetivarTorneioAsync (dupla nasceu do
    // pagamento) e EfetivarPagamentoDeInscricaoAsync ("pagar depois") gravam
    // `pagamento.ReferenciaId = dupla.Id` nesse momento — antes disso a dupla não estaria
    // visível pra ninguém cancelar.
    //
    // Extraído do controller (TorneiosController.CancelarSemParceiro) pra virar consulta
    // testável: `Where` com "is X or Y" não traduz pra SQL (é pattern-matching de árvore de
    // expressão), e o InMemory dos testes não teria acusado — só o Postgres real recusaria,
    // na primeira visita em produção (ver a régua em CLAUDE.md sobre EF InMemory não validar
    // SQL, e TraducaoDasConsultasDePalpiteTests.cs pro mesmo padrão de verificação).
    public static IQueryable<Pagamento> AtivaDe(DbPadelContext ctx, int duplaId) =>
        ctx.Pagamentos.Where(p => p.ReferenciaId == duplaId
            && (p.Tipo == "TorneioDupla" || p.Tipo == "TorneioPagarDepois")
            && (p.Status == "Confirmado" || p.Status == "Pendente" || p.Status == "AguardandoEstorno"));
}
