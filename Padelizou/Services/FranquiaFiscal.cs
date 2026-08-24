using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O medidor da franquia: quanto do que está INCLUÍDO no plano Fiscal o clube já gastou no mês.
//
// ── O QUE É A FRANQUIA ───────────────────────────────────────────────────────────────────
// A mensalidade do plano Fiscal (R$ 199) já inclui uma cota mensal de documentos — hoje 150
// notas de serviço e 600 cupons (ver PlanoClubeSettings). Passou disso, cada documento a mais
// é cobrado à parte. É o modelo de todo provedor fiscal do mercado, e existe porque o custo
// do Padelizou é por documento: sem cota, um clube movimentado consumiria mais crédito do que
// paga de mensalidade, e o prejuízo só apareceria na fatura do provedor.
//
// ── AS QUATRO REGRAS, E POR QUE CADA UMA ─────────────────────────────────────────────────
// 1. MENSAL E NÃO ACUMULA, inclusive no plano anual. Cota que acumula vira crédito eterno e
//    destrói a previsibilidade do custo — que é justamente o que a cota existe pra dar.
// 2. BALDES SEPARADOS, SEM COMPENSAÇÃO. Sobrar cupom não paga nota de serviço. São custos
//    diferentes no provedor; misturar seria subsidiar um com o outro sem querer.
// 3. SÓ NOTA AUTORIZADA CONSOME. Rejeitada não consome franquia (mesmo tendo custado crédito
//    nosso — esse é um risco que fica do nosso lado, e é o que empurra a gente a validar o
//    cadastro ANTES de mandar, ver FiscalDoProduto).
// 4. CANCELAMENTO NÃO DEVOLVE. A nota foi emitida, o documento existiu, o custo aconteceu.
//
// ── A PERGUNTA QUE ESTE MEDIDOR RESPONDE HOJE É OUTRA ─────────────────────────────────────
// ⚠️ Enquanto o EmissorFiscalDesligado estiver no lugar, NADA é autorizado — e um medidor que
// contasse só o consumo marcaria zero pra sempre. Por isso ele devolve DOIS números por balde:
//
//     Consumidas → o que comeu a franquia. É o número do CONTRATO e da cobrança.
//     Volume     → quantos documentos a operação do clube gerou, tendo emitido ou não.
//                  É o número da MEDIÇÃO: é ele que diz se 150+600 é a cota certa.
//
// Com o emissor desligado eles são "0" e "N" — e isso não é defeito, é exatamente o serviço:
// medir o volume real de um clube piloto ANTES de assinar contrato com provedor e ANTES de
// fechar a franquia. A cota definitiva se confirma com três meses de dados (FISCAL.md), e é
// este contador que os produz.
public static class FranquiaFiscal
{
    // A partir de quanto da cota o clube merece ser avisado. 80% dá tempo de reagir dentro do
    // mesmo mês — avisar em 100% é avisar depois que a conta já subiu.
    public const int PercentualDeAlerta = 80;

    public enum Situacao
    {
        Tranquilo,
        Perto,      // passou do PercentualDeAlerta e ainda não estourou
        Estourou,   // consumiu mais que a franquia; o excedente já está sendo cobrado
    }

    // O mês de competência de uma nota é o mês do MOVIMENTO, não o da resposta da SEFAZ.
    //
    // ⚠️ Decisão, e ela tem consequência: a venda das 23h50 de 31/01 cuja nota só autoriza às
    // 00h05 de 01/02 conta em JANEIRO. Quem vendeu foi janeiro, e é a fatura de janeiro que o
    // clube confere contra o próprio caixa. Amarrar a franquia à hora em que a Receita
    // respondeu deixaria a cota do clube à mercê da fila da SEFAZ — e tornaria impossível
    // conciliar a fatura com o relatório do bar, que é por data de venda.
    public static DateTime MesDeCompetencia(NotaFiscal nota) => nota.CriadaEm;

    // Esta nota comeu franquia? Só a que virou documento de verdade — e a cancelada continua
    // contando, porque cancelar não desfaz o que já foi emitido.
    //
    // A versão por STATUS existe porque a contagem no banco agrupa por status e não tem a
    // entidade em mãos. São duas assinaturas da mesma regra, e não duas regras: a de baixo
    // chama a de cima. Repetir a lista de status nos dois lugares é como uma delas envelhece.
    public static bool ConsomeFranquia(string status) =>
        status is NotaFiscal.Autorizada or NotaFiscal.Cancelada;

    public static bool ConsomeFranquia(NotaFiscal nota) => ConsomeFranquia(nota.Status);

    // O primeiro instante do mês de uma data, e o primeiro do mês seguinte. Em um lugar só
    // porque o intervalo aberto no fim (`< fim`) é o detalhe que, feito à mão duas vezes,
    // acaba perdendo ou duplicando as notas do último segundo do mês.
    public static (DateTime Inicio, DateTime Fim) LimitesDoMes(DateTime referencia)
    {
        var inicio = new DateTime(referencia.Year, referencia.Month, 1, 0, 0, 0);
        return (inicio, inicio.AddMonths(1));
    }
}

// Um balde: uma cota, um tipo de documento, um mês. Nunca se soma com o outro (regra 2).
public sealed record BaldeDaFranquia(
    string Tipo,
    int Cota,
    int Consumidas,
    int Volume,
    decimal PrecoUnitarioDoExcedente)
{
    public int Excedente => Math.Max(0, Consumidas - Cota);

    public decimal ValorDoExcedente => Excedente * PrecoUnitarioDoExcedente;

    public int Restantes => Math.Max(0, Cota - Consumidas);

    // Quanto da cota já foi, PARA MOSTRAR. Passa de 100 quando estourou — de propósito: a tela
    // precisa conseguir dizer "180% da cota", e travar em 100 esconderia o tamanho do estouro.
    //
    // ⚠️ É número de EXIBIÇÃO e não serve pra decidir nada. Um teste pegou o motivo: 479 de 600
    // é 79,8%, que arredondado vira 80 — e a `Situacao` calculada em cima disto disparava o
    // aviso um documento antes da hora. Quem decide usa a fração crua, logo abaixo.
    public int PercentualUsado => Cota <= 0 ? 0 : (int)Math.Round(Consumidas * 100.0 / Cota);

    // O que a operação gerou e ainda não virou documento — fila, rejeição ou emissor
    // desligado. É a diferença entre o que o clube VENDEU e o que ele EMITIU.
    public int NaoEmitidas => Math.Max(0, Volume - Consumidas);

    public FranquiaFiscal.Situacao Situacao =>
        Excedente > 0 ? FranquiaFiscal.Situacao.Estourou
        : Cota > 0 && Consumidas * 100m >= Cota * FranquiaFiscal.PercentualDeAlerta
            ? FranquiaFiscal.Situacao.Perto
        : FranquiaFiscal.Situacao.Tranquilo;

    public string Rotulo => Tipo == NotaFiscal.Nfse ? "Notas de serviço" : "Cupons do bar";
}

// A medida do mês inteiro: os dois baldes e a conta do excedente.
public sealed record MedidaDaFranquia(DateTime Mes, BaldeDaFranquia Servico, BaldeDaFranquia Cupom)
{
    // Aqui os dois SE SOMAM — e só aqui. A regra de não compensar é sobre a cota (sobra de um
    // não cobre falta do outro); o dinheiro a pagar é um só, e vem numa fatura só.
    public decimal ValorDoExcedente => Servico.ValorDoExcedente + Cupom.ValorDoExcedente;

    public bool TemExcedente => ValorDoExcedente > 0;

    // Quantos documentos a operação gerou no mês, tendo emitido ou não. É o número que o
    // piloto existe pra descobrir.
    public int VolumeTotal => Servico.Volume + Cupom.Volume;

    // A pior das duas manda: um balde estourado não fica escondido atrás do outro que sobrou.
    // Escrito por extenso em vez de comparar os enums como número — a ordem do enum é detalhe
    // de declaração, e amarrar a regra a ela quebra em silêncio no dia em que alguém inserir
    // um estado no meio.
    public FranquiaFiscal.Situacao Situacao =>
        Servico.Situacao == FranquiaFiscal.Situacao.Estourou
            || Cupom.Situacao == FranquiaFiscal.Situacao.Estourou ? FranquiaFiscal.Situacao.Estourou
        : Servico.Situacao == FranquiaFiscal.Situacao.Perto
            || Cupom.Situacao == FranquiaFiscal.Situacao.Perto ? FranquiaFiscal.Situacao.Perto
        : FranquiaFiscal.Situacao.Tranquilo;

    public string Recado => Situacao switch
    {
        FranquiaFiscal.Situacao.Estourou =>
            $"A franquia do mês foi ultrapassada — excedente de {ValorDoExcedente:C} até agora.",
        FranquiaFiscal.Situacao.Perto =>
            "A franquia do mês está perto do fim. Passando dela, cada documento a mais é cobrado à parte.",
        _ => "Dentro da franquia do mês.",
    };
}

// Quem vai ao banco contar. As regras ficam no FranquiaFiscal, puras; aqui só a consulta.
public class MedidorDeFranquia
{
    private readonly DbPadelContext _context;
    private readonly PlanoClubeSettings _cfg;

    public MedidorDeFranquia(DbPadelContext context,
        Microsoft.Extensions.Options.IOptions<PlanoClubeSettings> cfg)
    {
        _context = context;
        _cfg = cfg.Value;
    }

    public async Task<MedidaDaFranquia> DoMesAsync(int clubeId, DateTime referencia)
    {
        var (inicio, fim) = FranquiaFiscal.LimitesDoMes(referencia);

        // Uma consulta só, agrupada — e não duas (uma por tipo) nem seis (uma por status):
        // num clube de bar movimentado a tabela cresce por comanda fechada, e varrer isso
        // várias vezes por carregamento de tela é o tipo de coisa que só aparece no clube
        // grande, que é justamente o cliente que a gente não pode fazer esperar.
        var linhas = await _context.NotasFiscais
            .Where(n => n.ClubeId == clubeId && n.CriadaEm >= inicio && n.CriadaEm < fim)
            .GroupBy(n => new { n.Tipo, n.Status })
            .Select(g => new { g.Key.Tipo, g.Key.Status, Quantas = g.Count() })
            .ToListAsync();

        BaldeDaFranquia Balde(string tipo, int cota, decimal preco)
        {
            var doTipo = linhas.Where(l => l.Tipo == tipo).ToList();

            var volume = doTipo.Sum(l => l.Quantas);
            var consumidas = doTipo
                .Where(l => FranquiaFiscal.ConsomeFranquia(l.Status))
                .Sum(l => l.Quantas);

            return new BaldeDaFranquia(tipo, cota, consumidas, volume, preco);
        }

        return new MedidaDaFranquia(
            inicio,
            Balde(NotaFiscal.Nfse, _cfg.FranquiaNfseMensal, _cfg.ExcedenteNfse),
            Balde(NotaFiscal.Nfce, _cfg.FranquiaNfceMensal, _cfg.ExcedenteNfce));
    }
}
