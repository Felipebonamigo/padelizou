using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// QUEM TEM O JOGO MUDADO DE HORÁRIO FICA SABENDO (13/11/2026, item 4 de PROXIMOS-PASSOS.md).
//
// 🕳️ Até aqui, NENHUMA remarcação avisava ninguém: a troca ⇄ e as setas, a hora digitada na mão,
// o "adicionar horário" e o "aplicar planejamento" com a chave já pública. O jogador se programa
// pelo horário que viu quando as chaves saíram — horário mudado calado é W.O. na certa.
//
// 🔑 O CAMINHO:
//   1. A ação lê o que vai mudar ANTES do SaveChanges (`Capturar`): o horário de antes é o do
//      banco, e o ChangeTracker o esquece ao gravar. Anota DEPOIS de gravar — gravação que
//      estourou não mudou a hora de ninguém (TorneiosController.AnotarHorariosMudados).
//   2. Espera a grade sossegar (AvisosDeHorarioPendentes: 3 minutos sem mexer no torneio).
//   3. Aqui: confere de novo NO BANCO e manda um aviso por PESSOA, com todos os jogos dela.
//
// ✋ NÃO AVISA: chave em aprovação (o jogador não vê hora nenhuma); jogo que já começou, acabou ou
// saiu da grade; jogo que voltou pro horário de antes; dupla de TIME (a mesma régua do "Chaves
// saíram": o Jogador1Id dela é o organizador).
//
// ⚠️ BURACOS CONHECIDOS (revisão do #422, em PROXIMOS-PASSOS.md, item 4):
//   - O ROBÔ também remarca jogo que já existe (RoboDoChaveamento.AgendarNaGradeAsync reencaixa o
//     jogo "fora de ordem" quando outra categoria avança) e não passa por aqui.
//   - Chave RECOLHIDA depois de pública volta pra "em aprovação": remarcar ali e republicar não
//     avisa (o jogador já tinha visto a hora). Fica com a caixinha "avisar" da re-aprovação.
//   - Troca entre CLUBES muda o lugar junto; o texto só fala da hora.
//
// 📵 App + e-mail, sem WhatsApp — o mesmo canal do "Chaves saíram", pelo mesmo motivo: rajada de
// texto quase igual pra muita gente é a assinatura de spam que a Meta lê.
// atalho: um e-mail por pessoa, sem orçamento. Teto: TetoDoEmail.PorDia (100) — um Aplicar
// planejamento num torneio de 64 duplas manda ~128 e fecha o dia sozinho, como o "Chaves saíram".
// Saída: pesar no VolumeDoEmail e cair pra AppSemEmail acima de um limite (decisão do Felipe).
//
// 📍 O lugar não vai no texto: num torneio em dois clubes a quadra só se lê com a sede
// (LugarDoJogo), e o link leva pra tela que já sabe fazer isso.
public class AvisoDeHorarioMudado
{
    public sealed record Mudanca(int PartidaId, DateTime? Antes, DateTime Depois);

    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AvisoDeHorarioMudado(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    // Os jogos AGENDADOS deste torneio cuja hora vai mudar no próximo SaveChanges, com a hora do
    // banco. Jogo novo (Added) não é mudança: quem nunca teve hora não perdeu nenhuma. Jogo que
    // fica sem hora também não entra — é o organizador tirando da grade, não remarcando.
    public static List<Mudanca> Capturar(DbPadelContext context, int torneioId)
    {
        var mudancas = new List<Mudanca>();
        foreach (var entrada in context.ChangeTracker.Entries<Partida>())
        {
            if (entrada.State != EntityState.Modified) continue;
            var jogo = entrada.Entity;
            if (jogo.TorneioId != torneioId || jogo.Status != "Agendada") continue;

            var antes = entrada.Property(p => p.HorarioPrevisto).OriginalValue;
            if (jogo.HorarioPrevisto is not DateTime depois || antes == depois) continue;

            mudancas.Add(new Mudanca(jogo.Id, antes, depois));
        }
        return mudancas;
    }

    // Devolve quantas pessoas foram avisadas.
    public async Task<int> EnviarAsync(IReadOnlyCollection<AvisosDeHorarioPendentes.Vencido> vencidos)
    {
        int avisados = 0;
        foreach (var doTorneio in vencidos.GroupBy(v => v.TorneioId))
        {
            var torneio = await _context.Torneios.AsNoTracking().FirstOrDefaultAsync(t => t.Id == doTorneio.Key);
            if (torneio == null || !AprovacaoDeChaves.ChavePublicada(torneio)) continue;

            var antesPorJogo = doTorneio.GroupBy(v => v.PartidaId).ToDictionary(g => g.Key, g => g.First().Antes);
            var ids = antesPorJogo.Keys.ToList();
            var jogos = await _context.Partidas.AsNoTracking()
                .Where(p => ids.Contains(p.Id) && p.TorneioId == torneio.Id && p.Status == "Agendada")
                .Select(p => new { p.Id, p.HorarioPrevisto, Categoria = p.Categoria.Nome, p.Dupla1Id, p.Dupla2Id })
                .ToListAsync();

            var mudaram = new List<(DateTime? Antes, DateTime Depois, string Categoria, int[] Duplas)>();
            foreach (var jogo in jogos)
            {
                var antes = antesPorJogo[jogo.Id];
                if (jogo.HorarioPrevisto is not DateTime depois || depois == antes) continue;
                mudaram.Add((antes, depois, jogo.Categoria, new[] { jogo.Dupla1Id, jogo.Dupla2Id }));
            }
            if (mudaram.Count == 0) continue;

            var duplaIds = mudaram.SelectMany(m => m.Duplas).Distinct().ToList();
            var jogadoresPorDupla = await _context.Duplas.AsNoTracking()
                .Where(d => duplaIds.Contains(d.Id) && d.NomeTime == null)
                .Select(d => new { d.Id, d.Jogador1Id, d.Jogador2Id })
                .ToDictionaryAsync(d => d.Id, d => new[] { (int?)d.Jogador1Id, d.Jogador2Id }.OfType<int>().ToArray());

            var porJogador = new Dictionary<int, List<(DateTime? Antes, DateTime Depois, string Categoria)>>();
            foreach (var m in mudaram)
            {
                var jogadores = m.Duplas.SelectMany(d => jogadoresPorDupla.GetValueOrDefault(d) ?? Array.Empty<int>()).Distinct();
                foreach (var jogadorId in jogadores)
                {
                    if (!porJogador.TryGetValue(jogadorId, out var dele)) porJogador[jogadorId] = dele = new();
                    dele.Add((m.Antes, m.Depois, m.Categoria));
                }
            }

            var url = $"/Torneios/Jogos/{torneio.Id}";
            foreach (var (jogadorId, dele) in porJogador)
            {
                var (titulo, corpo) = Texto(torneio.Nome, dele.OrderBy(j => j.Depois).ToList());
                await _push.EnviarParaJogadorAsync(jogadorId, titulo, corpo, url, AlcanceDoAviso.SoApp);
                avisados++;
            }
        }
        return avisados;
    }

    // "12/11 às 18:50", como o "Chaves saíram" (AvisosDoDiaDeJogo.CorpoDasChaves): sem dia da
    // semana, que dependeria da cultura da thread — e o relógio roda fora de requisição.
    private static string Quando(DateTime h) => $"{h:dd/MM} às {h:HH:mm}";

    private static (string Titulo, string Corpo) Texto(string torneio,
        List<(DateTime? Antes, DateTime Depois, string Categoria)> jogos)
    {
        static string Mudou((DateTime? Antes, DateTime Depois, string Categoria) j) => j.Antes is DateTime antes
            ? $"passou de {Quando(antes)} para {Quando(j.Depois)}"
            : $"agora é {Quando(j.Depois)}";

        if (jogos.Count == 1)
        {
            var jogo = jogos[0];
            return ("Seu jogo mudou de horário", $"{torneio} · {jogo.Categoria}: seu jogo {Mudou(jogo)}.");
        }
        return ("Seus jogos mudaram de horário",
            $"{torneio}: " + string.Join("; ", jogos.Select(j => $"{j.Categoria}, {Mudou(j)}")) + ".");
    }
}
