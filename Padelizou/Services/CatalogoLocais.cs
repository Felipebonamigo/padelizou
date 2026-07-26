using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// Clube, cidade e time nascem do próprio cadastro: no começo não existe nenhum, e obrigar
// alguém a esperar um administrador cadastrar sua cidade é o jeito mais rápido de perder
// a pessoa na primeira tela.
//
// Criar acontece **dentro** do POST de cadastro/preferências, junto com uma conta de verdade
// — não por um endpoint aberto que qualquer visitante pode marretar pra encher a base.
public static class CatalogoLocais
{
    public const int TamanhoMaximoNome = 80;

    // Procura pelo nome sem diferenciar maiúsculas (LIKE do PostgreSQL diferencia) e só
    // cria se não achar — senão "Nata Padel" e "nata padel" viram dois clubes.
    public static async Task<Clube?> AcharOuCriarClubeAsync(DbPadelContext db, string? nome)
    {
        nome = Normalizar(nome);
        if (nome == null) return null;

        var alvo = nome.ToLower();
        var existente = await db.Clubes.FirstOrDefaultAsync(c => c.Nome.ToLower() == alvo);
        if (existente != null) return existente;

        var clube = new Clube { Nome = nome, Endereco = "", Contato = "" };
        db.Clubes.Add(clube);
        await db.SaveChangesAsync();
        return clube;
    }

    public static async Task<Cidade?> AcharOuCriarCidadeAsync(DbPadelContext db, string? nome, string? estado)
    {
        nome = Normalizar(nome);
        if (nome == null) return null;

        // UF sempre em maiúsculas, pra não existir "rs" e "RS" na lista.
        estado = Normalizar(estado)?.ToUpperInvariant();
        if (estado is { Length: > 2 }) estado = estado[..2];

        var alvo = nome.ToLower();
        var existente = await db.Cidades.FirstOrDefaultAsync(c =>
            c.Nome.ToLower() == alvo && (estado == null || c.Estado == null || c.Estado == estado));
        if (existente != null) return existente;

        var cidade = new Cidade { Nome = nome, Estado = estado };
        db.Cidades.Add(cidade);
        await db.SaveChangesAsync();
        return cidade;
    }

    private static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        valor = valor.Trim();
        return valor.Length > TamanhoMaximoNome ? valor[..TamanhoMaximoNome] : valor;
    }
}
