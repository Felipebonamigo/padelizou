using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Services;

namespace Padelizou.Controllers
{
    // ADICIONAR UM HORÁRIO NA GRADE SEM MEXER NO CHAVEAMENTO (11/11/2026).
    //
    // 🗣️ Felipe: *"permita aqui adicionar um horário em algum dia, se eu quiser, sem alterar o
    // chaveamento — adicionar um horário no meio, no final, e os demais se adaptarem"*. Escolhas dele:
    //   • `modo` "meio": todo jogo AGENDADO a partir de `hora` naquele dia anda um horário pra frente
    //     (mesmos confrontos, mesma ordem); "fim": o dia ganha um horário no fim e ninguém anda.
    //   • `seNaoCouber` "esticar": o dia ganha horário no fim pra caber quem passou do limite;
    //     "proximoDia": quem passou abre o dia seguinte, e os dias seguintes também andam.
    //
    // É mudança NA MÃO do organizador — a exceção que ele mesmo abriu pra "depois de aprovada a grade
    // não muda" (HorarioDoChaveamentoNaoMudaAteOFimTests). A promessa do mata-mata (ReservaDeHorario)
    // anda junto, senão o jogo nasceria no horário velho. Nunca passa do último dia do torneio: aí
    // recusa e não muda nada. (InserirHorarioNaGradeTests)
    public partial class TorneiosController
    {
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InserirHorario(int id, DateTime dia, TimeSpan? hora, string? modo,
            string? seNaoCouber, string? voltarPara = null, string? filtros = null)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var cal = HorarioDoDia.Do(torneio);
            var duracao = VagasDaGrade.Duracao(torneio);
            dia = dia.Date;

            if (modo != "meio")
            {
                var novoLimite = cal.Limite(dia).Add(TimeSpan.FromMinutes(duracao));
                // A faixa do dia vive DENTRO do dia (HorarioDoDia.Escrever): passar da meia-noite seria
                // jogo de madrugada no dia seguinte, fora de qualquer faixa. Recusa sem mudar nada.
                if (novoLimite >= TimeSpan.FromDays(1))
                {
                    TempData["Erro"] = $"O último jogo de {dia:dd/MM} já começa às {cal.Limite(dia):hh\\:mm}: mais um horário "
                        + "no fim passaria da meia-noite. Abra o espaço no meio, com \"empurrar pro dia seguinte\".";
                    return VoltarPara(voltarPara, id, filtros);
                }

                EsticarODia(torneio, cal, dia, novoLimite);
                await _context.SaveChangesAsync();
                TempData["Sucesso"] = $"{dia:dd/MM} ganhou mais um horário no fim: o último jogo pode começar até "
                    + $"{novoLimite:hh\\:mm}. Nenhum jogo mudou.";
                return VoltarPara(voltarPara, id, filtros);
            }

            if (hora is not TimeSpan horaDoCorte)
            {
                TempData["Erro"] = "Diga a partir de que horário abrir o espaço.";
                return VoltarPara(voltarPara, id, filtros);
            }

            var corte = dia.Add(horaDoCorte);
            bool proximoDia = seNaoCouber == "proximoDia";
            // Esticando, só o próprio dia anda; empurrando, o resto do torneio anda junto.
            bool Anda(DateTime h) => h >= corte && (proximoDia || h.Date == dia);

            var jogos = await _context.Partidas.Where(p => p.TorneioId == id && p.HorarioPrevisto != null).ToListAsync();
            var reservas = await ReservasDeHorario.DoTorneio(_context, id).ToListAsync();

            // Jogo em quadra ou acabado não anda — e um agendado empurrado pra cima dele cairia no
            // mesmo horário. Melhor recusar que encavalar.
            if (jogos.Any(p => p.Status != "Agendada" && Anda(p.HorarioPrevisto!.Value)))
            {
                TempData["Erro"] = "Já tem jogo em quadra ou encerrado a partir desse horário — escolha um horário depois dele.";
                return VoltarPara(voltarPara, id, filtros);
            }

            DateTime NovoHorario(DateTime h) => proximoDia ? cal.DepoisDe(h, duracao) : h.AddMinutes(duracao);

            var agendados = jogos.Where(p => p.Status == "Agendada" && Anda(p.HorarioPrevisto!.Value)).ToList();
            var promessas = reservas.Where(r => Anda(r.Horario)).ToList();
            var novos = agendados.Select(p => NovoHorario(p.HorarioPrevisto!.Value))
                .Concat(promessas.Select(r => NovoHorario(r.Horario))).ToList();

            if (novos.Count == 0)
            {
                TempData["Erro"] = $"Não há jogo agendado a partir de {corte:dd/MM HH:mm} pra empurrar.";
                return VoltarPara(voltarPara, id, filtros);
            }

            // Esticando, o dia só cresce até a meia-noite: o jogo empurrado pra 00:20 cairia no dia
            // seguinte, fora de qualquer faixa (e, no último dia, fora do torneio).
            var depoisDaMeiaNoite = novos.Where(h => h.Date > dia).ToList();
            if (!proximoDia && depoisDaMeiaNoite.Count > 0)
            {
                TempData["Erro"] = $"Esticando, um jogo de {dia:dd/MM} passaria da meia-noite "
                    + $"(iria pra {depoisDaMeiaNoite.Min():dd/MM HH:mm}). "
                    + "Escolha \"empurrar pro dia seguinte\" ou abra o espaço mais cedo.";
                return VoltarPara(voltarPara, id, filtros);
            }

            // Nunca depois do último dia (GradeNuncaPassaDoFimDoTorneioTests). Esticando, o teto é o
            // próprio dia esticado — o dia seguinte não é tocado.
            if (proximoDia && cal.UltimoInicio is DateTime fim && novos.Any(h => h > fim))
            {
                TempData["Erro"] = $"Empurrando, o último jogo passaria do fim do torneio ({fim:dd/MM HH:mm}). "
                    + "Escolha \"esticar o dia\" ou adicione uma quadra.";
                return VoltarPara(voltarPara, id, filtros);
            }

            foreach (var jogo in agendados) jogo.HorarioPrevisto = NovoHorario(jogo.HorarioPrevisto!.Value);
            foreach (var reserva in promessas) reserva.Horario = NovoHorario(reserva.Horario);

            if (!proximoDia)
            {
                var ultimoDoDia = novos.Where(h => h.Date == dia).DefaultIfEmpty(corte).Max().TimeOfDay;
                if (ultimoDoDia > cal.Limite(dia)) EsticarODia(torneio, cal, dia, ultimoDoDia);
            }

            var horariosMudados = AvisoDeHorarioMudado.Capturar(_context, id);
            await _context.SaveChangesAsync();
            AnotarHorariosMudados(torneio, horariosMudados);
            TempData["Sucesso"] = $"Horário aberto em {corte:dd/MM HH:mm}: {agendados.Count + promessas.Count} "
                + "jogo(s) andaram um horário. Os confrontos não mudaram.";
            return VoltarPara(voltarPara, id, filtros);
        }

        // Grava a faixa do dia com o último início novo (Torneio.HorariosPorDia), a abertura de sempre.
        private static void EsticarODia(Models.Torneio torneio, HorarioDoDia.Calendario cal, DateTime dia, TimeSpan novoLimite)
        {
            var faixas = HorarioDoDia.Ler(torneio.HorariosPorDia).ToDictionary(f => f.Key, f => f.Value);
            faixas[dia] = new HorarioDoDia.Faixa(cal.Abre(dia), novoLimite);
            torneio.HorariosPorDia = HorarioDoDia.Escrever(faixas);
        }
    }
}
