using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Dupla")]
public partial class Dupla
{
    public int Id { get; set; }

    public int CategoriaId { get; set; }

    public int Jogador1Id { get; set; }

    // NULO = inscrito sozinho, ainda procurando parceiro. A dupla existe (ocupa vaga,
    // pagou, entra na lista de espera), mas fica de fora do sorteio até ter os dois nomes.
    public int? Jogador2Id { get; set; }

    public string? Codigo { get; set; }

    // Convite de parceiro: o link que fecha a dupla sem ninguém precisar do CPF do outro.
    // Nulo depois de aceito — token usado não volta a valer (ver Services/ConviteDeParceiro).
    public string? ConviteToken { get; set; }
    public DateTime? ConviteCriadoEm { get; set; }

    public virtual Categoria Categoria { get; set; } = null!;

    public virtual Jogador Jogador1 { get; set; } = null!;

    public virtual Jogador? Jogador2 { get; set; }

    // Só entra em chaves/grupos quando a dupla está fechada.
    [NotMapped]
    public bool Completa => Jogador2Id != null;

    // Situação do pagamento desta inscrição. Vira true sozinho quando o webhook do Asaas
    // confirma, e o organizador pode virar na mão a qualquer momento — muita inscrição é
    // paga em dinheiro na quadra, e a última palavra sobre quem pagou é sempre dele.
    public bool Pago { get; set; }
    public DateTime? PagoEm { get; set; }

    // QUANTO ESTA INSCRIÇÃO CUSTOU, gravado quando ela nasce.
    //
    // Existe porque desde 08/08/2026 duas inscrições do mesmo torneio podem custar valores
    // diferentes (a segunda categoria do mesmo jogador é mais barata — ver
    // Services/PrecoDaInscricao). ⚠️ Sem esta coluna, todo somatório de dinheiro do sistema
    // — relatório do dia do jogo, lista de devolução do cancelamento, base da taxa do "por
    // fora" — recalcularia `PrecoInscricao × pessoas` e MENTIRIA, porque não tem como saber
    // quem entrou pela segunda vez.
    //
    // NULO = inscrição anterior a esta coluna: quem soma cai no cálculo de sempre, que
    // naquele tempo estava certo (havia um preço só).
    public decimal? ValorInscricao { get; set; }

    public virtual ICollection<Partida> PartidasDupla1 { get; set; } = new List<Partida>();

    public virtual ICollection<Partida> PartidasDupla2 { get; set; } = new List<Partida>(); 
    // A quinta só existe em torneio que COMEÇA na quinta — ver Torneio.QuintaEhDiaDoTorneio.
    public bool ImpedimentoQuintaNoite { get; set; }
    public bool ImpedimentoSextaNoite { get; set; }
    public bool ImpedimentoSabadoManha { get; set; }
    public bool ImpedimentoSabadoTarde { get; set; }
    public int? GrupoTorneioId { get; set; }
    public virtual GrupoTorneio? GrupoTorneio { get; set; }

    [NotMapped]
    public int Jogos { get; set; }

    [NotMapped]
    public int Vitorias { get; set; }

    [NotMapped]
    public int Derrotas { get; set; }

    [NotMapped]
    public int SaldoGames { get; set; }
    public string UltimaFase { get; set; } = "Grupos";
    public string? Grupo { get; set; } // Vai receber "A", "B", etc.

    // Verdadeiro quando a categoria (ou o torneio) já estava com vagas esgotadas no
    // momento da inscrição. Fica de fora das chaves/contagens até ser promovido.
    public bool EmListaDeEspera { get; set; }

    // Nulo = inscrição feita antes de 25/07/2026 (quando a coluna nasceu) — usado nas
    // métricas de uso do admin (inscrições por semana).
    public DateTime? CriadoEm { get; set; } = DateTime.Now;

    // Check-in no dia do torneio: a dupla apareceu. Nulo = ainda não fez check-in.
    // Serve pro organizador ver quem falta antes de começar, e evitar W.O. surpresa.
    public DateTime? CheckInEm { get; set; }

    // Último lembrete de "você ainda não pagou" já enviado, guardado como o MARCO em dias que
    // faltavam pro prazo (ver Services/LembreteDeInscricaoNaoPaga). Nulo = nenhum ainda.
    //
    // ⚠️ Guarda o marco e não a data do envio de propósito: com a data, decidir "já mandei o
    // aviso de 7 dias?" viraria uma conta com o calendário toda vez, e o varredor roda de hora
    // em hora — bastaria um fuso ou um restart no meio pra alguém levar o mesmo aviso duas
    // vezes. Com o marco, a pergunta é uma comparação de inteiros e a resposta é sempre a mesma.
    public int? UltimoLembreteDePagamento { get; set; }

    // ---- Time (categoria de times) ----
    // Preenchido = esta linha é um TIME, não dois jogadores. Jogador1Id aponta pro
    // organizador que cadastrou (a coluna é NOT NULL e o sistema inteiro depende dela),
    // mas NENHUMA regra de jogador vale aqui: não pontua no ranking, não recebe push,
    // não fica fora do sorteio por "estar sem parceiro".
    public string? NomeTime { get; set; }

    // Vínculo opcional com o cadastro de Times (traz o escudo na tela). SetNull no banco:
    // apagar um time do cadastro não pode apagar a história de um torneio.
    public int? TimeId { get; set; }
    public virtual Time? Time { get; set; }

    [NotMapped]
    public bool EhTime => NomeTime != null;

    // O nome que as telas mostram, seja time ou dupla. Defensivo com as navegações:
    // consulta sem Include não pode estourar a página inteira por causa de um rótulo.
    [NotMapped]
    public string NomeDeExibicao => NomeTime
        ?? (Jogador1 == null ? $"Dupla {Id}"
            : Jogador2 == null ? Jogador1.NomeNaTela
            : $"{Jogador1.NomeNaTela} & {Jogador2.NomeNaTela}");
}
