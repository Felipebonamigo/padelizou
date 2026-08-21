using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// E-mail, CPF e login identificam UMA pessoa cada um.
//
// E-mail e login NÃO são espaços separados: quem entra digita um ou outro, e
// BuscaJogador.PorIdentificadorAsync casa os dois na mesma consulta, com FirstOrDefault.
// Deixar o login de alguém ser igual ao e-mail de outro torna essa busca ambígua — o
// banco escolhe sozinho qual linha responde, e o dono legítimo fica sem entrar (a senha
// confere contra a outra conta) e sem recuperar a senha (o link vai pro e-mail da outra
// conta). Por isso todo teste de unicidade aqui compara contra os DOIS campos.
//
// Existe porque a regra estava pela metade: o cadastro checava login contra login, e a
// edição de perfil gravava e-mail sem checar nada.
public static class IdentidadeJogador
{
    // Maiúscula não distingue ninguém: "Bona", "bona" e "bOnA" são a mesma pessoa, e a
    // entrada já trata assim. Guardar sem normalizar e comparar normalizado é o combinado.
    public static string Normalizar(string? valor) => (valor ?? "").Trim().ToLower();

    // Já existe OUTRA conta atendendo por este identificador, seja como e-mail ou como login?
    // `exceto` é a própria conta — na edição de perfil, e na reivindicação de um CPF de
    // pré-cadastro, onde o e-mail já gravado não pode barrar o próprio dono.
    public static async Task<bool> EmUsoAsync(DbPadelContext context, string? identificador, int? exceto = null)
    {
        var alvo = Normalizar(identificador);
        if (alvo.Length == 0) return false;

        return await context.Jogadores.AnyAsync(j =>
            (exceto == null || j.Id != exceto.Value) &&
            ((j.Email != null && j.Email.ToLower() == alvo) ||
             (j.Login != null && j.Login.ToLower() == alvo)));
    }

    // Mínimo definido pelo Felipe em 29/07/2026. Login curto demais vira sigla ambígua num
    // espaço de nomes que ele divide com os e-mails, e é o identificador que organizadores
    // digitam pra te achar — "jo" acharia o João errado com muita facilidade.
    public const int TamanhoMinimoDoLogin = 4;

    // Devolve a mensagem de recusa, ou null se o login serve. O chamador decide onde mostrar.
    public static string? ValidarLogin(string? login)
    {
        var texto = (login ?? "").Trim();

        if (texto.Length < TamanhoMinimoDoLogin)
            return $"O login precisa ter pelo menos {TamanhoMinimoDoLogin} caracteres.";

        // O máximo não é gosto: a coluna é `character varying(30)` e o Postgres RECUSA o que
        // passa disso — não corta. Sem esta linha, "joaovictordossantosoliveirajunior" (33)
        // derrubava o cadastro com erro 500 e a pessoa perdia tudo o que tinha digitado.
        return LimitesDeTexto.Problema(texto, LimitesDeTexto.LoginDeJogador, "O login");
    }

    // O crachá que vai no cookie: quem é a pessoa e o que ela pode ver. A navbar lê daqui, não
    // do banco, então o que faltar neste conjunto some da tela (menu de professor, painel admin).
    //
    // Existe num lugar só porque existia em QUATRO — três cópias iguais no AuthController
    // (entrar, salvar perfil, cadastrar) e uma no middleware de acesso antecipado, mantidas em
    // sincronia por um comentário pedindo que ficassem iguais. Não ficaram: só a do middleware
    // tratava e-mail nulo, e as outras três estouravam no `new Claim` — ou seja, **quem não tem
    // e-mail não conseguia entrar**, e o erro caía na tela de login. Pré-cadastro (jogador
    // inscrito pelo organizador) nasce exatamente assim, sem e-mail.
    public static List<Claim> ClaimsDe(Jogador jogador)
    {
        return new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, jogador.Id.ToString()),
            new Claim(ClaimTypes.Name, jogador.Nome),
            // `?? ""` em tudo que é anulável no modelo: `new Claim(tipo, null)` lança exceção.
            new Claim(ClaimTypes.Email, jogador.Email ?? ""),
            new Claim("FotoPerfil", jogador.FotoPerfil ?? ""),
            // O carimbo da sessão viaja DENTRO do cookie pra poder ser conferido contra a conta
            // a cada request (ver SessoesAbertas). É o que torna possível derrubar um login já
            // aberto: sem ele, o cookie é auto-suficiente e ninguém consegue expulsar ninguém.
            new Claim(SessoesAbertas.TipoDaClaim, jogador.CarimboDeSessao ?? ""),
            new Claim("IsProfessor", jogador.IsProfessor ? "true" : "false"),
            // ⚠️ `IsAdmin` é a credencial de EDITAR, e o assistente NÃO entra nela — é o que
            // impede um caminho de escrita de se abrir por acidente. Ver PoderesNoSistema.
            new Claim("IsAdmin", PoderesNoSistema.PodeEditarTudo(jogador) ? "true" : "false"),
            new Claim("IsAdminRaiz", jogador.IsAdminRaiz ? "true" : "false"),
            // Credencial separada, só de leitura: destrava o painel e a gestão do torneio em
            // modo "olhar", e é ela que acende a faixa de só-leitura na tela.
            new Claim("Assistente", jogador.IsAssistente ? "true" : "false"),
            // Parceiro do Ranking: destrava UMA tela do painel e mais nada. Separada das
            // outras três pelo mesmo motivo — credencial que abre pouco não pode ser
            // confundida com credencial que abre tudo.
            new Claim("ParceiroRanking", jogador.IsParceiroRanking ? "true" : "false")
        };
    }
}

// O CPF não tem método aqui de propósito: todo caminho que cria jogador já procura por CPF
// antes de inserir (cadastro, inscrição de dupla, troca de parceiro, seed), então a regra
// no código já existe. O que faltava era a garantia embaixo — índice único no banco, junto
// com os de e-mail e login, na migração UnicidadeDeIdentificadores.
