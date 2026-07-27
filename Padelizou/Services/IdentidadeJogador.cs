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
}

// O CPF não tem método aqui de propósito: todo caminho que cria jogador já procura por CPF
// antes de inserir (cadastro, inscrição de dupla, troca de parceiro, seed), então a regra
// no código já existe. O que faltava era a garantia embaixo — índice único no banco, junto
// com os de e-mail e login, na migração UnicidadeDeIdentificadores.
