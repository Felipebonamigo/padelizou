using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// JUNTAR DOIS TIMES QUE SÃO O MESMO TIME ESCRITO DE DOIS JEITOS.
//
// Existe porque a produção acumulou "ER Padel" (21 jogadores, 318 pontos) e "Er padel" (2
// jogadores, 25 pontos) como cadastros diferentes, e o pódio de times do torneio saía partido
// entre as duas linhas. A partir de 10/09/2026 o banco tem índice único em
// `lower(btrim("Nome"))` e esse par deixa de ser possível — mas a trava do banco RECUSA, e
// recusar sozinho deixaria a pessoa presa: ela digita o nome do próprio clube, o banco diz não,
// e ela não tem como chegar no time certo. Por isso a fusão.
//
// ⚠️ Atalho deliberado: dois salvamentos simultâneos com o mesmo nome novo passam os dois pela
// checagem em C# e o segundo bate no índice único — vira erro 500, não duplicata. Não há retry
// aqui de propósito: é um sistema de um operador só, e uma tela de erro num empate de
// milissegundos é melhor que a corrupção silenciosa que existia antes. Se um dia doer, o
// conserto é capturar DbUpdateException e reentrar por FundirAsync.
//
// ⚠️ ESTA CLASSE É A RÉGUA, e a migration `20260910...FusaoDeTimesComNomeIgual` carrega a MESMA
// régua em SQL. Não é regra duplicada por descuido: a migration é um retrato congelado do
// reparo de UMA base num dia, roda uma vez e nunca mais; esta classe é o comportamento vivo,
// que roda toda vez que alguém salva o perfil. O que não pode é divergirem no CRITÉRIO, e por
// isso ele está escrito uma vez só, aqui, e citado lá.
public static class FusaoDeTimes
{
    // Funde os dois times num só e devolve o que sobreviveu.
    //
    // ⚠️ Não chama SaveChanges — mesma regra do TransferenciasDeTime.Registrar: quem chama
    // decide quando gravar, e aqui isso importa porque a fusão acontece no meio de um salvar
    // de perfil que ainda tem outras coisas pra gravar na mesma transação.
    public static async Task<Time> FundirAsync(DbPadelContext context, int umId, int outroId)
    {
        if (umId == outroId) return (await context.Times.FindAsync(umId))!;

        // Sobrevive o de MAIS jogadores; empate, o de menor Id. O critério é o mesmo que o
        // projeto já usa pra grafias concorrentes de cidade ("entre duas grafias empatadas,
        // ganha a que mais gente escreveu") — a escrita da maioria é a que fica.
        var quantos = await context.Jogadores
            .Where(j => j.TimeId == umId || j.TimeId == outroId)
            .GroupBy(j => j.TimeId!.Value)
            .Select(g => new { TimeId = g.Key, Quantos = g.Count() })
            .ToDictionaryAsync(x => x.TimeId, x => x.Quantos);

        quantos.TryGetValue(umId, out var deUm);
        quantos.TryGetValue(outroId, out var deOutro);

        var (sobreviventeId, absorvidoId) = (deUm, deOutro) switch
        {
            _ when deUm > deOutro => (umId, outroId),
            _ when deOutro > deUm => (outroId, umId),
            _ => (Math.Min(umId, outroId), Math.Max(umId, outroId)),
        };

        // ⚠️ O nome do sobrevivente NÃO muda. A colisão é por `lower(btrim(nome))`, então o que
        // a pessoa digitou só pode diferir do que já está lá na caixa das letras ou em espaço
        // — e aplicar o que ela digitou deixaria alguém trocar a grafia de um time que não
        // administra, só por saber como ele se chama.
        var sobrevivente = (await context.Times.FindAsync(sobreviventeId))!;
        var absorvido = await context.Times.FindAsync(absorvidoId);
        if (absorvido == null) return sobrevivente;

        foreach (var jogador in await context.Jogadores.Where(j => j.TimeId == absorvidoId).ToListAsync())
        {
            // Sem TransferenciasDeTime.Registrar aqui: ninguém trocou de time. A pessoa
            // continua no mesmo clube — quem mudou foi o cadastro, e inventar "saiu do
            // Er padel e entrou no ER Padel" encheria a aba de movimento que não houve.
            jogador.TimeId = sobreviventeId;
        }

        // O vínculo da dupla com o time é só o escudo na tela, mas deixá-lo apontando pra um
        // Id que vai sumir apagaria o escudo de um torneio já encerrado.
        foreach (var dupla in await context.Duplas.Where(d => d.TimeId == absorvidoId).ToListAsync())
        {
            dupla.TimeId = sobreviventeId;
        }

        // A história de quem passou pelo time absorvido continua valendo, e passa a apontar
        // pro sobrevivente.
        var transferencias = await context.TransferenciasDeTime
            .Where(t => t.TimeAnteriorId == absorvidoId || t.TimeNovoId == absorvidoId)
            .ToListAsync();
        foreach (var t in transferencias)
        {
            if (t.TimeAnteriorId == absorvidoId) t.TimeAnteriorId = sobreviventeId;
            if (t.TimeNovoId == absorvidoId) t.TimeNovoId = sobreviventeId;
        }

        // ⚠️ Quem já tinha ido de uma grafia pra outra vira "saiu do ER Padel e entrou no
        // ER Padel" depois do repontamento — exatamente a linha que o Registrar se recusa a
        // criar por não querer dizer nada. Some.
        context.TransferenciasDeTime.RemoveRange(
            transferencias.Where(t => t.TimeAnteriorId == t.TimeNovoId));

        // Sedes: traz as que faltam e descarta as repetidas. TimeSede tem chave composta
        // (TimeId, ClubeId) — mover cego a sede que os dois já têm estoura a chave e derruba
        // a gravação inteira.
        var sedesQueJaTenho = await context.TimeSedes
            .Where(s => s.TimeId == sobreviventeId).Select(s => s.ClubeId).ToListAsync();
        foreach (var sede in await context.TimeSedes.Where(s => s.TimeId == absorvidoId).ToListAsync())
        {
            // A chave composta inclui o TimeId, então não dá pra só trocar o campo: sai e
            // entra de novo.
            context.TimeSedes.Remove(sede);
            if (!sedesQueJaTenho.Contains(sede.ClubeId))
            {
                context.TimeSedes.Add(new TimeSede { TimeId = sobreviventeId, ClubeId = sede.ClubeId });
            }
        }

        // ⚠️ ADMINISTRAÇÃO NÃO É HERDADA — e isto é a parte que mais importa aqui. Quem
        // comandava o time absorvido não passa a comandar o sobrevivente: a dona do "Er padel"
        // mandava em 2 pessoas, e herdar daria a ela o comando de 21. Reparo de dado não pode
        // virar promoção. É a mesma razão pela qual digitar no cadastro o nome de um time que
        // já existe não dá cargo nenhum (AuthController.DefinirTimeAsync).
        //
        // Quem administrava o SOBREVIVENTE continua administrando — essas linhas nem são
        // tocadas. Se o absorvido era o único com administrador, o time fica sem nenhum, que é
        // o mesmo estado dos 44 importados do ranking: um admin do Padelizou concede o
        // primeiro.
        context.TimeAdministradores.RemoveRange(
            await context.TimeAdministradores.Where(a => a.TimeId == absorvidoId).ToListAsync());

        context.Times.Remove(absorvido);
        return sobrevivente;
    }
}
