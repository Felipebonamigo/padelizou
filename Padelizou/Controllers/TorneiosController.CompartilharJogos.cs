using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;

namespace Padelizou.Controllers
{
    // "COMPARTILHAR ESTA LISTA" — a aba Jogos, filtrada, como texto pro WhatsApp e arte pro
    // story (10/09/2026). Regra em Services/ListaDeJogosCompartilhavel e Services/CartaoDosJogos;
    // aqui só entra o que é HTTP.
    //
    // ⚠️ MORA AQUI, E NÃO NO CartoesController, de propósito. Os outros cards nascem de uma
    // entidade (categoria, grupo, chave) que qualquer controller consulta; este nasce da LISTA
    // DA TELA — a que `CarregarViewBagJogosAsync` monta com os filtros de time, categoria, meus
    // jogos, clube, quadra e fase, e com o portão da chave em aprovação. Refazer essa lista em
    // outro lugar seria a segunda régua de "quais jogos", e é justamente a que vazaria: o
    // portão de 09/09 ("nao deixe q nada vaze sem ser publicado") mora naquele método.
    //
    // Família de DIVULGAÇÃO (ver o cabeçalho do CartoesController): sem login, cache público.
    // Cada inscrito que manda a grade no grupo do time é divulgação que não custa nada — a
    // mesma decisão do cartaz.
    public partial class TorneiosController
    {
        // A tela: o texto pronto, o alternador de prévias e uma arte por dia.
        [HttpGet]
        public async Task<IActionResult> CompartilharJogos(int id, [FromServices] FonteDoCartao fontes,
            int? timeFiltroId = null, int[]? categoriaFiltroIds = null, bool soMeusJogos = false,
            int? clubeFiltroId = null, string? quadraFiltro = null, string? faseFiltro = null,
            bool comPrevias = false)
        {
            var lista = await ListaParaCompartilharAsync(id, timeFiltroId, categoriaFiltroIds, soMeusJogos,
                clubeFiltroId, quadraFiltro, faseFiltro, comPrevias);
            if (lista == null) return NotFound();

            var filtros = new RouteValueDictionary
            {
                ["id"] = id,
                ["timeFiltroId"] = timeFiltroId,
                ["categoriaFiltroIds"] = categoriaFiltroIds,
                ["soMeusJogos"] = soMeusJogos,
                ["clubeFiltroId"] = clubeFiltroId,
                ["quadraFiltro"] = quadraFiltro,
                ["faseFiltro"] = faseFiltro,
                ["comPrevias"] = comPrevias,
            };

            // ABSOLUTO, como o link do convite: vai pro WhatsApp de alguém, e um caminho relativo
            // colado lá não leva a lugar nenhum. Aponta pra página dedicada de jogos (é a porta
            // de entrada dos avisos também), com os mesmos filtros e SEM `comPrevias` — na tela
            // a prévia sempre aparece.
            var semPrevias = new RouteValueDictionary(filtros);
            semPrevias.Remove("comPrevias");
            var link = Url.Action("Jogos", "Torneios", semPrevias, Request.Scheme) ?? "";

            var vm = new CompartilharJogosVM(
                Torneio: lista.Value.Torneio,
                Grade: lista.Value.Grade,
                Jogos: lista.Value.Jogos,
                Texto: TextoDaLista.Montar(lista.Value.Torneio.Nome, lista.Value.Grade.Recorte, lista.Value.Jogos, link),
                LinkDaLista: link,
                ComPrevias: comPrevias,
                TemPrevias: lista.Value.TemPrevias,
                FonteDisponivel: fontes.Disponivel,
                Filtros: filtros);

            return View(vm);
        }

        // O PNG de uma parte (uma arte por dia; dia cheio vira duas — ver CartaoDosJogos.Dividir).
        [HttpGet]
        public async Task<IActionResult> JogosImagem(int id, [FromServices] FonteDoCartao fontes,
            int? timeFiltroId = null, int[]? categoriaFiltroIds = null, bool soMeusJogos = false,
            int? clubeFiltroId = null, string? quadraFiltro = null, string? faseFiltro = null,
            bool comPrevias = false, int parte = 1)
        {
            // A recusa por falta de fonte vem ANTES de qualquer consulta, e é 404 e não uma
            // imagem em branco — mesma razão de todos os cards (ver FonteDoCartao).
            if (!fontes.Disponivel) return NotFound();

            var lista = await ListaParaCompartilharAsync(id, timeFiltroId, categoriaFiltroIds, soMeusJogos,
                clubeFiltroId, quadraFiltro, faseFiltro, comPrevias);
            if (lista == null) return NotFound();

            var arte = lista.Value.Grade.Artes.ElementAtOrDefault(parte - 1);
            if (arte == null) return NotFound();

            var png = CartaoDosJogos.Desenhar(lista.Value.Grade, arte, fontes, _env.WebRootPath);
            return EntregaDeCard.Png(Response,
                png, $"jogos-{CartoesController.Arquivo(lista.Value.Torneio.Nome)}-{CartoesController.Arquivo(arte.Rotulo)}.png");
        }

        // A lista, pela MESMA porta e pela MESMA montagem da aba Jogos. Nula quando não há o
        // que compartilhar: torneio inexistente, oculto pra quem olha, cancelado, ou chave
        // ainda em aprovação pra quem não organiza (aí a aba também está vazia).
        private async Task<(Torneio Torneio, GradeDesenhavel Grade, List<JogoDaLista> Jogos, bool TemPrevias)?>
            ListaParaCompartilharAsync(int id, int? timeFiltroId, int[]? categoriaFiltroIds, bool soMeusJogos,
                int? clubeFiltroId, string? quadraFiltro, string? faseFiltro, bool comPrevias)
        {
            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return null;

            // Mesma porta do Details e do Jogos: a grade de um torneio escondido conta o torneio
            // inteiro pra quem não deveria nem saber que ele existe.
            if (!await VisibilidadeDoTorneio.PodeAbrirAsync(_context, torneio, ObterJogadorIdLogado()))
                return null;

            // Mesma régua dos outros cards de torneio: cancelado não anuncia nada.
            if (!CampeoesDoTorneio.PodeAnunciar(torneio.Status)) return null;

            await CarregarViewBagJogosAsync(id, timeFiltroId, categoriaFiltroIds, soMeusJogos,
                new FiltroDeJogos(clubeFiltroId, quadraFiltro, faseFiltro));

            // O portão da chave em aprovação já esvaziou as listas pra quem não organiza; aqui
            // ele vira 404, porque uma tela de "compartilhar" vazia não tem o que dizer.
            if (ViewBag.ChaveAindaNaoPublicada == true) return null;

            var agendadas = ViewBag.Agendadas as List<Partida> ?? new List<Partida>();
            var previstos = ViewBag.JogosQueVem as List<ProximasFasesDaChave.JogoQueVem> ?? new();

            // ⚠️ A MESMA FILA DA TELA (OrdemNoHorario): duas contas de "quem vem antes" fariam o
            // grupo do WhatsApp receber a grade numa ordem diferente da que a pessoa viu.
            var fila = OrdemNoHorario.Ordenar(agendadas, previstos);
            var sedes = ViewData.Sedes();
            var jogos = ListaDeJogos.Montar(fila, sedes, comPrevias);

            var recorte = RecorteDaLista.Descrever(
                meusJogos: ViewBag.SoMeusJogos == true,
                time: timeFiltroId is int time
                    ? await _context.Times.AsNoTracking().Where(t => t.Id == time).Select(t => t.Nome).FirstOrDefaultAsync()
                    : null,
                categorias: (ViewBag.CategoriasDoTorneio as List<Categoria> ?? new())
                    .Where(c => (categoriaFiltroIds ?? Array.Empty<int>()).Contains(c.Id))
                    .Select(c => c.Nome),
                clube: clubeFiltroId is int clube ? sedes?.NomeDoClube(clube) : null,
                quadra: quadraFiltro,
                fase: faseFiltro);

            var grade = new GradeDesenhavel(
                Torneio: torneio.Nome,
                Recorte: recorte,
                Clube: sedes?.NomeDoClubePrincipal,
                Artes: CartaoDosJogos.Dividir(jogos));

            return (torneio, grade, jogos, previstos.Count > 0);
        }
    }
}
