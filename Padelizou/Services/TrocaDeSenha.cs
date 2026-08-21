namespace Padelizou.Services;

// Regras da troca de senha de quem JÁ ESTÁ DENTRO — separadas do controller pra poderem ser
// testadas, como as do "esqueci minha senha" (ver RecuperacaoSenha).
//
// Até 21/08/2026 o sistema só sabia REDEFINIR senha: o caminho era esquecê-la, pedir o link e
// abrir o e-mail. Quem lembrava da senha e simplesmente queria outra não tinha por onde — e
// quem desconfiava que alguém tinha visto a dela precisava mentir pro próprio site pra trocar.
//
// ⚠️ O mínimo é o MESMO de RecuperacaoSenha, de propósito: duas réguas de senha no mesmo
// sistema é a que aceita 4 caracteres virando a porta por onde se entra na conta trocada na
// outra tela. Aqui só se acrescenta o que é próprio da troca — a confirmação.
public static class TrocaDeSenha
{
    // Devolve a mensagem de recusa, ou null se a senha nova serve. O chamador decide onde mostrar.
    public static string? Problema(string? senha, string? confirmacao)
    {
        // O tamanho vem antes da confirmação: reclamar de "as duas não são iguais" quando as
        // duas estão iguais e curtas manda a pessoa consertar o que não está errado.
        if (RecuperacaoSenha.ProblemaComASenha(senha) is { } problema) return problema;

        // Digitar duas vezes existe porque o campo é mascarado: um dedo errado na ÚNICA
        // digitação vira uma senha que ninguém no mundo conhece — nem quem acabou de criá-la —
        // e a pessoa descobre isso no próximo login, já do lado de fora.
        if (!string.Equals(senha, confirmacao, StringComparison.Ordinal))
            return "As duas senhas não são iguais. Digite a mesma nos dois campos.";

        return null;
    }
}
