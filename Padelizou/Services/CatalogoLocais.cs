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
    //
    // A CIDADE entrou em 11/08/2026, e não é enfeite de cadastro: o clube é o único lugar que
    // sabe ONDE um torneio acontece (ver Services/UfDoTorneio), e sem ela o aviso de torneio
    // novo não tem como ser mirado. Conferido em produção: os 3 clubes que sediam torneio
    // real tinham `CidadeId` NULO, porque este método nunca perguntou.
    //
    // ⚠️ Continua OPCIONAL. Obrigar a cidade aqui travaria o cadastro de quem só quer marcar
    // onde jogou, e um campo a mais na primeira tela é onde se perde gente. Sem cidade, a
    // mira cai no plano B (o estado de quem organiza) — que é pior, mas não é silêncio.
    public static async Task<Clube?> AcharOuCriarClubeAsync(DbPadelContext db, string? nome,
        string? cidadeNome = null, string? estado = null)
    {
        nome = Normalizar(nome);
        if (nome == null) return null;

        var cidade = await AcharOuCriarCidadeAsync(db, cidadeNome, estado);

        var alvo = nome.ToLower();
        var existente = await db.Clubes.FirstOrDefaultAsync(c => c.Nome.ToLower() == alvo);
        if (existente != null)
        {
            // CONSERTA O QUE JÁ EXISTE: clube antigo sem cidade recebe a que acabou de ser
            // informada. É de graça e resolve sozinho o passivo dos clubes que nasceram antes
            // desta pergunta existir.
            //
            // ⚠️ Só PREENCHE VAZIO, nunca sobrescreve: quem digita o nome de um clube que já
            // existe não pode mudar o endereço dele pra todo mundo por engano de digitação.
            if (existente.CidadeId == null && cidade != null)
            {
                existente.CidadeId = cidade.Id;
                await db.SaveChangesAsync();
            }

            return existente;
        }

        var clube = new Clube { Nome = nome, Endereco = "", Contato = "", CidadeId = cidade?.Id };
        db.Clubes.Add(clube);
        await db.SaveChangesAsync();
        return clube;
    }

    public static async Task<Cidade?> AcharOuCriarCidadeAsync(DbPadelContext db, string? nome, string? estado)
    {
        nome = Normalizar(nome);
        if (nome == null) return null;
        nome = NomeDeCidade.Arrumar(nome);   // "Porto    Alegre" e "Porto Alegre" são a mesma linha

        // UF sempre em maiúsculas, pra não existir "rs" e "RS" na lista.
        estado = Normalizar(estado)?.ToUpperInvariant();
        if (estado is { Length: > 2 }) estado = estado[..2];

        // ⚠️ Compara por `NomeDeCidade.Chave` e não por `ToLower()`: minúscula sozinha resolve
        // "GRAVATAI" e deixa passar "Gravatai" — foi assim que grafia sem acento virou uma
        // SEGUNDA linha de catálogo ao lado da certa, com metade dos professores em cada uma.
        // A conta roda na memória porque `Chave` não vira SQL; Cidades é catálogo, tem dezenas
        // de linhas (mesma escolha já feita em AulasController.MinhasCidades).
        var chave = NomeDeCidade.Chave(nome);
        var existente = (await db.Cidades.ToListAsync()).FirstOrDefault(c =>
            NomeDeCidade.Chave(c.Nome) == chave && (estado == null || c.Estado == null || c.Estado == estado));
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
