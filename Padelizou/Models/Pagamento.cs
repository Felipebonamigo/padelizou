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

    // Total pago pelo jogador.
    public decimal Valor { get; set; }

    // Fatia que o split manda direto pro organizador/professor.
    public decimal ValorRepasse { get; set; }

    // Fatia do Padelizou, bruta — a taxa do Asaas ainda é descontada dela.
    public decimal Comissao { get; set; }

    public string? AsaasPaymentId { get; set; }
    public string? AsaasCustomerId { get; set; }
    public string? InvoiceUrl { get; set; }

    // "Pendente" | "Confirmado" | "Cancelado" | "Estornado"
    public string Status { get; set; } = "Pendente";

    public DateTime CriadoEm { get; set; } = DateTime.Now;
    public DateTime? ConfirmadoEm { get; set; }

    // Depois disso a cobrança perde a validade e a vaga segurada é liberada.
    public DateTime? ExpiraEm { get; set; }

    // Quando o lembrete de "sua cobrança está pra vencer" foi enviado — evita mandar
    // o mesmo aviso duas vezes. Nulo = ainda não avisou.
    public DateTime? LembreteEnviadoEm { get; set; }
}
