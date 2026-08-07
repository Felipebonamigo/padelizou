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
    // "Padrao" (duplas fixas, grupos + mata-mata), "Americano" (inscrição individual,
    // parceiro troca a cada rodada) ou "AmericanoDuplas" (duplas fixas, todos contra todos,
    // vence quem somar mais games).
    public string Formato { get; set; } = "Padrao";
    public bool FormatoUnico { get; set; }
    public int SetsFaseGrupos { get; set; }
    public int GamesFaseGrupos { get; set; }
    public int SetsFaseMataMata { get; set; }
    public int GamesFaseMataMata { get; set; }
    public int SetsFaseFinal { get; set; }
    public int GamesFaseFinal { get; set; }
    public int ClubeId { get; set; }
    // Propriedade de navegação: só tem valor em consulta que fez Include. `null!` é o combinado
    // do EF pra isso — quem usa sem Include recebe nulo, e é assim mesmo.
    public Clube Clube { get; set; } = null!;
    public int TempoPrevistoPartidaMinutos { get; set; } = 50; // Padrão de 50 minutos

    // Torneio SEM hora marcada: os jogos nascem numa ORDEM e vão pra quadra conforme ela
    // vaga, chamados pela Mesa de Controle. É como roda a maioria dos internos de clube —
    // ninguém consegue prever quanto dura um jogo de 4 games com desempate, e uma grade que
    // atrasa 40 min logo cedo passa o resto do dia mentindo pra todo mundo.
    //
    // Ligado, o sorteio NÃO calcula horário nenhum: `Partida.HorarioPrevisto` fica nulo e a
    // ordem é a de criação. As telas passam a mostrar "1º, 2º, 3º jogo" no lugar da hora, e
    // os avisos que dependem de relógio ("seu jogo é o próximo", "sua quadra atrasou") não
    // têm base pra existir — a Mesa é que avisa, quando chama.
    public bool SemHorarioPrevisto { get; set; }

    // Janela de jogos. DataInicio guarda só a data — sem a hora de abertura a grade
    // começava à meia-noite.
    //
    // São DUAS aberturas porque o torneio de fim de semana tem duas: sexta começa à noite
    // (todo mundo trabalha de dia) e sábado/domingo começam cedo. Uma hora só obrigaria a
    // escolher entre marcar jogo às 8h de uma sexta útil ou desperdiçar a manhã de sábado.
    public TimeSpan HoraInicioDoDia { get; set; } = new(18, 0, 0);
    public TimeSpan HoraInicioDiasSeguintes { get; set; } = new(8, 0, 0);

    // Hora limite pra um jogo COMEÇAR — não pra terminar. O jogo das 23h50 varando a
    // madrugada é o normal do torneio, e ninguém quer somar 23h50 + 50 min pra preencher
    // um campo.
    public TimeSpan HoraFimDoDia { get; set; } = new(23, 50, 0);

    // Americano: se der empate em games na liderança, jogam uma partida final pra decidir.
    // No individual, os empatados escolhem um parceiro cada e sai um jogo só (montado pelo
    // organizador); no Americano de Duplas as duas duplas empatadas jogam entre si, e o robô
    // cria o jogo sozinho. Fica desligado por padrão — tem torneio que resolve empate no
    // critério, sem quadra extra.
    public bool DesempateAmericano { get; set; }

    // Este Americano vale ponto no Ranking Americano? Nasce DESLIGADO e é contratado pelo
    // organizador na criação: R$ 5 por pessoa inscrita (ver Services/PontosDoAmericano).
    //
    // O Americano NÃO pontua no ranking oficial em hipótese nenhuma — isso é formato, não
    // escolha (ver EstatisticasService.ContaNoRanking). O que se contrata aqui é a entrada no
    // ranking PRÓPRIO dele, que é outra tabela e outra tela.
    //
    // Por que pago: ponto de graça é ponto que se fabrica. Quatro amigos, placares inventados,
    // ranking novo toda semana. O preço por pessoa faz a conta não fechar pra quem quer
    // fabricar e não pesar pra quem faz o rodízio de verdade.
    public bool PontuaNoRankingAmericano { get; set; }

    // ---- Aprovação do torneio (decisão do Felipe, 07/08/2026) ----
    // Quando o admin liberou este torneio pra APARECER. Nulo = criado e ainda esperando.
    //
    // ⚠️ Esperar aprovação NÃO trava nada pro organizador: ele monta o torneio, abre a página,
    // recebe inscrição e compartilha o link no mesmo minuto em que criou. O que não acontece
    // antes do OK é a VITRINE — listagem pública, Home e o aviso pra base inteira. Segurar a
    // criação faria ele esperar com a quadra reservada; segurar a vitrine não custa nada a ele
    // e é exatamente o que impede alguém de lotar o sistema de torneio inventado.
    //
    // ⚠️ O aviso "novo torneio aberto" saiu da criação e passou pra cá. Avisar na criação
    // entregaria à base justamente o torneio que ainda não foi olhado por ninguém.
    public DateTime? AprovadoEm { get; set; }

    public int? AprovadoPorId { get; set; }

    // Quando o acerto dos R$ 5 por pessoa foi confirmado. Nulo = contratado mas ainda não pago.
    //
    // ⚠️ O ponto só entra no ranking depois disto. Contratar é intenção; o que vale é o
    // dinheiro ter entrado — senão bastaria marcar a caixinha pra pontuar de graça, que é
    // exatamente o buraco que o preço existe pra fechar.
    //
    // Confirmação MANUAL pelo extrato, no painel admin — mesmo caminho da taxa do "por fora" e
    // do acerto com o Ranking RS. Aqui não passa gateway: é acerto direto com o organizador.
    public DateTime? RankingAmericanoPagoEm { get; set; }

    // Até quando o organizador tem a quadra. Opcional: sem isso o sistema ainda diz quando
    // o torneio termina, só não tem contra o que comparar pra avisar que não cabe.
    public DateTime? DataFim { get; set; }

    // Quando o primeiro jogo entra na quadra.
    [NotMapped]
    public DateTime AberturaDaGrade =>
        (DataInicio ?? DateTime.Today).Date.Add(HoraInicioDoDia);
    public int TamanhoGrupo { get; set; } = 3;
    public int ClassificadosPorGrupo { get; set; } = 2;

    // OBSOLETO — mantido só para não dropar a coluna em produção (evita janela de erro
    // com o app antigo rodando). A regra agora é controlada por RestricaoCategoria.
    public bool BloquearCategoriaInferior { get; set; }

    // DORMENTE desde 06/08/2026 — decisão do Felipe: quem nivela é o Ranking RS
    // (ValidarPeloRankingRs, logo abaixo), que enxerga o estado inteiro em vez de só quem já
    // jogou aqui. A opção saiu das telas de criar/editar torneio e a migração
    // 20260806190821_TravaDeCategoriaSoPeloRankingRs zerou as linhas existentes em "Livre".
    //
    // O motor NÃO foi apagado: DuplasController.MotivoBloqueioCategoriaAsync e os testes que o
    // cobrem continuam de pé, e nada aqui deixou de funcionar — só ninguém mais liga. Voltar a
    // oferecer é devolver o <select> nas duas views. Ver RANKING.md.
    //
    // Valores: "Livre" (sem restrição) | "SaiuChave" (Quartas+) | "Semifinal" | "Final".
    public string RestricaoCategoria { get; set; } = "Livre";

    // Valida as inscrições contra o Ranking RS (mundodoatleta.com.br)? Nasce DESLIGADO e é
    // escolha do organizador, torneio a torneio — o ranking é do Rio Grande do Sul, e um
    // torneio de outro estado não tem por que ser julgado por ele.
    //
    // Diferente do RestricaoCategoria acima, que olha só o histórico DENTRO do Padelizou:
    // aqui a pergunta vai pra fora, e a resposta chega por nome. As duas regras convivem —
    // quem passa numa ainda pode ser barrado pela outra.
    public bool ValidarPeloRankingRs { get; set; }

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

    // ---- A condição do "por fora" (5%) ----
    // No Externo o dinheiro não passa pelo sistema, então a taxa depende de o organizador
    // pagar — e a moeda de troca é a chave: sortear grupos/rodadas só depois de quitar (ou
    // de o Padelizou registrar uma negociação). Regras em Services/TaxaDoTorneioExterno.
    public DateTime? TaxaExternoPagaEm { get; set; }
    public DateTime? TaxaExternoNegociadaEm { get; set; }

    // Registro de COMO foi negociado ("isento — primeiro torneio", "acertado em 3x"...),
    // preenchido pelo admin que registrou. É o que evita a pergunta "quem liberou isso?".
    public string? TaxaExternoNegociadaObs { get; set; }

    // ---- Como o inscrito paga quando é "por fora" ----
    // A chave Pix do ORGANIZADOR, pra quem se inscreve pagar direto. O Padelizou não toca
    // nesse dinheiro nem confere se entrou: aqui a chave é só um recado, exibido junto com o
    // valor. Sem ela, "combinado com o organizador" deixava o jogador sem saber pra onde
    // mandar — e o organizador respondendo a mesma pergunta no WhatsApp trinta vezes.
    //
    // Guardada como texto porque chave Pix é qualquer coisa: CPF, celular, e-mail ou
    // aleatória. Validar formato aqui só criaria recusa em chave válida que o sistema não
    // reconhece.
    public string? ChavePixOrganizador { get; set; }

    // Recado livre do organizador pra quem se inscreve ("levem bola", "estacionamento pelos
    // fundos", "confirma no grupo do zap"). Aparece na tela de inscrição e no comprovante.
    public string? RecadoAosInscritos { get; set; }

    // ---- O que o inscrito quer saber e ninguém respondia ----
    // Duas datas PREVISTAS, não automáticas: quando as inscrições devem fechar e quando o
    // chaveamento deve sair. O sistema não age por elas (quem encerra e quem sorteia continua
    // sendo o organizador, no botão) — são promessa publicada, pra parar a pergunta "quando
    // sai a chave?" e pra o próprio organizador se cobrar do prazo que deu.
    public DateTime? PrevisaoEncerramentoInscricoes { get; set; }
    public DateTime? PrevisaoChaveamento { get; set; }

    // Usar a lista de chamada no dia? Nem todo torneio quer: num interno de 8 duplas o
    // organizador conhece todo mundo de vista, e a tela vira mais um botão pra ignorar.
    //
    // Nasce DESLIGADO: é o caso comum, e ligado por padrão o organizador ganhava uma tela que
    // não pediu — pior, uma tela que os inscritos veem e cobram. Quem já tinha torneio no ar
    // não perde nada: a migração CheckInOpcional gravou `true` em todos eles. Desligado, o
    // botão some e a rota recusa junto — sem isso o link antigo (ou o histórico do navegador)
    // continuaria abrindo uma tela que o organizador decidiu não usar.
    public bool UsaCheckIn { get; set; }

    // ---- Cancelamento (Services/CancelamentoDoTorneio) ----
    // O torneio não vai acontecer. Vive no Status ("Cancelado"), e estes dois campos guardam
    // o PORQUÊ e o QUANDO — sem eles o organizador cancela hoje e daqui a duas semanas não
    // lembra se aquele torneio caiu por chuva ou por falta de gente, e o inscrito que abre a
    // página vê um "Cancelado" seco.
    public string? MotivoCancelamento { get; set; }
    public DateTime? CanceladoEm { get; set; }
}
