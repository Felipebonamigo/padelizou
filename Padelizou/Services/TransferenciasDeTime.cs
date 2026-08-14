using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A JANELA DE TRANSFERÊNCIAS: quem trocou de camisa, e o que a aba mostra disso.
//
// Duas responsabilidades, e as duas precisam morar juntas: quem GRAVA a troca e quem LÊ a
// lista. Se a gravação ficasse espalhada pelos controllers, cada tela que mexe em time
// lembraria (ou esqueceria) de registrar — e o buraco de uma que esquece é invisível: o
// jogador aparece no time novo normalmente, só o histórico é que fica com um movimento a
// menos, e ninguém relaciona uma coisa com a outra meses depois.
public static class TransferenciasDeTime
{
    // Quantos movimentos a aba mostra por padrão. "Recentes" é a promessa do título — uma
    // lista sem fim viraria arquivo histórico, que é outra tela.
    public const int QuantidadePadrao = 60;

    // O que a pessoa quer ver. Com um time escolhido, "entradas" são os reforços DELE e
    // "saídas" são quem o deixou. Sem time escolhido, valem pra qualquer time: entrada é
    // todo movimento que termina numa camisa, saída é todo movimento que larga uma.
    //
    // ⚠️ Uma transferência comum (do time A pro time B) é as DUAS coisas ao mesmo tempo, e é
    // assim que ela tem que aparecer nos dois filtros. Só a estreia (sem time antes) é
    // entrada pura, e só a aposentadoria da camisa (sem time depois) é saída pura.
    public enum Sentido { Todas, Entradas, Saidas }

    public static Sentido SentidoDe(string? texto) => texto?.ToLowerInvariant() switch
    {
        "entradas" => Sentido.Entradas,
        "saidas" or "saídas" => Sentido.Saidas,
        _ => Sentido.Todas,
    };

    // ── GRAVAR ────────────────────────────────────────────────────────────────────────

    // Muda o time do jogador E registra o movimento. Devolve false quando não houve troca —
    // salvar o perfil sem mexer no time não pode inventar uma transferência, senão a aba
    // encheria de "Fulano saiu do Nata e entrou no Nata".
    //
    // ⚠️ Não chama SaveChanges: quem chama decide quando gravar (mesma regra de
    // AdministracaoTime.Conceder, que aliás passa por aqui).
    public static bool Registrar(DbPadelContext context, Jogador jogador, int? novoTimeId)
    {
        if (jogador.TimeId == novoTimeId) return false;

        context.Set<TransferenciaDeTime>().Add(new TransferenciaDeTime
        {
            // A NAVEGAÇÃO, e não o Id: no cadastro o jogador ainda pode não ter Id (a linha
            // nasce no mesmo SaveChanges), e gravar `JogadorId = 0` estouraria a chave
            // estrangeira. Com a navegação, o EF preenche a coluna depois do insert.
            Jogador = jogador,
            TimeAnteriorId = jogador.TimeId,
            TimeNovoId = novoTimeId,
            Em = DateTime.Now,
        });

        jogador.TimeId = novoTimeId;
        return true;
    }

    // ── LER ───────────────────────────────────────────────────────────────────────────

    // Os movimentos recentes, já filtrados. `timeId` nulo = todos os times.
    public static async Task<List<TransferenciaDeTime>> RecentesAsync(
        DbPadelContext context,
        int? timeId = null,
        Sentido sentido = Sentido.Todas,
        string? busca = null,
        int quantidade = QuantidadePadrao)
    {
        var consulta = context.TransferenciasDeTime
            .AsNoTracking()
            .Include(t => t.Jogador)
            .Include(t => t.TimeAnterior)
            .Include(t => t.TimeNovo)
            .AsQueryable();

        if (timeId is int alvo)
        {
            consulta = sentido switch
            {
                Sentido.Entradas => consulta.Where(t => t.TimeNovoId == alvo),
                Sentido.Saidas => consulta.Where(t => t.TimeAnteriorId == alvo),
                // "Todas" com time escolhido é tudo que passou por aquele vestiário, nas duas
                // direções — é o que alguém quer ver ao abrir a página do próprio time.
                _ => consulta.Where(t => t.TimeNovoId == alvo || t.TimeAnteriorId == alvo),
            };
        }
        else
        {
            consulta = sentido switch
            {
                Sentido.Entradas => consulta.Where(t => t.TimeNovoId != null),
                Sentido.Saidas => consulta.Where(t => t.TimeAnteriorId != null),
                _ => consulta,
            };
        }

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // A MESMA busca do resto do sistema (nome, apelido ou CPF inteiro) — e é ela que
            // já tira de vista quem pediu exclusão de conta pela LGPD. Reescrever um
            // `Contains` aqui seria a segunda régua de "achar jogador" e a primeira a
            // esquecer desse filtro.
            var achados = BuscaJogador.Filtrar(context.Jogadores.AsNoTracking(), busca).Select(j => j.Id);
            consulta = consulta.Where(t => achados.Contains(t.JogadorId));
        }

        return await consulta
            .OrderByDescending(t => t.Em)
            .ThenByDescending(t => t.Id)   // mesma data/hora: a linha mais nova primeiro
            .Take(quantidade)
            .ToListAsync();
    }

    // O QUE ACONTECEU, sem o nome de quem: "saiu do Nata e foi pro Batata".
    //
    // Sem o nome de propósito — a tela põe o jogador num link pro perfil dele e emenda esta
    // frase depois. Com o nome embutido, a view teria que recortá-lo de volta pra conseguir
    // linká-lo, e recorte de texto quebra no primeiro nome vazio ou repetido.
    //
    // Fica aqui (e não no .cshtml) porque é a mesma frase em dois lugares — a aba geral e a
    // página do time — e porque uma frase é testável; um `@if` aninhado numa view não é.
    public static string Movimento(TransferenciaDeTime t)
    {
        var de = t.TimeAnterior?.Nome;
        var para = t.TimeNovo?.Nome;

        // Time apagado do cadastro depois do movimento: a linha sobrevive com o lado em
        // branco (SetNull), e dizer "saiu de (nada)" seria pior do que admitir a lacuna.
        if (t.TimeAnteriorId != null && de == null) de = "um time removido";
        if (t.TimeNovoId != null && para == null) para = "um time removido";

        return (de, para) switch
        {
            (null, not null) => $"vestiu a camisa do {para}",
            (not null, null) => $"deixou o {de}",
            (not null, not null) => $"saiu do {de} e foi pro {para}",
            // Não deveria existir (Registrar recusa troca de nada pra nada), mas uma linha
            // torta no banco não pode virar texto em branco na tela.
            _ => "mudou de time",
        };
    }

    // A frase inteira, com o nome na frente. Pro que não tem como linkar o perfil (um resumo,
    // um aviso), e é o mesmo texto — as duas saídas não podem divergir.
    public static string Frase(TransferenciaDeTime t) =>
        $"{t.Jogador?.ComoChamar ?? "Jogador"} {Movimento(t)}";
}
