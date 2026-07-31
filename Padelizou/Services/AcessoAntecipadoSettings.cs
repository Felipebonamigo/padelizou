namespace Padelizou.Services;

// Uma dupla usuário/senha que abre o portão.
public class CredencialDoPortao
{
    public string? Usuario { get; set; }
    public string? Senha { get; set; }
}

public class AcessoAntecipadoSettings
{
    public bool Habilitado { get; set; }

    // A credencial PRINCIPAL. Continua sozinha no seu lugar de sempre porque é dela que sai o
    // hash do cookie (ver AcessoAntecipadoMiddleware.CalcularHash): trocá-la derruba todo mundo
    // pra fora do portão de uma vez, que é justamente o que se quer quando a senha vaza.
    public string Usuario { get; set; } = null!;
    public string Senha { get; set; } = null!;

    // Credenciais ADICIONAIS, todas com o mesmo poder da principal. Existem pra dar entrada
    // separada a outra pessoa ou grupo sem reemitir a senha de todo mundo — e pra poder cortar
    // essa entrada depois sem expulsar quem entrou pela principal.
    //
    // Por variável de ambiente no systemd, o formato é indexado:
    //   AcessoAntecipado__Extras__0__Usuario=fulano
    //   AcessoAntecipado__Extras__0__Senha=segredo
    public List<CredencialDoPortao> Extras { get; set; } = new();

    // CPF do jogador em que o visitante entra logado automaticamente depois de acertar a
    // senha do gate — modo demonstração, pra mostrar o sistema cheio sem pedir cadastro.
    // **Vazio desliga**, e aí quem entra navega deslogado e cria a própria conta se quiser:
    // é o que a gente quer em dev, onde os testes são de gente de verdade.
    public string? LoginAutomaticoCpf { get; set; }

    // A dupla digitada bate com alguma das credenciais?
    //
    // O usuário do portão NÃO é segredo (o segredo é a senha) — e ele chega por WhatsApp,
    // digitado num celular que decide sozinho se capitaliza a primeira letra. Comparar com
    // caixa exigiria acertar "Corneteiros" e não "corneteiros", o que viraria chamado de
    // suporte na primeira noite. A senha continua exata.
    //
    // O Trim vale pros dois: copiar de uma mensagem quase sempre traz espaço colado, e recusar
    // por causa disso é recusar quem digitou certo.
    public bool Confere(string? usuario, string? senha)
    {
        var digitadoUsuario = usuario?.Trim() ?? "";
        var digitadaSenha = senha?.Trim() ?? "";

        // Credencial sem senha nunca abre nada: um Extras meio preenchido no systemd viraria
        // porta escancarada pra quem deixasse o campo em branco.
        return TodasAsCredenciais().Any(c =>
            !string.IsNullOrWhiteSpace(c.Senha)
            && string.Equals(digitadoUsuario, c.Usuario?.Trim(), StringComparison.OrdinalIgnoreCase)
            && digitadaSenha == c.Senha.Trim());
    }

    private IEnumerable<CredencialDoPortao> TodasAsCredenciais()
    {
        yield return new CredencialDoPortao { Usuario = Usuario, Senha = Senha };
        foreach (var extra in Extras) yield return extra;
    }
}
