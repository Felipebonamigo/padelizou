using Padelizou.Models;

namespace Padelizou.Services;

// A HORA DA VAGA CUJO JOGO JÁ NASCEU, NA PRÉVIA DA CHAVE (21/11/2026, Wallau).
//
// 🗣️ Rafael: *"Porque fica 'a definir' o horário?"* — Quartas 2 e 4 da 4ª Masculina, com os
// nomes já na tela, sem hora. Com grupo em aberto a aba Chaves desenha a PRÉVIA por colocação e
// tira a hora de cada vaga da projeção; a projeção só promete o que FALTA
// (RoboDoChaveamento.ProjetarProximasFasesAsync, `aberturaJaCriada`). O jogo que já nasceu pelo
// avanço parcial ficava sem hora em lugar nenhum do quadro — a hora dele está no jogo.
//
// Devolve, por (fase, número no quadro), um JogoQueVem com a hora, a quadra e o clube do jogo
// REAL — o formato que o quadro já sabe desenhar. O número é o do quadro
// (ReservasDeHorario.NumeroNaFase), não a ordem de criação: a Quartas 4 pode nascer antes da 2.
// `jogos` tem que ser a categoria inteira (ou a fase inteira), pela mesma razão.
public static class AberturaJaCriada
{
    public static Dictionary<(string Fase, int Numero), ProximasFasesDaChave.JogoQueVem> PorVaga(
        IEnumerable<Partida> jogos, string categoria)
    {
        var doMataMata = jogos.Where(p => ChaveamentoMataMata.EhFaseDeMataMata(p.Fase)).ToList();
        var numero = ReservasDeHorario.NumeroNaFase(doMataMata);
        var semLado = new ProximasFasesDaChave.Lado("");

        var porVaga = new Dictionary<(string, int), ProximasFasesDaChave.JogoQueVem>();
        foreach (var p in doMataMata)
        {
            if (!numero.TryGetValue(p.Id, out var n)) continue;
            porVaga[(p.Fase, n)] = new ProximasFasesDaChave.JogoQueVem(
                categoria, p.Fase, n, p.HorarioPrevisto, semLado, semLado,
                p.NomeQuadra, p.CategoriaId, ClubeId: p.ClubeId);
        }
        return porVaga;
    }
}
