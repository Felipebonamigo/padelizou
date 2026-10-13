using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O QUE ACONTECE COM O DINHEIRO QUANDO DUAS INSCRIÇÕES VIRAM UMA.
//
// Juntar apaga a inscrição sozinha e mantém a outra (ver Services/InscricaoRepetida). A linha
// some — mas o dinheiro que entrou por ela não pode sumir junto.
//
// 🗣️ Felipe, 19/08/2026: *"se ele já pagou, tem q se manter como pago"*. Sem isto, quem tinha
// pago a própria inscrição sozinha aparecia como DEVENDO depois de fechar dupla: entrava na
// lista de inadimplentes do organizador, levava lembrete de pagamento (ver
// Services/LembreteDeInscricaoNaoPaga) e seria cobrado de novo na quadra por um dinheiro que
// já estava na conta. O erro oposto — deixar de cobrar quem não pagou — custa uma conversa; o
// deste lado custa a confiança de quem pagou certo.
//
// ⚠️ O `Pago` é da INSCRIÇÃO, não de cada pessoa: o banco não guarda "quem dos dois pagou".
// Então quando UMA das duas estava paga, a inscrição que fica nasce paga e o que faltar é
// acerto com o organizador — as telas dizem isso a quem junta.
public static class JuntarInscricoes
{
    // A inscrição que fica herda o pagamento da que sai. Pura de propósito: quem chama grava
    // no mesmo SaveChanges que apaga a linha, e a regra fica testável sem banco.
    //
    // ⚠️ NUNCA REBAIXA: inscrição que já estava paga não volta a dever porque a outra não
    // estava. Só sobe.
    public static void LevarOPagamento(Dupla queFica, IEnumerable<Dupla> queSaem)
    {
        if (queFica.Pago) return;

        // A mais antiga entre as pagas: a data que vale é a do dinheiro que entrou primeiro,
        // não a do desempate. `PagoEm` nulo (inscrição marcada à mão antes da coluna) vai pro
        // fim da fila, mas continua servindo — o que importa é o `Pago`.
        var jaPaga = queSaem
            .Where(d => d.Pago)
            .OrderBy(d => d.PagoEm ?? DateTime.MaxValue)
            .FirstOrDefault();

        if (jaPaga == null) return;

        queFica.Pago = true;
        // Sem inventar data: se a que saiu não tinha uma, a que fica também não tem. Carimbar
        // "agora" diria que o dinheiro entrou hoje, e não entrou.
        queFica.PagoEm = jaPaga.PagoEm;
    }

    // O IMPEDIMENTO DA QUE SAI ENTRA NA QUE FICA — UM POR DUPLA.
    //
    // 🗣️ Felipe, 13/10/2026: *"deixe para a pessoa q colocou impedimento, salvo lá, o
    // impedimento q ela colocou"*, e *"lembrando que o impedimento é um por dupla"*. Até aqui o
    // turno sumia junto com a linha apagada: a pessoa marcava "Sábado de manhã", fechava dupla
    // e a dupla aparecia sem impedimento — o "não está ficando salvo" do Andrey no NATA.
    //
    // A régua (opção "a", escolhida por ele):
    //  - a que fica SEM impedimento herda o da que sai, com a autoria e com a taxa no valor —
    //    o mesmo número que a dupla teria se tivesse nascido com ele (é o `Aplicar` de sempre);
    //  - a que fica JÁ COM impedimento mantém o seu, e o dono da que saiu volta na lista de
    //    `FicaramDeFora` pra ser avisado — perder calado é o defeito que se está corrigindo;
    //  - turno que o torneio não tem (ver TurnosDoTorneio) não é herdado: seria cobrar taxa por
    //    uma janela que o sorteio ignora.
    //
    // Pura, como o LevarOPagamento: quem chama grava no mesmo SaveChanges e avisa depois.
    // ⚠️ Quem chama precisa rodar ISTO ANTES de recontar o valor da dupla (AoEntrarOParceiro) e
    // de perguntar se ela continua quitada: a taxa herdada faz parte do que ela deve.
    public sealed record ImpedimentoQueFicouDeFora(int JogadorId, TurnoDoImpedimento Turno);

    public sealed record ImpedimentoAoJuntar(bool Veio, IReadOnlyList<ImpedimentoQueFicouDeFora> FicaramDeFora);

    public static ImpedimentoAoJuntar LevarOImpedimento(Dupla queFica, IEnumerable<Dupla> queSaem, Torneio torneio)
    {
        var veio = false;
        var deFora = new List<ImpedimentoQueFicouDeFora>();

        // A mais antiga primeiro: entre duas sozinhas com turnos diferentes (raro, mas a junção
        // aceita mais de uma), quem chegou antes leva.
        foreach (var sai in queSaem.OrderBy(d => d.Id))
        {
            var turno = AlteracaoDeImpedimento.TurnoAtual(sai);
            if (turno == TurnoDoImpedimento.Nenhum || !TurnosDoTorneio.Tem(torneio, turno)) continue;

            var atual = AlteracaoDeImpedimento.TurnoAtual(queFica);
            if (atual == turno) continue;

            if (atual != TurnoDoImpedimento.Nenhum)
            {
                deFora.Add(new ImpedimentoQueFicouDeFora(sai.Jogador1Id, turno));
                continue;
            }

            // O `Aplicar` marca o turno e soma a taxa (nulo continua nulo); a autoria que ele
            // carimba é sobrescrita logo abaixo pela da inscrição de onde o turno veio.
            AlteracaoDeImpedimento.Aplicar(queFica, torneio, turno, 0, default);
            queFica.ImpedimentoAlteradoPorId = sai.ImpedimentoAlteradoPorId;
            queFica.ImpedimentoAlteradoEm = sai.ImpedimentoAlteradoEm;
            veio = true;
        }

        return new ImpedimentoAoJuntar(veio, deFora);
    }

    public const string TituloDoImpedimentoQueNaoEntrou = "Seu impedimento não entrou na dupla";

    public static string CorpoDoImpedimentoQueNaoEntrou(
        string torneio, TurnoDoImpedimento seu, TurnoDoImpedimento daDupla) =>
        $"{torneio}: cada dupla tem um impedimento só, e a sua ficou com "
        + $"\"{AlteracaoDeImpedimento.Rotulo(daDupla)}\". O seu \"{AlteracaoDeImpedimento.Rotulo(seu)}\" "
        + "não entrou — se precisar dele, combine com seu parceiro e troque na tela do torneio "
        + "(ou fale com o organizador).";

    // AS COBRANÇAS DE "PAGAR DEPOIS" PASSAM A APONTAR PRA INSCRIÇÃO QUE FICOU.
    //
    // Este tipo de cobrança guarda o alvo no JSON (`DadosPagamentoDeInscricao`) e é o único
    // cujo efeito é reversível dos dois lados: confirmar marca "pago", estornar volta pra "não
    // pago" (ver PagamentoInscricaoService). Apontando pra uma linha apagada, a confirmação
    // que chegasse depois cairia no log de erro — dinheiro na conta e ninguém marcado como
    // pago.
    //
    // ⚠️ O tipo "TorneioDupla" fica de fora DE PROPÓSITO, e não é esquecimento: lá o estorno
    // APAGA a inscrição apontada. Repontá-lo faria o estorno pedido por um derrubar do torneio
    // a dupla inteira — inclusive a outra pessoa, que não pediu nada e pode ter pago. Esses
    // seguem como hoje: o estorno automático não acha a linha e registra no log que a
    // devolução é à mão (ver ESTORNO.md).
    public static async Task ReapontarCobrancasDoPagarDepoisAsync(
        DbPadelContext context, int torneioId, IReadOnlyList<int> idsQueSairam, int idQueFicou)
    {
        if (idsQueSairam.Count == 0) return;

        // Filtra pelo torneio no banco (o JSON não é consultável em SQL) e faz o resto em
        // memória — são poucas linhas, a mesma abordagem do CobrancaPendenteDaInscricaoAsync.
        var candidatas = await context.Pagamentos
            .Where(p => p.Tipo == "TorneioPagarDepois" && p.TorneioId == torneioId)
            .ToListAsync();

        foreach (var pagamento in candidatas)
        {
            if (pagamento.ReferenciaId is int referencia && idsQueSairam.Contains(referencia))
                pagamento.ReferenciaId = idQueFicou;

            if (string.IsNullOrWhiteSpace(pagamento.DadosInscricao)) continue;

            DadosPagamentoDeInscricao? dados;
            try
            {
                dados = JsonSerializer.Deserialize<DadosPagamentoDeInscricao>(pagamento.DadosInscricao);
            }
            catch (JsonException)
            {
                // JSON quebrado não é problema desta rotina: quem lê pra valer já trata e
                // registra. Aqui, deixar como está é o que não piora nada.
                continue;
            }

            if (dados?.DuplaId is not int duplaId || !idsQueSairam.Contains(duplaId)) continue;

            pagamento.DadosInscricao = JsonSerializer.Serialize(dados with { DuplaId = idQueFicou });
        }
    }
}
