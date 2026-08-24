using Padelizou.Models;

namespace Padelizou.Services;

// Os números dos planos de clube. Em configuração porque são preço de tabela — renegociar com
// um clube não pode exigir republicar o site (mesma razão do plano do professor).
public class PlanoClubeSettings
{
    public decimal MensalidadeGestao { get; set; } = 99m;
    public decimal MensalidadeFiscal { get; set; } = 199m;

    // Doze meses por dez. O desconto é o mesmo dos dois planos de propósito: duas réguas de
    // desconto viram duas conversas de negociação, e o clube compara com o vizinho.
    public decimal AnuidadeGestao { get; set; } = 990m;
    public decimal AnuidadeFiscal { get; set; } = 1990m;

    // ── A franquia do plano Fiscal ────────────────────────────────────────────────────────
    // Quantos documentos por mês estão INCLUÍDOS na mensalidade, e quanto custa o que passar.
    //
    // Estes quatro números estavam escritos à mão dentro da tela do plano — contra a regra do
    // topo deste arquivo, e não por descuido de estilo: a franquia é justamente o número que
    // vai mudar. Ela foi calibrada por comparação com o concorrente (150+600 é 25% mais que os
    // 100+500 do Gripo, ver FISCAL.md "O VEREDITO") e só se confirma com três meses de dados
    // de um clube real. Número que nasce pra mudar não pode exigir republicar o site.
    //
    // ⚠️ Os dois baldes são SEPARADOS e não se compensam: sobrar cupom não paga nota de
    // serviço. E a franquia é MENSAL e NÃO ACUMULA, inclusive no plano anual.
    public int FranquiaNfseMensal { get; set; } = 150;
    public int FranquiaNfceMensal { get; set; } = 600;

    public decimal ExcedenteNfse { get; set; } = 0.30m;
    public decimal ExcedenteNfce { get; set; } = 0.15m;

    public int DiasDeTeste { get; set; } = 15;

    // Dias de atraso que ainda seguram o acesso. Cortar o bar do clube no primeiro minuto de
    // atraso é fechar o balcão na cara do cliente dele por causa de um boleto de feriado — e
    // o estrago (venda que não é registrada) não se desfaz pagando depois.
    public int DiasDeCarencia { get; set; } = 7;
}

// O plano do clube: o que ele assinou, até quando está pago, e o que isso libera.
//
// ── Os três degraus ───────────────────────────────────────────────────────────────────────
//   REDE (sem plano, grátis)  → torneio, ranking, rede de jogadores, PUBLICAR quadras e
//                               horários, e o jogador reservando por eles.
//   GESTÃO                    → + ADMINISTRAR a agenda (mensalista, balcão, no-show, política
//                               de cancelamento, financeiro por quadra) + bar, comanda,
//                               estoque, caixa e contas do clube.
//   FISCAL                    → + emissão de nota (NFS-e e NFC-e).
//
// ⚠️ A REDE CONTINUA GRÁTIS, E ISSO NÃO É GENEROSIDADE — é o motor de aquisição. O clube chega
// pelo torneio que o organizador criou e pelos jogadores que já estão aqui; cobrar na porta
// mataria justamente o que traz o cliente pra dentro. O que se vende é a retaguarda: o que o
// clube já faz no caderno e na planilha.
//
// ⚠️ A RÉGUA DO CORTE, EM UMA FRASE: **o que o JOGADOR toca é grátis; o que o CLUBE
// administra é o plano.** O clube publica a agenda, aparece na vitrine e recebe reservas sem
// pagar nada — isso é o funil. Administrar aquela agenda é a retaguarda, e retaguarda é o que
// se vende. A reserva gravada fica do lado grátis por necessidade técnica, não por bondade: o
// HorarioMarcacaoService calcula "vago" como a grade MENOS as reservas confirmadas, então sem
// ela a vitrine não encolhe — ela passa a mentir.
//
// ⚠️ DOIS EIXOS QUE **NÃO** VIRARAM PLANO, e ficam registrados pra não serem redecididos:
//
// 1. PUBLICAR HORÁRIO continua grátis. Cobrar por publicar é cobrar na entrada do funil, e a
//    vitrine cheia de jogadores é o único ativo que o concorrente não copia comprando módulo.
//
// 2. TRAZER TORNEIO não vira plano, nem desconto, nem crédito — porque JÁ É COBRADO, e caro.
//    O `Torneio.ModoComissao` é fixo em "Descontada" (ver Models/Torneio.cs) e nenhum
//    controller escreve nele: a taxa sai de dentro do preço anunciado, ou seja **quem paga é o
//    organizador**. Um torneio de 60 duplas a R$ 150 rende de R$ 450 a R$ 1.350 conforme a
//    forma de pagamento — 4,5 a 13,6 meses de Gestão, num evento só. Somar plano em cima disso
//    seria cobrar duas vezes pela mesma coisa. E há um segundo motivo, independente do
//    primeiro: **o organizador frequentemente não é o clube**, então "torneio no sistema" como
//    degrau cobraria do clube uma receita que vem de terceiro. Quem quer pagar menos já tem a
//    válvula honesta: escolher "Externo" (5%), em que não tocamos no dinheiro.
//
// ⚠️ FISCAL INCLUI GESTÃO, sempre. Não existe emitir nota de uma venda que o sistema não
// registra — vender os dois separados criaria um estado impossível de sustentar.
//
// A regra mora aqui, pura, porque a resposta é consumida em quatro lugares (o gate do bar, o
// gate do fiscal, a tela do plano e a cobrança) e os quatro TÊM que concordar.
public static class PlanoDoClube
{
    public const string Gestao = "Gestao";
    public const string Fiscal = "Fiscal";

    public static readonly string[] Planos = { Gestao, Fiscal };

    public static bool PlanoValido(string? plano) => plano != null && Planos.Contains(plano);

    // Plano EXISTE (PlanoValido) é diferente de plano ESTÁ À VENDA HOJE, e confundir os dois
    // já custou caro numa demonstração: com `Fiscal__Habilitado=false` a tela de plano seguia
    // oferecendo o Clube Fiscal com botão funcionando, o clube escolhia, gerava cobrança de
    // R$ 199 — e depois batia num "essa parte não é sua", porque o módulo não existe ainda.
    //
    // É o mesmo princípio que o ModuloFiscal já defende do outro lado do balcão (lá: em
    // construção nunca vira convite pra assinar). Aqui é a metade que faltava: enquanto a
    // emissão não está de pé, o Fiscal não se escolhe nem se cobra. Cobrar por algo que não
    // funciona não é otimismo comercial, é dinheiro que teremos que devolver.
    //
    // Os outros planos não dependem de interruptor nenhum: o Gestão é o bar, que já roda.
    public static bool EstaAVenda(string? plano, bool fiscalHabilitado) =>
        PlanoValido(plano) && (plano != Fiscal || fiscalHabilitado);

    public static string RotuloDoPlano(string? plano) => plano switch
    {
        Gestao => "Clube Gestão",
        Fiscal => "Clube Fiscal",
        _ => "Clube Rede"
    };

    public static decimal ValorDo(string? plano, string? ciclo, PlanoClubeSettings cfg)
    {
        var anual = CicloDeAssinatura.Valido(ciclo) == CicloDeAssinatura.Anual;

        return plano == Fiscal
            ? (anual ? cfg.AnuidadeFiscal : cfg.MensalidadeFiscal)
            : (anual ? cfg.AnuidadeGestao : cfg.MensalidadeGestao);
    }

    public static decimal EconomiaDoAnual(string? plano, PlanoClubeSettings cfg) =>
        CicloDeAssinatura.EconomiaDoAnual(
            ValorDo(plano, CicloDeAssinatura.Mensal, cfg),
            ValorDo(plano, CicloDeAssinatura.Anual, cfg));

    public enum Situacao
    {
        SemPlano,    // clube Rede: usa o que é grátis e nunca pediu mais
        EmTeste,     // 15 dias com o plano escolhido valendo, sem ter pago nada
        EmDia,       // assinatura quitada (ou dentro da carência)
        EmAtraso     // escolheu o plano e não pagou: perdeu o acesso até quitar
    }

    // O relógio do teste começa quando alguém do clube ABRE a tela do plano — não na criação
    // do clube. Mesma razão do professor: senão o teste corre antes de a pessoa saber que
    // existe, e ela descobre o produto no dia em que ele já acabou.
    public static bool EmTeste(Clube clube, DateTime agora, PlanoClubeSettings cfg) =>
        clube.TesteDoClubeInicio != null
        && agora <= clube.TesteDoClubeInicio.Value.AddDays(cfg.DiasDeTeste);

    public static DateTime? FimDoTeste(Clube clube, PlanoClubeSettings cfg) =>
        clube.TesteDoClubeInicio?.AddDays(cfg.DiasDeTeste);

    public static Situacao SituacaoDe(Clube clube, DateTime agora, PlanoClubeSettings cfg)
    {
        if (!PlanoValido(clube.PlanoDoClube)) return Situacao.SemPlano;

        if (clube.AssinaturaClubePagaAte != null
            && agora <= clube.AssinaturaClubePagaAte.Value.AddDays(cfg.DiasDeCarencia))
            return Situacao.EmDia;

        // Escolheu o plano durante o teste e a primeira cobrança ainda não venceu: o teste
        // segura o acesso até o fim dos 15 dias.
        if (EmTeste(clube, agora, cfg)) return Situacao.EmTeste;

        return Situacao.EmAtraso;
    }

    // ── O que cada plano libera ───────────────────────────────────────────────────────────
    //
    // ⚠️ Estas duas funções são a diferença entre um plano e uma promessa. Elas são chamadas
    // pelo ModuloDoBar e pelo ModuloFiscal, e é por elas que o clube que parou de pagar para
    // de entrar. Cuidado ao mexer: afrouxar aqui é entregar o produto de graça e ninguém
    // percebe; apertar demais é fechar o balcão de um cliente em dia no meio do sábado.

    public static bool LiberaGestao(Clube clube, DateTime agora, PlanoClubeSettings cfg) =>
        SituacaoDe(clube, agora, cfg) is Situacao.EmTeste or Situacao.EmDia;

    // Fiscal exige o plano Fiscal E estar em dia. O clube da Gestão que abrir a aba fiscal vê
    // o convite pra subir de plano, não um erro.
    public static bool LiberaFiscal(Clube clube, DateTime agora, PlanoClubeSettings cfg) =>
        clube.PlanoDoClube == Fiscal && LiberaGestao(clube, agora, cfg);

    public static DateTime NovaDataPagaAte(DateTime? pagaAteAtual, DateTime agora, string? ciclo = null) =>
        CicloDeAssinatura.NovaDataPagaAte(pagaAteAtual, agora, ciclo);

    // A frase da tela, por situação. Fica aqui junto da regra pra tela e gate nunca contarem
    // histórias diferentes sobre o mesmo clube — "seu plano está ativo" numa tela e porta
    // fechada na outra é o pior jeito de perder um cliente.
    public static string Recado(Clube clube, DateTime agora, PlanoClubeSettings cfg) =>
        SituacaoDe(clube, agora, cfg) switch
        {
            Situacao.EmTeste =>
                $"Teste até {FimDoTeste(clube, cfg):dd/MM} — depois disso é preciso assinar pra continuar.",
            Situacao.EmDia =>
                $"{RotuloDoPlano(clube.PlanoDoClube)} em dia até {clube.AssinaturaClubePagaAte:dd/MM/yyyy}.",
            Situacao.EmAtraso =>
                "A assinatura venceu — gere a cobrança pra reabrir o bar e as contas do clube.",
            // ⚠️ Não diz "reservas" solto: o clube RECEBE reserva de graça, mas administrar a
            // agenda (mensalista, balcão, no-show) é o Gestão. A frase antiga prometia o que
            // hoje está atrás do plano.
            _ => "Seu clube usa o plano gratuito: torneios, ranking e a agenda publicada."
        };
}
