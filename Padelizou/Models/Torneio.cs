using System;
using System.Collections.Generic;

namespace Padelizou.Models;

using padelizou.Models;
using System.ComponentModel.DataAnnotations.Schema;
[Table("Torneio")]
public partial class Torneio
{
    public int Id { get; set; }

    public string Nome { get; set; } = null!;

    public string Codigo { get; set; } = null!;

    public virtual ICollection<Categoria> Categorias { get; set; } = new List<Categoria>();

    // Quem organiza vive em TorneioOrganizadores (vários por torneio, com NivelAcesso).
    // A entidade Organizador antiga foi removida em 26/07/2026 — tabela estava vazia.
    public DateTime? DataInicio{ get; set; }
    public bool PermiteImpedimentos { get; set; }
    public bool PermiteImpedimentoSextaNoite { get; set; } = true;
    public bool PermiteImpedimentoSabadoManha { get; set; } = true;
    public bool PermiteImpedimentoSabadoTarde { get; set; } = true;
    // Valor que UMA PESSOA paga pra se inscrever — sempre por pessoa, nunca por dupla.
    // A inscrição de uma dupla cobra o dobro (ver ValorCobrado); a de um americano, uma vez.
    // É este valor que o jogador vê anunciado: a taxa do Padelizou sai daqui de dentro,
    // não é somada por cima.
    public decimal PrecoInscricao { get; set; }

    // Quanto a mais custa CADA impedimento de horário marcado na inscrição. Zero = de graça.
    //
    // Impedimento não é capricho do sistema: cada um deles tira uma janela da grade e obriga
    // o organizador a montar os jogos em volta. Cobrar por isso é o jeito honesto de fazer
    // quem realmente precisa marcar pagar pela flexibilidade que tirou de todo mundo — e de
    // desestimular quem marca "por via das dúvidas".
    public decimal TaxaPorImpedimento { get; set; }

    // Quantas pessoas entram numa inscrição deste formato.
    [NotMapped]
    public int PessoasPorInscricao => Formato == "Americano" ? 1 : 2;

    // O que é efetivamente cobrado de quem se inscreve: o preço por pessoa vezes o número
    // de pessoas, mais a taxa de cada impedimento marcado.
    public decimal ValorCobrado(bool inscricaoDeDupla, int impedimentos = 0) =>
        PrecoInscricao * (inscricaoDeDupla ? 2 : 1)
        + TaxaPorImpedimento * Math.Max(impedimentos, 0);

    // Como o dinheiro da inscrição corre. Escolhido pelo organizador ao criar o torneio, e
    // é ele quem define a taxa — porque a escolha é dele, não do jogador. A forma que o
    // jogador escolhe no checkout não pode mexer na taxa: o split é fixado quando a cobrança
    // nasce, muito antes de alguém pagar.
    //
    // "OnlinePix"   — cobrança travada em Pix. Cai na hora, custo baixo → taxa menor.
    // "OnlineTodas" — Pix, boleto, débito e crédito à escolha do jogador. Crédito custa
    //                 caro e só cai em 32 dias → taxa maior.
    // "Externo"     — o Padelizou não toca no dinheiro, só organiza. Sem custo de gateway
    //                 nem risco → taxa menor de todas, cobrada do organizador depois.
    public string FormaPagamento { get; set; } = "OnlinePix";

    [NotMapped]
    public bool SomentePix => FormaPagamento == "OnlinePix";

    // Fixo em "Descontada" desde 27/07/2026: o jogador paga exatamente o valor anunciado e a
    // taxa do Padelizou sai de dentro dele. Já foi uma escolha do organizador ("Somada" somava
    // a taxa por cima), mas obrigá-lo a decidir isso só atrapalhava quem queria anunciar um
    // preço redondo. A coluna continua porque o cálculo do rateio a consome.
    public string ModoComissao { get; set; } = "Descontada";

    [NotMapped]
    public bool CobraPeloSite => FormaPagamento.StartsWith("Online");

    // Pagar é condição pra se inscrever, ou dá pra garantir a vaga e acertar depois?
    // true (padrão) mantém o comportamento que já existia: sem pagar, sem inscrição.
    public bool PagamentoObrigatorioNaInscricao { get; set; } = true;

    // Data limite pra quitar quando o pagamento não é obrigatório na hora. Nulo = sem prazo.
    public DateTime? PrazoPagamento { get; set; }

    // O que acontece com quem não pagou até o prazo: sai do torneio ou só fica devendo.
    // Padrão false — tirar alguém de um torneio é grave demais pra ser o comportamento
    // implícito; o organizador liga isso conscientemente.
    public bool ExcluirSeNaoPagar { get; set; }

    public string? LocalTorneio { get; set; }
    public string? ImagemCapa { get; set; }
    public int QuantidadeQuadras { get; set; }
    public string Status { get; set; } = "Inscrições Abertas";
    public string Formato { get; set; } = "Padrao"; // "Padrao" ou "Americano"
    public bool FormatoUnico { get; set; }
    public int SetsFaseGrupos { get; set; }
    public int GamesFaseGrupos { get; set; }
    public int SetsFaseMataMata { get; set; }
    public int GamesFaseMataMata { get; set; }
    public int SetsFaseFinal { get; set; }
    public int GamesFaseFinal { get; set; }
    public int ClubeId { get; set; }
    public Clube Clube { get; set; }
    public int TempoPrevistoPartidaMinutos { get; set; } = 50; // Padrão de 50 minutos

    // Janela de jogos de cada dia. DataInicio guarda só a data — sem a hora de abertura a
    // grade começava à meia-noite. Ao bater no fim, o que sobra vai pro dia seguinte, na
    // hora de abertura: torneio não vira a noite.
    public TimeSpan HoraInicioDoDia { get; set; } = new(8, 0, 0);
    public TimeSpan HoraFimDoDia { get; set; } = new(22, 0, 0);

    // Quando o primeiro jogo entra na quadra.
    [NotMapped]
    public DateTime AberturaDaGrade =>
        (DataInicio ?? DateTime.Today).Date.Add(HoraInicioDoDia);
    public int TamanhoGrupo { get; set; } = 3;
    public int ClassificadosPorGrupo { get; set; } = 2;

    // OBSOLETO — mantido só para não dropar a coluna em produção (evita janela de erro
    // com o app antigo rodando). A regra agora é controlada por RestricaoCategoria.
    public bool BloquearCategoriaInferior { get; set; }

    // Regra anti-sandbagging configurável pelo organizador. Define o "gatilho" a partir
    // do qual um jogador comprova nível numa categoria e não pode se inscrever em
    // categorias mais fracas. Valores:
    //   "Livre"     -> sem restrição (qualquer um em qualquer categoria)
    //   "SaiuChave" -> comprova ao passar da fase de grupos (Quartas+)
    //   "Semifinal" -> comprova ao chegar à semifinal (Semi+)
    //   "Final"     -> comprova ao chegar à final (Final ou Campeão)
    public string RestricaoCategoria { get; set; } = "Livre";

    // Vagas máximas somando TODAS as categorias do torneio. Null = sem limite.
    // Quem se inscrever depois de atingido vai pra lista de espera (Dupla/InscricaoAmericana.EmListaDeEspera).
    public int? LimiteDuplasTotal { get; set; }

    // Torneio restrito: só quem tiver a ChaveAcesso consegue se inscrever (Dupla ou
    // InscricaoAmericana). Chave gerada automaticamente na criação quando Restrito = true.
    public bool Restrito { get; set; }
    public string? ChaveAcesso { get; set; }

    // Some da listagem pública (Torneios/Index) — só acessível por quem tem o link direto
    // ou é organizador. Não afeta a inscrição em si (isso é papel de Restrito/ChaveAcesso).
    public bool Oculto { get; set; }

    // O mesmo jogador pode entrar em mais de uma categoria deste torneio?
    // Default true = comportamento que sempre existiu (não havia trava nenhuma).
    // Desligar é útil pra evitar choque de horário: o jogador fica preso a uma categoria só.
    public bool PermiteMultiplasCategorias { get; set; } = true;
}
