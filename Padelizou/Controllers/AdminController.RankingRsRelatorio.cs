using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // O RELATÓRIO DA PARCERIA COM O RANKING — a única tela do painel que gente de fora abre.
    //
    // Pedido do Felipe (10/08/2026): uma página que ele e o pessoal do Ranking Brasil enxerguem,
    // com o retrato da integração — torneios que aderiram, quem se inscreveu por eles, quem foi
    // barrado e quem passou pelo filtro. Pro Felipe é mais um card do painel; pro parceiro é o
    // painel inteiro.
    //
    // ⚠️ TRÊS COISAS QUE ESTE ARQUIVO NUNCA FAZ, e que são o contrato do perfil novo:
    //
    //  1. Deixar o parceiro sair daqui. `ObterQuemVeORelatorioAsync` é a ÚNICA porta que aceita
    //     `IsParceiroRanking`, e ela abre uma ação só. Todo o resto do painel continua atrás do
    //     `ObterJogadorAdminAsync`, que não conhece a flag — então esquecer um lugar custa "ele
    //     não vê", nunca "ele viu o financeiro". É a mesma disciplina do assistente, um degrau
    //     mais estreita.
    //
    //  2. Gravar qualquer coisa. Não há um formulário nesta tela. Não é só disciplina: o
    //     teste que lê o código-fonte do painel (AssistenteDoSistemaTests) quebra no dia em que
    //     um SaveChangesAsync aparecer sob um [HttpGet] daqui.
    //
    //  3. Mostrar o que não é da parceria. Nada de caixa do Padelizou, nada de torneio que não
    //     aderiu, nada de contato de atleta — e o CPF sai mascarado pra quem é de fora.
    public partial class AdminController : Controller
    {
        // A porta do relatório: os dois admins, o assistente e o parceiro.
        //
        // ⚠️ Só GET, e a checagem do verbo fica aqui em vez de "confiar que a ação é GET":
        // o dia em que alguém acrescentar um POST neste arquivo, ele nasce fechado.
        private async Task<Jogador?> ObterQuemVeORelatorioAsync()
        {
            var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdValue, out var userId)) return null;

            var jogador = await _context.Jogadores.FindAsync(userId);
            if (jogador == null) return null;

            if (PoderesNoSistema.PodeEditarTudo(jogador)) return jogador;

            bool sóLendo = HttpMethods.IsGet(Request.Method) || HttpMethods.IsHead(Request.Method);
            return sóLendo && PoderesNoSistema.PodeVerRelatorioDoRanking(jogador) ? jogador : null;
        }

        // Em que categorias cada pessoa ENTROU de verdade neste torneio.
        //
        // ⚠️ Sem este mapa o relatório afirma coisa que não aconteceu. A consulta ao ranking
        // roda ANTES de a inscrição existir, e várias regras ainda podem recusá-la depois —
        // então há no banco consulta de categoria em que a pessoa nunca entrou. Ver o aviso em
        // RelatorioDoRankingRs.Juntar.
        //
        // Mesmas exclusões do dinheiro (Services/AcertoComORankingRs): lista de espera e time
        // ficam de fora, e vaga de parceiro ainda vazia não é ninguém.
        private async Task<Dictionary<int, HashSet<int>>> CategoriasEmQueEntrouAsync(int torneioId)
        {
            var mapa = new Dictionary<int, HashSet<int>>();

            void Anotar(int jogadorId, int categoriaId)
            {
                if (!mapa.TryGetValue(jogadorId, out var cats))
                    mapa[jogadorId] = cats = new HashSet<int>();
                cats.Add(categoriaId);
            }

            var duplas = await _context.Duplas
                .Where(d => d.Categoria.TorneioId == torneioId && !d.EmListaDeEspera && d.NomeTime == null)
                .Select(d => new { d.CategoriaId, d.Jogador1Id, d.Jogador2Id })
                .ToListAsync();

            foreach (var dupla in duplas)
            {
                if (dupla.Jogador1Id is int j1) Anotar(j1, dupla.CategoriaId);
                if (dupla.Jogador2Id is int j2) Anotar(j2, dupla.CategoriaId);
            }

            var americanas = await _context.InscricoesAmericanas
                .Where(i => i.Categoria.TorneioId == torneioId && !i.EmListaDeEspera)
                .Select(i => new { i.CategoriaId, i.JogadorId })
                .ToListAsync();

            foreach (var inscricao in americanas) Anotar(inscricao.JogadorId, inscricao.CategoriaId);

            return mapa;
        }

        [HttpGet]
        public async Task<IActionResult> RankingRsRelatorio()
        {
            var quem = await ObterQuemVeORelatorioAsync();
            if (quem == null) return RedirectToAction("Perfil", "Auth");

            var torneios = await _context.Torneios
                .Where(t => t.ValidarPeloRankingRs)
                .OrderByDescending(t => t.DataInicio)
                .ToListAsync();

            var torneioIds = torneios.Select(t => t.Id).ToList();

            // As três coleções de apoio numa consulta cada, e não uma por torneio: são tabelas
            // pequenas, e o laço abaixo já faz quatro idas ao banco por torneio pra montar os
            // inscritos.
            var consultas = await _context.ConsultasAoRankingRs
                .Where(c => torneioIds.Contains(c.TorneioId))
                .ToListAsync();

            var bloqueios = await _context.BloqueiosDoRanking
                .Where(b => torneioIds.Contains(b.TorneioId))
                .OrderByDescending(b => b.CriadoEm)
                .ToListAsync();

            var acertos = await _context.AcertosRankingRs
                .Where(a => torneioIds.Contains(a.TorneioId))
                .ToDictionaryAsync(a => a.TorneioId, a => a);

            // Nome da categoria pros bloqueios: a lista deles é lida por categoria ("não pode
            // jogar 6ª Masculina"), e sem isto seria um id na tela.
            var categoriaIds = bloqueios.Select(b => b.CategoriaId).Distinct().ToList();
            var nomeDaCategoria = await _context.Categorias
                .Where(c => categoriaIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Nome);

            var nomeDoTorneio = torneios.ToDictionary(t => t.Id, t => t.Nome);

            var linhas = new List<RelatorioDoRankingRs.LinhaDoTorneio>();
            foreach (var torneio in torneios)
            {
                var cobradas = AcertoComORankingRs.Cobradas(await InscritosDoRankingRsAsync(torneio.Id));

                // O CPF é a chave que casa inscrito com consulta — na hora da consulta a pessoa
                // podia ainda não ter linha em `Jogador`, então o registro é por CPF.
                var jogadorIds = cobradas.Select(p => p.JogadorId).ToList();
                var cpfDoJogador = await _context.Jogadores
                    .Where(j => jogadorIds.Contains(j.Id))
                    .ToDictionaryAsync(j => j.Id, j => j.Cpf);

                var inscritos = RelatorioDoRankingRs.Juntar(
                    cobradas,
                    cpfDoJogador,
                    await CategoriasEmQueEntrouAsync(torneio.Id),
                    consultas.Where(c => c.TorneioId == torneio.Id));

                var barrados = bloqueios
                    .Where(b => b.TorneioId == torneio.Id)
                    .Select(b => new RelatorioDoRankingRs.Barrado(
                        b,
                        nomeDoTorneio.GetValueOrDefault(b.TorneioId, "—"),
                        nomeDaCategoria.GetValueOrDefault(b.CategoriaId, "—")))
                    .ToList();

                acertos.TryGetValue(torneio.Id, out var acerto);

                linhas.Add(new RelatorioDoRankingRs.LinhaDoTorneio(
                    torneio,
                    inscritos,
                    barrados,
                    AcertoComORankingRs.Valor(inscritos.Count, _rankingRs.CustoPorInscrito),
                    acerto));
            }

            ViewBag.Linhas = linhas;
            ViewBag.Totais = RelatorioDoRankingRs.Somar(linhas);
            ViewBag.CustoPorInscrito = _rankingRs.CustoPorInscrito;

            // ⚠️ DESDE QUANDO EXISTE REGISTRO DE CONSULTA. A tabela nasceu em 10/08/2026, e a
            // integração é de 07/08 — quem se inscreveu no meio não tem linha, e apareceria
            // como "sem registro de consulta". Sem esta data na tela, esse buraco de três dias
            // pareceria integração falhando. Nulo = ninguém foi consultado ainda.
            ViewBag.RegistroDesde = consultas.Count == 0
                ? (DateTime?)null
                : consultas.Min(c => c.CriadoEm);

            // Quem é de fora vê o CPF mascarado. O dono vê inteiro — é o sistema dele.
            ViewBag.CpfInteiro = PoderesNoSistema.PodeOlharTudo(quem);
            ViewBag.SoVeORelatorio = PoderesNoSistema.SoVeORelatorioDoRanking(quem);
            ViewBag.NomeDeQuemOlha = quem.Nome;

            return View();
        }
    }
}
