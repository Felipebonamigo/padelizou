using Padelizou.Models;

namespace Padelizou.Services;

// Exclusão de conta pelo próprio usuário (LGPD).
//
// A conta é ANONIMIZADA, não apagada, e isso não é preguiça — é o único caminho correto:
//
//   · "Pagamento.JogadorId" é ON DELETE CASCADE: apagar a conta levaria junto o registro
//     fiscal que o MEI obriga a guardar;
//   · "Dupla.Jogador1Id" é NO ACTION: o banco simplesmente RECUSA apagar quem já jogou;
//   · o resultado de uma partida é dado de quatro pessoas — sumir com um nome estraga o
//     histórico dos outros três.
//
// A LGPD (art. 16) permite reter o que é obrigação legal e o que toca direito de terceiro.
// O que ela exige é que a pessoa deixe de ser identificável — e é exatamente o que a
// anonimização faz: nome, CPF, e-mail, login, telefone, foto, cidade e senha somem.
public static class ExclusaoDeConta
{
    public const string NomeDeQuemSaiu = "Jogador removido";

    // Motivo pra recusar, ou null se pode excluir.
    //
    // As duas travas existem porque a saída de uma pessoa deixaria OUTRAS pessoas na mão —
    // não é sobre proteger o sistema, é sobre não quebrar o torneio de quem está jogando.
    public static string? ProblemaParaExcluir(
        bool ehUnicoAdminDoSistema, int torneiosEmQueEhUnicoOrganizador)
    {
        if (ehUnicoAdminDoSistema)
            return "Você é o único administrador do Padelizou. Passe a administração para "
                 + "outra pessoa antes de excluir sua conta.";

        if (torneiosEmQueEhUnicoOrganizador > 0)
            return $"Você é o único organizador de {torneiosEmQueEhUnicoOrganizador} torneio(s) "
                 + "ainda não finalizado(s). Inclua outro organizador (ou finalize o torneio) "
                 + "antes de excluir sua conta.";

        return null;
    }

    // CPF de quem saiu. Precisa ser único (a coluna tem índice único) e NÃO pode parecer
    // um CPF de verdade — daí começar com letra: nenhum CPF real colide com isto, e a busca
    // por CPF (que exige 11 dígitos) nunca devolve uma conta excluída por engano.
    public static string CpfDeQuemSaiu(int jogadorId) => $"EX{jogadorId:D9}";

    // Raspa os dados pessoais da entidade. Puro de propósito: é a parte que precisa estar
    // certa, e assim dá pra testar campo a campo sem banco.
    public static void Anonimizar(Jogador jogador, DateTime agora)
    {
        jogador.Nome = NomeDeQuemSaiu;
        jogador.Cpf = CpfDeQuemSaiu(jogador.Id);

        jogador.Apelido = null;
        jogador.Email = null;
        jogador.Login = null;
        jogador.Celular = null;
        jogador.Cidade = null;
        jogador.Estado = null;
        jogador.Instagram = null;
        jogador.FotoPerfil = null;

        // Sem senha e sem token: a conta deixa de ser acessível, inclusive por "esqueci
        // minha senha" — sem isto, um link antigo no e-mail ainda abriria a porta.
        jogador.SenhaHash = null;
        jogador.TokenRecuperacao = null;
        jogador.TokenRecuperacaoExpiraEm = null;

        // Nenhum canal de contato continua ligado: mandar e-mail pra quem pediu pra sair
        // seria o oposto do que ela pediu.
        jogador.NotificarEmail = false;
        jogador.NotificarWhatsApp = false;
        jogador.NotificarTorneiosAbertos = false;
        jogador.NotificarSeguidosTorneio = false;
        jogador.NotificarAvisoJogo = false;
        jogador.NotificarJogoAula = false;
        jogador.NotificarRaqueteLivre = false;
        jogador.AceitaConvitesJogo = false;

        // Some das telas de perfil, mas segue aparecendo no resultado dos jogos que disputou.
        jogador.PerfilPrivado = true;

        // Poder de administrador não sobrevive à saída.
        jogador.IsAdminGeral = false;
        jogador.IsAdminRaiz = false;
        jogador.IsProfessor = false;

        jogador.ExcluidoEm = agora;
    }
}
