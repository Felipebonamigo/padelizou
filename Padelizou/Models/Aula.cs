using Padelizou.Models;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace padelizou.Models;

[Table("Aula")]
public partial class Aula
{
    public int Id { get; set; }
    public int ProfessorId { get; set; }
    public int? AlunoId { get; set; }
    public int LocalAulaId { get; set; }
    public DateTime DataHora { get; set; }
    public decimal Preco { get; set; }
    public string Status { get; set; } = null!;

    // Quanto tempo a aula dura. Até 10/08/2026 o sistema inteiro fingia que toda aula era de
    // uma hora: a Google Agenda criava evento de 60 min e a trava de conflito só olhava o
    // horário de INÍCIO — duas aulas de 1h30 encavaladas passavam sem um pio.
    //
    // Nasce em 60 porque era a duração implícita de tudo que já estava marcado.
    public int DuracaoMinutos { get; set; } = 60;

    // Série semanal SEM data pra acabar ("todo sábado às 9h, e pronto"). As aulas continuam
    // nascendo em lote — o que muda é que o RenovadorDeAulaFixaBackgroundService repõe o
    // horizonte enquanto isto estiver ligado. Desligar não apaga nada: só para de renovar.
    public bool RecorrenciaSemFim { get; set; }

    // Preenchidos só quando o professor adiciona a aula manualmente para um aluno
    // sem conta no sistema (AlunoId fica null nesse caso).
    public string? NomeAlunoAvulso { get; set; }
    public string? TelefoneAlunoAvulso { get; set; }

    // Agrupa as aulas geradas de uma mesma série semanal recorrente (null se avulsa/única)
    public Guid? RecorrenciaId { get; set; }

    // Nome com que o aluno se apresenta NESTA aula. O cadastro pode dizer "Felipe C. B. dos
    // Santos" e o professor conhecer outro nome — e o professor precisa saber quem chega na
    // quadra, não quem está no banco.
    public string? NomeCompletoAluno { get; set; }

    // Quem mais vem nesta aula. Texto livre de propósito: aula de padel é frequentemente em
    // dupla ou trio, e exigir que cada acompanhante tenha conta no site travaria a marcação
    // por causa de quem nem usa o app. O professor só precisa saber quantos e quem.
    public string? Acompanhantes { get; set; }

    // QUANTOS alunos nesta aula (1, 2 ou 3). Acompanhantes diz quem vem; isto diz o tamanho
    // — e tamanho é preço: Services/PrecoDaAula lê daqui qual valor do local se aplica.
    //
    // Nasce em 1 porque toda aula anterior a 03/08/2026 era individual: era o único preço
    // que existia.
    public int QuantidadeAlunos { get; set; } = 1;

    // A MESMA turma de quadra, quando ela tem MAIS DE UM aluno com cobrança PRÓPRIA — cada
    // um sua linha de Aula, seu preço (a fatia dele do valor da turma) e sua ficha, mas o
    // mesmo horário/local/duração e o MESMO evento na Google Agenda (GoogleEventId igual nas
    // linhas do grupo). Nulo é a aula de sempre: sozinha, ou em grupo com um "Acompanhantes"
    // solto sem cobrança própria (Acompanhantes e TurmaId não se excluem — o professor pode
    // ter 2 alunos com conta cobrados à parte e um terceiro "de brinde" só anotado ali).
    //
    // ESTÁVEL: nasce UMA vez quando o professor cria a turma e viaja com cada série pra
    // sempre — a renovação semanal (Services/RenovacaoDaAulaFixa) copia o mesmo valor pra
    // frente, não sorteia um novo. Não existe RecorrenciaId de grupo: cada aluno mantém a
    // PRÓPRIA série, independente dos colegas (um sai — ou cancela só a dele numa semana —,
    // os outros continuam sem quebrar nada). É também por isto que precisa ser estável: sem
    // um jeito de reconhecer "esta aula aqui é da MESMA turma", a trava de conflito de
    // horário do renovador enxergaria os colegas de turma uns dos outros como aula
    // concorrente no mesmo horário — e pularia a semana pensando que a quadra já tem outra
    // coisa marcada, quando é a turma toda jogando junto.
    public Guid? TurmaId { get; set; }

    // Token opaco usado no link de aceitar/recusar enviado por e-mail (sem exigir login)
    public Guid TokenConfirmacao { get; set; } = Guid.NewGuid();

    // Preenchido quando o evento é criado na Google Agenda do professor
    public string? GoogleEventId { get; set; }

    // ---- Presença ----
    // null = professor ainda não marcou; true = veio; false = faltou.
    // Quem falta com aviso dentro do prazo é cancelamento (Status), não falta.
    public bool? Compareceu { get; set; }

    // ---- Cancelamento ----
    public DateTime? CanceladaEm { get; set; }
    public string? CanceladaPor { get; set; }   // "Aluno" | "Professor"

    // Falta/cancelamento fora do prazo que o professor decidiu cobrar assim mesmo.
    // Entra na previsão financeira como valor a receber.
    public bool CobrarMesmoFaltando { get; set; }

    // NESTA aula o aluno acerta o aluguel da quadra direto com o clube. O custo por aula
    // do local (LocalAula.CustoPorAula) deixa de contar como despesa do professor aqui —
    // é por aula, e não por local, porque o mesmo aluno ora paga a quadra, ora não.
    public bool AlunoPagaQuadra { get; set; }

    // Esta aula REPÕE outra, que ficou como "A recuperar" (ver Services/Reposicao). Nula na
    // esmagadora maioria — só a aula de encaixe aponta pra trás.
    //
    // A ligação existe pra fila de pendências saber quem já foi encaixado: sem ela, a única
    // resposta possível seria "o professor marcou alguma aula pra esse aluno depois?", que
    // acerta por acidente e erra no aluno que tem aula toda terça.
    public int? RecuperaAulaId { get; set; }

    // Quantas horas antes da aula o cancelamento foi feito. Guardado no momento do
    // cancelamento porque a política do professor pode mudar depois — o que valeu
    // pro aluno foi a regra do dia.
    public int? HorasDeAntecedenciaCancelamento { get; set; }

    // Quem vai dar a aula
    [ForeignKey("ProfessorId")]
    [InverseProperty("AulasDadas")]
    public virtual Jogador Professor { get; set; } = null!;

    // Quem vai receber a aula (null quando é aluno avulso, ver NomeAlunoAvulso)
    [ForeignKey("AlunoId")]
    [InverseProperty("AulasRecebidas")]
    public virtual Jogador? Aluno { get; set; }

    [ForeignKey("LocalAulaId")]
    public virtual LocalAula LocalAula { get; set; } = null!;

    // A aula que esta aqui repõe (null quando esta não é reposição de nada).
    [ForeignKey("RecuperaAulaId")]
    public virtual Aula? RecuperaAula { get; set; }
}