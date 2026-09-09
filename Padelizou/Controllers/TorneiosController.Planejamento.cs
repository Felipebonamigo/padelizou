using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Controllers
{
    // PLANEJAMENTO DE QUADRAS — a tela onde o organizador decide quanta quadra alugar.
    //
    // 🗣️ Pedido do Felipe (09/09/2026), pelo Er: "eles só têm 2 quadras (…) querem uma aba que
    // possam ver quantos horários teriam que locar de quadra, extra, para fechar os jogos da
    // chave (…) que ele possa também alterar o horário de início (…) uma previsão de quantos
    // jogos precisaria colocar lá na sexta, e no sábado".
    //
    // ── TELA À PARTE, ABA NA `Details` ────────────────────────────────────────────────────
    // O `Details` é a página mais pesada do site, e um planejador existe pra ser GIRADO: mudar
    // a hora, olhar, mudar de novo. Recarregar o torneio inteiro a cada volta do botão seria
    // trocar a resposta rápida por uma tela que trava no celular do organizador. Então a aba
    // do `Details` mostra o RESUMO (cabe? falta quadra?) e traz pra cá, que é uma tela leve —
    // mesmo caminho de Financeiro, Relatório e Mesa de Controle.
    //
    // ⚠️ E O CÁLCULO CONTINUA NO SERVIDOR, inteiro: a tela só desenha (Services/
    // PlanejamentoDeQuadras). A tela de criação já tentou o contrário — as contas em
    // JavaScript, fora do alcance da suíte — e a lição está escrita no topo de
    // Services/PrevisaoDoTorneio.
    public partial class TorneiosController
    {
        // Teto do campo "jogos" da tela. O valor vem da URL e alimenta uma multiplicação
        // (`MinutosExtras`); sem teto, um número absurdo digitado à mão vira estouro de int.
        // 2.000 jogos é ~20× o maior torneio que já rodou aqui.
        private const int MaximoDeJogosNoPlanejamento = 2000;

        // O alfabeto de nomes de quadra do `Editar` tem 26 letras, e é ele que dá teto real ao
        // que o organizador consegue cadastrar. Aqui vai um pouco além pra não travar a
        // simulação, mas longe do absurdo.
        private const int MaximoDeQuadrasNoPlanejamento = 40;
        private const int MinimoDeMinutosPorJogo = 5;
        private const int MaximoDeMinutosPorJogo = 240;

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Planejamento(
            int id, DateTime? dataInicio = null, TimeSpan? horaInicio = null,
            TimeSpan? horaSeguintes = null, TimeSpan? horaFim = null,
            int? quadras = null, int? duracao = null, int? jogos = null,
            DateTime? ate = null, string? limites = null,
            Dictionary<string, string>? limite = null)
        {
            var jogadorId = ObterJogadorIdLogado() ?? 0;

            // A MESMA régua da aba "Gerenciar Torneio": organizador, criador, admin do sistema
            // e o assistente (que entra em leitura). O pedido do Felipe — "apenas o organizador
            // e criador e nós do sistema" — é exatamente esta lista, então nada de papel novo.
            if (!await PodeOlharAGestaoAsync(id, jogadorId)) return Forbid();

            var torneio = await _context.Torneios
                .Include(t => t.Categorias)
                    .ThenInclude(c => c.Duplas)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (torneio == null) return NotFound();

            var (reais, sorteados) = await JogosDoTorneioAsync(torneio);

            bool simulando = jogos is int pedido && pedido > 0 && pedido != reais;
            int total = simulando
                ? Math.Min(jogos!.Value, MaximoDeJogosNoPlanejamento)
                : reais;

            // A tabela da tela manda uma caixinha de hora POR DIA (`limite[2026-09-13]=14:00`);
            // o `limites` de uma string só é a forma que atravessa link compartilhado. As duas
            // viram o mesmo texto antes de chegar no serviço, que é quem sabe lê-lo — assim não
            // existem dois parsers pra mesma coisa.
            var limitesTexto = limite is { Count: > 0 }
                ? string.Join(';', limite
                    .Where(par => !string.IsNullOrWhiteSpace(par.Value))
                    .Select(par => $"{par.Key}={par.Value}"))
                : limites;

            var botoes = Botoes(torneio, dataInicio, horaInicio, horaSeguintes, horaFim,
                quadras, duracao, ate, limitesTexto);
            var plano = MontarPlano(botoes, total);

            return View(new PlanejamentoDeQuadrasVM
            {
                Torneio = torneio,
                Plano = plano,
                JogosDoTorneio = reais,
                JogosJaSorteados = sorteados,
                Simulando = simulando,
                SoLeitura = !await EhOrganizadorAsync(id, jogadorId),
                // ⚠️ SÓ PELO QUE O "APLICAR" DE FATO GRAVA. Quadras e hora-de-fechar-por-dia
                // são botões de SIMULAÇÃO (a primeira precisa de nome e clube, a segunda não
                // tem coluna) — acender o botão por causa delas faria o organizador apertar
                // "Aplicar" achando que acabou de contratar a terceira quadra.
                MudouAlgo = Gravavel(botoes) != Gravavel(Botoes(torneio)),
                DataInicio = botoes.DataInicio,
                HoraInicio = botoes.Abre,
                HoraSeguintes = botoes.AbreSeguintes,
                HoraFim = botoes.Fecha,
                Quadras = botoes.Quadras,
                Duracao = botoes.Duracao,
                Jogos = total,
                Ate = botoes.Ate,
                Limites = botoes.Limites,
            });
        }

        // ── O ÚNICO CAMINHO DAQUI QUE GRAVA ───────────────────────────────────────────────
        //
        // ⚠️ O NÚMERO DE QUADRAS NÃO SE APLICA POR AQUI, de propósito. Quadra tem NOME (é
        // identidade — ver Models/Quadra) e pode ficar em outro clube, com janela de horário
        // própria quando é alugada. Criar quadra sem nome daqui repetiria a reconciliação que
        // o `Editar` já faz, e uma segunda cópia dessa receita é exatamente o que
        // Services/VagasDaGrade nasceu pra matar. O planejamento DIZ quantas faltam; quem as
        // cadastra é a tela que sabe nomeá-las, e a tela manda o organizador pra lá.
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AplicarPlanejamento(
            int id, DateTime? dataInicio = null, TimeSpan? horaInicio = null,
            TimeSpan? horaSeguintes = null, TimeSpan? horaFim = null, int? duracao = null)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var abre = horaInicio ?? torneio.HoraInicioDoDia;
            var abreSeguintes = horaSeguintes ?? torneio.HoraInicioDiasSeguintes;
            var fecha = horaFim ?? torneio.HoraFimDoDia;

            // ⚠️ FRONTEIRA DE CONFIANÇA — e por isso fora da escada de "quanto código o pedido
            // merece". Um dia que fecha antes de abrir faz `GradeDeJogos.Horarios` tratar o dia
            // como SEM VIRADA: a grade inteira empilha no primeiro dia e vara a madrugada. O
            // planejador aceita isso na SIMULAÇÃO (é assim que se diz "domingo eu não jogo"),
            // mas gravar no torneio estragaria o próximo sorteio em silêncio.
            if (fecha <= abre || fecha <= abreSeguintes)
            {
                TempData["Erro"] = "A hora limite precisa ser depois das duas horas de abertura — "
                    + "senão o dia não comporta jogo nenhum e a grade inteira empilha no primeiro dia.";
                return RedirectToAction(nameof(Planejamento), new { id });
            }

            if (dataInicio != null) torneio.DataInicio = dataInicio.Value.Date;
            torneio.HoraInicioDoDia = abre;
            torneio.HoraInicioDiasSeguintes = abreSeguintes;
            torneio.HoraFimDoDia = fecha;
            // Mesmo teto da simulação, e pelo mesmo motivo: o valor vem do navegador. Aqui ele
            // é pior que lá — uma duração absurda GRAVADA quebra a grade do próximo sorteio.
            if (duracao is int d && d > 0)
                torneio.TempoPrevistoPartidaMinutos =
                    Math.Clamp(d, MinimoDeMinutosPorJogo, MaximoDeMinutosPorJogo);

            await _context.SaveChangesAsync();

            // ⚠️ NÃO REMARCA JOGO NENHUM: quem já tem `HorarioPrevisto` continua com ele. Estes
            // campos valem pro PRÓXIMO cálculo de grade (sorteio ou "refazer grade"), e dizer
            // isso na mensagem evita a leitura mais cara possível — o organizador achar que
            // acabou de avisar 63 duplas de um horário novo.
            TempData["Sucesso"] = torneio.Status == "Inscrições Abertas" || torneio.Status == "Chaves em Sorteio"
                ? "Horários do torneio atualizados."
                : "Horários do torneio atualizados. Os jogos que já têm horário marcado não mudaram — "
                  + "isto vale para a próxima vez que a grade for calculada.";

            return RedirectToAction(nameof(Planejamento), new { id });
        }
        // ── O CAMINHO ÚNICO, usado pela tela cheia E pelo resumo da aba do torneio ────────
        //
        // ⚠️ Duas telas respondendo sobre o MESMO torneio não podem discordar. Enquanto o
        // resumo da aba montasse o próprio plano "com os valores do torneio", bastava uma
        // normalização diferente (a duração 0 que vira 50, por exemplo) pra aba dizer "cabe" e
        // a tela cheia dizer "faltam 15". Por isso o resumo é literalmente o plano SEM botão
        // girado — mesma função, todos os parâmetros nulos.
        private sealed record BotoesDoPlanejamento(
            DateTime DataInicio, TimeSpan Abre, TimeSpan AbreSeguintes, TimeSpan Fecha,
            int Quadras, int Duracao, DateTime? Ate, string? Limites);

        private static BotoesDoPlanejamento Botoes(
            Torneio torneio, DateTime? dataInicio = null, TimeSpan? horaInicio = null,
            TimeSpan? horaSeguintes = null, TimeSpan? horaFim = null,
            int? quadras = null, int? duracao = null, DateTime? ate = null, string? limites = null)
            => new(
                (dataInicio ?? torneio.DataInicio ?? DateTime.Today).Date,
                horaInicio ?? torneio.HoraInicioDoDia,
                horaSeguintes ?? torneio.HoraInicioDiasSeguintes,
                horaFim ?? torneio.HoraFimDoDia,
                // ⚠️ TETO NOS DOIS: os valores chegam pela URL, e a tela é a única coisa que
                // limita o campo. `vagas = rodadas × quadras` com um número absurdo estoura o
                // int calado — e a conta errada é pior que o erro.
                Math.Clamp(quadras ?? torneio.QuantidadeQuadras, 1, MaximoDeQuadrasNoPlanejamento),
                duracao is int d && d > 0
                    ? Math.Clamp(d, MinimoDeMinutosPorJogo, MaximoDeMinutosPorJogo)
                    : VagasDaGrade.Duracao(torneio),
                // O prazo nasce do torneio (`DataFim`): é ele que já responde "até quando eu
                // tenho o clube". Sem prazo não há o que faltar — a grade só empurra pra frente.
                ate ?? torneio.DataFim,
                string.IsNullOrWhiteSpace(limites) ? null : limites);

        // O recorte dos botões que o `AplicarPlanejamento` escreve no torneio — os outros são
        // de simulação. Sai daqui, e não de uma lista repetida na tela, pra que acrescentar um
        // campo gravável no futuro acenda o botão sem ninguém precisar lembrar de dois lugares.
        private static (DateTime, TimeSpan, TimeSpan, TimeSpan, int) Gravavel(BotoesDoPlanejamento b) =>
            (b.DataInicio, b.Abre, b.AbreSeguintes, b.Fecha, b.Duracao);

        private static PlanejamentoDeQuadras.Plano MontarPlano(BotoesDoPlanejamento b, int total) =>
            PlanejamentoDeQuadras.Montar(
                inicio: b.DataInicio.Add(b.Abre),
                aberturaDiasSeguintes: b.AbreSeguintes,
                limitePadrao: b.Fecha,
                quadras: b.Quadras,
                duracaoMinutos: b.Duracao,
                totalDeJogos: total,
                ate: b.Ate,
                limitesPorDia: PlanejamentoDeQuadras.LerLimites(b.Limites));

        // Quantos jogos este torneio tem. Depois do sorteio é FATO; antes dele, a projeção das
        // duplas inscritas — a MESMA conta do painel "como essa grade vai ficar", pra que as
        // duas telas não possam discordar sobre o tamanho do mesmo torneio.
        //
        // ⚠️ A projeção só sabe contar grupo + mata-mata. Nos dois Americanos ela responderia
        // sobre duplas que a inscrição deles nem usa (lá é InscricaoAmericana), então o número
        // só existe depois de gerar as rodadas — e até lá a tela pede que o organizador simule.
        private async Task<(int Jogos, bool JaSorteados)> JogosDoTorneioAsync(Torneio torneio)
        {
            int sorteados = await _context.Partidas.CountAsync(p => p.TorneioId == torneio.Id);
            if (sorteados > 0) return (sorteados, true);

            return (torneio.Formato == FormatoDoTorneio.Padrao
                ? MontarPrevisaoDaGrade(torneio).TotalDeJogos
                : 0, false);
        }

        // O resumo que a aba "Planejamento de quadras" do torneio mostra: o plano com as
        // configurações que o torneio TEM hoje. Nulo quando ainda não há jogo pra contar —
        // a aba então convida pra tela cheia em vez de mostrar uma tabela de zeros.
        private async Task<PlanejamentoDeQuadras.Plano?> ResumoDoPlanejamentoAsync(Torneio torneio)
        {
            var (jogos, _) = await JogosDoTorneioAsync(torneio);
            return jogos > 0 ? MontarPlano(Botoes(torneio), jogos) : null;
        }
    }
}
