using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// POR QUE ESSA VAGA ABRIU — o histórico de quem saiu do torneio (Felipe, 25/09/2026: *"ter um
// histórico para isso, para ver quem desistiu"*).
//
// ⚠️ NÃO EXISTIA NADA PRA MOSTRAR. `TirarDaInscricaoAsync` faz `Duplas.Remove(dupla)`: depois do
// cancelamento não sobrava nome, data, nem se estava paga. Não era registro escondido, era
// ausência de dado — e é por isso que esta tabela precisou nascer em vez de uma consulta.
//
// ⚠️ UMA LINHA POR SAÍDA, E NÃO POR PESSOA. A dupla que sai inteira abre UMA vaga, não duas —
// com a linha por pessoa, a contagem de vagas abertas dobraria calada. E a pergunta do Felipe
// ("quem desistiu") é respondida igual, porque os dois nomes estão na mesma linha.
// O que tirou a inscrição dali. ⚠️ São TRÊS porque existem QUATRO portas de saída e duas delas
// são a mesma coisa pra quem lê (desistir sozinho e desistir a dupla inteira) — o que as separa
// é `AbriuVaga`, não o motivo.
public enum MotivoDaSaida
{
    // O próprio inscrito clicou: `Desistir` ou `DesistirDoAmericano`.
    Desistiu,

    // O organizador tirou (`RemoverDupla`). Fica no histórico mesmo ele sabendo o que fez: com
    // dois organizadores, um não tem como saber o que o outro mexeu.
    RemovidoPeloOrganizador,

    // O sistema tirou por não ter sido paga até o prazo (`TirarDuplaDoTorneioAsync`). É a única
    // em que `QuemPediuId` é nulo.
    NaoPagou,
}

[Table("SaidaDoTorneio")]
public class SaidaDoTorneio
{
    public int Id { get; set; }

    public int TorneioId { get; set; }

    // ⚠️ SEM FK, de propósito: categoria apagada não pode levar o histórico junto. A tela
    // resolve o nome na leitura e mostra a categoria como removida quando ela não existe mais.
    public int CategoriaId { get; set; }

    // ⚠️ QUEM SAIU, SEM FK PRA `Jogador` — a mesma escolha do `InscritoPorOutro.InscritoPorId` e
    // do `Dupla.ImpedimentoAlteradoPorId`, e pelo mesmo motivo escrito lá: já existe caminho de
    // cascade demais saindo de Jogador, e o que importa aqui é o registro histórico.
    //
    // ⚠️ E O NOME NÃO FICA CONGELADO AQUI. Guardar uma cópia do nome criaria um SEGUNDO lugar
    // com dado pessoal, que ninguém lembra de limpar quando alguém exerce o direito de sair — a
    // régua disso mora num lugar só (Services/ExclusaoDeConta). O custo é que a linha de uma
    // conta encerrada mostra o que aquela política deixar, e esse custo é o certo a pagar.
    public int Jogador1Id { get; set; }

    // Nulo quando saiu UMA pessoa só (o "só eu saio", em que o parceiro continua inscrito). ⚠️
    // Quem FICOU nunca entra aqui: ele não saiu, e pô-lo na linha faria o histórico acusar de
    // desistência quem seguiu no torneio.
    public int? Jogador2Id { get; set; }

    // Quem clicou. ⚠️ NULO É A ASSINATURA DO AUTOMÁTICO (não pagou no prazo) — pôr o id do
    // organizador aqui faria a lista dizer que ele removeu alguém que nunca tocou.
    public int? QuemPediuId { get; set; }

    public MotivoDaSaida Motivo { get; set; }

    // O texto livre que quem desiste pode escrever. Opcional de propósito: obrigatório faz a
    // pessoa digitar "x" pra passar, e aí o campo mente.
    public string? Observacao { get; set; }

    // É o que liga o estorno — o aviso ao organizador muda de texto por causa dele.
    public bool EstavaPaga { get; set; }

    // Falso no "só eu saio". ⚠️ É por este campo que a tela conta vagas; derivar de `Motivo`
    // daria errado, porque desistir sozinho e desistir a dupla inteira têm o MESMO motivo.
    public bool AbriuVaga { get; set; }

    public DateTime SaiuEm { get; set; } = DateTime.Now;

    [ForeignKey("TorneioId")]
    public virtual Torneio Torneio { get; set; } = null!;
}
