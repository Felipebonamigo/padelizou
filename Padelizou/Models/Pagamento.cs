using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// Uma cobrança gerada no Asaas para liberar uma inscrição paga. Fica numa tabela só (em vez
// de espalhar campos por Dupla/InscricaoAmericana/InscricaoJogoAula) pra centralizar o
// histórico e facilitar o relatório de repasses e comissões.
[Table("Pagamento")]
// O webhook procura a cobrança por esse id a cada notificação do Asaas.
[Index(nameof(AsaasPaymentId))]
public class Pagamento
{
    public int Id { get; set; }

    // "TorneioDupla" | "TorneioAmericano" | "Aula" — diz qual inscrição este pagamento libera.
    public string Tipo { get; set; } = null!;

    // Id da inscrição, preenchido só depois que o pagamento confirma e ela é criada.
    public int? ReferenciaId { get; set; }

    // JSON com o que é preciso pra montar a inscrição quando o pagamento confirmar. A
    // inscrição não é criada antes de pagar: assim nenhuma consulta existente precisa saber
    // separar inscrição "de verdade" de inscrição fantasma esperando pagamento.
    public string? DadosInscricao { get; set; }

    // Torneio ou aula a que este pagamento se refere — usado nos relatórios de repasse.
    public int? TorneioId { get; set; }
    public int? JogoAulaId { get; set; }

    // Quem paga.
    public int JogadorId { get; set; }
    public virtual Jogador Jogador { get; set; } = null!;

    // Quem recebe o split (dono do torneio ou professor da aula). Sem isso não dá pra montar
    // o extrato de repasses de cada organizador.
    public int? RecebedorId { get; set; }

    // Total pago pelo jogador — o que VALE hoje, não o que foi cobrado um dia. Um estorno
    // parcial abaixa os três números de baixo (ver Services/EstornoParcial), de propósito:
    // assim todo relatório que já somava Valor/Repasse/Comissao continua certo sem precisar
    // saber que estorno existe. Quanto voltou fica em ValorEstornado.
    public decimal Valor { get; set; }

    // Fatia que o split manda direto pro organizador/professor.
    public decimal ValorRepasse { get; set; }

    // Fatia do Padelizou, bruta — a taxa do Asaas ainda é descontada dela.
    public decimal Comissao { get; set; }

    // Quanto já voltou pro jogador. Zero na esmagadora maioria das cobranças; > 0 quando
    // devolvemos parte do dinheiro (cobrança a maior, desconto combinado depois do pagamento).
    // Estorno TOTAL não usa este campo — lá o status vira "Estornado" e a inscrição é desfeita.
    public decimal ValorEstornado { get; set; }

    public string? AsaasPaymentId { get; set; }
    public string? AsaasCustomerId { get; set; }
    public string? InvoiceUrl { get; set; }

    // "Pendente" | "Confirmado" | "Cancelado" | "Estornado" | "AguardandoConfirmacao"
    //
    // O último só existe no Pix direto: o pagador disse que pagou e o admin ainda não conferiu
    // o extrato. É pendente pra todos os efeitos — só não é "esquecido" (ver Services/PixDireto).
    public string Status { get; set; } = "Pendente";

    // "Gateway" (o padrão de sempre) | "PixDireto".
    //
    // Só vale "PixDireto" no que é 100% receita do Padelizou — mensalidade e taxa do externo.
    // Quem tem repasse a fazer PRECISA do split do gateway; a trava está em PixDireto.AceitaPix.
    public string MetodoPagamento { get; set; } = "Gateway";

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public DateTime? ConfirmadoEm { get; set; }

    // Quem deu baixa no Pix direto. No gateway ninguém dá — quem confirma é o webhook — então
    // fica nulo lá. Aqui é uma PESSOA decidindo que o dinheiro entrou, e isso precisa de nome.
    public int? ConfirmadoPorJogadorId { get; set; }

    // Depois disso a cobrança perde a validade e a vaga segurada é liberada.
    public DateTime? ExpiraEm { get; set; }

    // Quando o lembrete de "sua cobrança está pra vencer" foi enviado — evita mandar
    // o mesmo aviso duas vezes. Nulo = ainda não avisou.
    public DateTime? LembreteEnviadoEm { get; set; }
}
