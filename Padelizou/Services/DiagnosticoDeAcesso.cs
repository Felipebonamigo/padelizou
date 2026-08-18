using Padelizou.Models;

namespace Padelizou.Services;

// Por que uma pessoa não consegue entrar. São quatro respostas, e a diferença entre elas é
// tudo: cada uma manda a pessoa pra um lugar diferente, e três das quatro NÃO se resolvem
// com "esqueci minha senha".
//
// ⚠️ Esta classificação existia escrita à mão dentro de AuthController.EsqueciSenha, em três
// `if` seguidos. Quando a tela do admin (/Admin/Acesso) precisou da MESMA leitura pra dizer
// ao Felipe o que responder no WhatsApp, copiar os três `if` teria criado a segunda cópia —
// e a segunda cópia é como o site passa a dizer uma coisa pra pessoa e outra pro admin sobre
// a mesma conta. Agora as duas telas perguntam aqui.
public enum SituacaoDeAcesso
{
    // Ninguém com esse identificador. Atenção: quem excluiu a conta também cai aqui, porque
    // a exclusão troca o CPF por "EX000000123" (ExclusaoDeConta.CpfDeQuemSaiu) — o CPF real
    // dela deixa de existir na base.
    NaoAchei,

    // Pediu exclusão (LGPD). A linha continua viva pro histórico dos jogos, sem senha e sem
    // nenhum dado pessoal. Não há acesso a devolver.
    ContaExcluida,

    // A conta existe, com o histórico de torneios dentro, mas NUNCA teve senha — foi um
    // organizador que digitou o CPF dela numa inscrição. Não há senha a recuperar: o que
    // falta é CRIAR a conta com esse mesmo CPF, e o Cadastro reivindica a linha existente.
    PreCadastro,

    // Tem senha, mas não tem e-mail pra onde mandar o link. Acontece com quem assumiu um
    // pré-cadastro sem preencher e-mail, e é o único beco sem saída de verdade: a pessoa
    // sozinha não sai dele, alguém da casa tem que preencher o e-mail na conta.
    SemEmail,

    // O caminho normal: tem senha e tem e-mail. "Esqueci minha senha" resolve, e desde
    // 17/08/2026 ele aceita o CPF — então nem o e-mail a pessoa precisa lembrar.
    PodeRecuperar,
}

public static class DiagnosticoDeAcesso
{
    // A ORDEM importa e não é arbitrária: conta excluída vem antes de pré-cadastro porque a
    // exclusão também zera o SenhaHash, e sem esta ordem quem pediu pra sair apareceria como
    // "é só se cadastrar de novo" — dizendo a quem foi embora que a porta continua aberta.
    public static SituacaoDeAcesso De(Jogador? jogador) => jogador switch
    {
        null => SituacaoDeAcesso.NaoAchei,
        { Excluido: true } => SituacaoDeAcesso.ContaExcluida,
        { EhPreCadastro: true } => SituacaoDeAcesso.PreCadastro,
        _ when string.IsNullOrWhiteSpace(jogador.Email) => SituacaoDeAcesso.SemEmail,
        _ => SituacaoDeAcesso.PodeRecuperar,
    };

    // Dá pra mandar o link de "esqueci minha senha"? É a única situação em que o e-mail sai.
    public static bool PodeMandarLink(Jogador? jogador) =>
        De(jogador) == SituacaoDeAcesso.PodeRecuperar;
}
