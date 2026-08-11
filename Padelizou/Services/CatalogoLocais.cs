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

    // ── QUAIS CLUBES PODEM SER ESCOLHIDOS ─────────────────────────────────────────────────
    //
    // A lista que aparece pra quem vai ESCOLHER um clube: no cadastro, no perfil, ao criar
    // torneio, ao publicar aviso, ao configurar grupo, ao publicar desafio, na busca de
    // jogadores e no painel. Traz a cidade junto, porque com muitos clubes o nome sozinho não
    // distingue duas "Arena Central" de cidades diferentes.
    //
    // ⚠️ EXISTE PRA SER O ÚNICO LUGAR. Antes disto, onze telas repetiam
    // `_context.Clubes.OrderBy(c => c.Nome)` — acrescentar a régua em dez e esquecer de uma
    // não quebra nada: o clube desligado simplesmente continua aparecendo naquela tela, calado,
    // e ninguém descobre até alguém escolher o clube errado. Regra de catálogo espalhada é a
    // mesma família de bug que a regra de torneio duplicada.
    //
    // NÃO usar em: /Admin/Clubes (a tela de gestão precisa ver os desligados pra religar), na
    // checagem de nome repetido (nome duplicado é duplicado mesmo desligado) e em consulta de
    // HISTÓRICO — o clube onde um torneio aconteceu não deixa de ser onde ele aconteceu.
    public static IQueryable<Clube> ParaEscolher(this IQueryable<Clube> clubes) =>
        clubes.Where(c => c.Selecionavel)
              .Include(c => c.Cidade)
              .OrderBy(c => c.Nome);

    // O nome como ele aparece numa lista de escolha: "Nata Padel · Porto Alegre/RS".
    // Clube sem cidade fica só com o nome — o passivo antigo não pode virar "Fulano · ".
    public static string RotuloComCidade(Clube clube) =>
        clube.Cidade == null ? clube.Nome : $"{clube.Nome} · {NomeDaCidade(clube.Cidade)}";

    // Só a cidade, pro agrupamento e pro filtro. Quem não tem cidade cai num grupo próprio, e
    // não some da lista: clube sem cidade é a maioria do passivo, e escondê-lo seria pior que
    // deixá-lo no fim.
    public const string SemCidade = "Sem cidade";

    public static string CidadeDoClube(Clube clube) =>
        clube.Cidade == null ? SemCidade : NomeDaCidade(clube.Cidade);

    private static string NomeDaCidade(Cidade cidade) =>
        string.IsNullOrWhiteSpace(cidade.Estado) ? cidade.Nome : $"{cidade.Nome}/{cidade.Estado}";

    // Os clubes agrupados por cidade, pro `<optgroup>` dos seletores. O navegador já sabe
    // desenhar grupo com título — num seletor de 200 clubes isso é a diferença entre rolar
    // procurando e ir direto na cidade certa, sem uma linha de JavaScript.
    //
    // "Sem cidade" vai por último de propósito: é o passivo dos clubes que nasceram antes de a
    // cidade ser perguntada, e ele encolhe sozinho (AcharOuCriarClubeAsync preenche o vazio
    // quando alguém informa a cidade daquele clube).
    public static IEnumerable<IGrouping<string, Clube>> AgrupadosPorCidade(IEnumerable<Clube> clubes) =>
        clubes
            .GroupBy(CidadeDoClube)
            .OrderBy(g => g.Key == SemCidade ? 1 : 0)
            .ThenBy(g => g.Key);

    // O rótulo de `<optgroup>` deste clube — o MESMO texto que `AgrupadosPorCidade` usa, pra
    // que o clube recém-criado caia no grupo certo do seletor em vez de ficar solto no fim.
    //
    // ⚠️ EXISTE PORQUE `CidadeDoClube` LÊ A NAVEGAÇÃO `clube.Cidade`, e quem acabou de criar o
    // clube tem só o `CidadeId`: `AcharOuCriarClubeAsync` grava o id e não carrega a navegação.
    // Não há lazy loading neste repo — ler `clube.Cidade` ali devolveria "Sem cidade" mesmo com
    // a cidade gravada, sem erro nenhum. Por isso aqui vai ao banco quando falta.
    public static async Task<string> RotuloDaCidadeAsync(DbPadelContext db, Clube clube)
    {
        if (clube.Cidade != null) return CidadeDoClube(clube);
        if (clube.CidadeId == null) return SemCidade;

        var cidade = await db.Cidades.FirstOrDefaultAsync(c => c.Id == clube.CidadeId);
        return cidade == null ? SemCidade : NomeDaCidade(cidade);
    }

    // As cidades presentes numa lista de clubes, na ordem em que devem aparecer num filtro.
    public static List<string> CidadesDaLista(IEnumerable<Clube> clubes) =>
        AgrupadosPorCidade(clubes).Select(g => g.Key).ToList();

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
    // `endereco` só chega pela porta do "cadastre um local novo" (ClubesController.Criar); as
    // outras duas não perguntam. Fica opcional pra que essa porta não perca o que já gravava.
    public static async Task<Clube?> AcharOuCriarClubeAsync(DbPadelContext db, string? nome,
        string? cidadeNome = null, string? estado = null, string? endereco = null)
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

        var clube = new Clube
        {
            Nome = nome,
            Endereco = endereco?.Trim() ?? "",
            Contato = "",
            CidadeId = cidade?.Id,
        };
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
