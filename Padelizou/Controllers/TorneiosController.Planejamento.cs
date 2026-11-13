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

        private const int MinimoDeMinutosPorJogo = 5;
        private const int MaximoDeMinutosPorJogo = 240;

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Planejamento(
            int id, DateTime? dataInicio = null, TimeSpan? horaInicio = null,
            TimeSpan? horaSeguintes = null, TimeSpan? horaFim = null,
            int? duracao = null, int? jogos = null,
            DateTime? ate = null, string? limites = null,
            Dictionary<string, string>? limite = null,
            string? aberturas = null, Dictionary<string, string>? abertura = null)
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
            // A ABERTURA POR DIA (11/11/2026), no mesmo formato e pelo mesmo caminho do fechamento.
            var aberturasTexto = abertura is { Count: > 0 }
                ? string.Join(';', abertura
                    .Where(par => !string.IsNullOrWhiteSpace(par.Value))
                    .Select(par => $"{par.Key}={par.Value}"))
                : aberturas;
            // Tabela enviada = o organizador decidiu cada caixinha (vazia = o padrão do dia). Sem
            // ela e sem link, a tela abre no que o torneio JÁ TEM gravado por dia.
            bool veioDaTabela = limite is { Count: > 0 } || abertura is { Count: > 0 }
                || limites != null || aberturas != null;

            // AS QUADRAS SÃO AS DE VERDADE (09/09/2026), com a janela de cada uma — não mais um
            // número girável. "E se eu tivesse 4?" continua respondido por `QuadrasNecessarias`
            // e por acrescentar uma linha na tabela; o que saiu foi a possibilidade de a
            // tabela dizer 2 e a conta usar 4.
            var quadrasReais = await QuadrasDoPlanejamentoAsync(id);

            var botoes = veioDaTabela
                ? Botoes(torneio, dataInicio, horaInicio, horaSeguintes, horaFim,
                    duracao, ate, limitesTexto, aberturasTexto ?? "")
                : Botoes(torneio, dataInicio, horaInicio, horaSeguintes, horaFim, duracao, ate);
            var plano = MontarPlano(botoes, total, quadrasReais);

            // ── A GRADE DE VERDADE MANDA, DEPOIS DO SORTEIO (11/11/2026) ─────────────────────
            // A soma de vagas não enxerga que semifinal espera quartas e final espera semifinal, nem
            // a dupla presa a um turno: a tela dizia "Cabe" e a grade tinha jogo "por ordem". Com a
            // grade montada, conta o que ficou SEM HORA — jogos reais e os previstos do mata-mata.
            // Só sem simulação: girando os botões, a grade de verdade ainda não mudou.
            // (GradeNuncaPassaDoFimDoTorneioTests)
            int semHorario = sorteados && !simulando && !veioDaTabela && !torneio.SemHorarioPrevisto
                ? await JogosSemHorarioNaGradeAsync(id)
                : 0;

            return View(new PlanejamentoDeQuadrasVM
            {
                Torneio = torneio,
                JogosSemHorario = semHorario,
                Plano = plano,
                JogosDoTorneio = reais,
                JogosJaSorteados = sorteados,
                Simulando = simulando,
                SoLeitura = !await EhOrganizadorAsync(id, jogadorId),
                // ⚠️ SÓ PELO QUE O "APLICAR" DE FATO GRAVA. Quadras são botão de SIMULAÇÃO
                // (precisam de nome e clube) — acender o botão por causa delas faria o
                // organizador apertar "Aplicar" achando que contratou a terceira quadra. A faixa
                // de cada dia GRAVA desde 11/11/2026 (Torneio.HorariosPorDia), e por isso conta.
                MudouAlgo = Gravavel(botoes) != Gravavel(Botoes(torneio)),
                DataInicio = botoes.DataInicio,
                HoraInicio = botoes.Abre,
                HoraSeguintes = botoes.AbreSeguintes,
                HoraFim = botoes.Fecha,
                Duracao = botoes.Duracao,
                Jogos = total,
                Ate = botoes.Ate,
                Limites = botoes.Limites,
                Aberturas = botoes.Aberturas,
                Quadras = quadrasReais,
                Clubes = await _context.Clubes.ParaEscolher().ToListAsync(),
                NomeDoClubeDoTorneio = await _context.Clubes
                    .Where(c => c.Id == torneio.ClubeId).Select(c => c.Nome).FirstOrDefaultAsync()
                    ?? torneio.LocalTorneio ?? "Clube do torneio",
                MaximoDeQuadras = MaximoDeQuadrasDoTorneio,
                AberturaDoTorneio = JanelaDoTorneioInteiro(torneio).De,
                FechamentoDoTorneio = JanelaDoTorneioInteiro(torneio).Ate,
                NovaQuadra = PlanejamentoDeQuadras.SugerirProximaQuadra(quadrasReais),
            });
        }

        // ── A TABELA DE QUADRAS (09/09/2026) ─────────────────────────────────────────────
        //
        // 🗣️ Felipe: "adicionar aqui nessa tela uma ou mais quadras, para calcular corretamente,
        // crie uma tabela também, para que possa controlar as quadras que estarão disponíveis,
        // se são no mesmo clube ou não, e quais horários elas irão receber (de que horas até
        // que horas, cada quadra)". Perguntado, escolheu: o planejador é o LUGAR ÚNICO de
        // quadra — os campos de nome/quantidade saíram do `Editar` e viraram link pra cá.
        //
        // ⚠️ AS TRÊS REGRAS QUE ESTES POSTS SEGURAM, cada uma já bug de produção:
        //   1. `QuantidadeQuadras` == número de linhas, sempre — reescrita a cada salvamento.
        //      Divergindo, a grade oferece vaga sem nome e o jogo nasce com hora e sem quadra
        //      (Interno de 05/08/2026; Services/NomesDeQuadra existe por isso).
        //   2. Nome é identidade (Services/NomeDeQuadraUnico): `Partida.NomeQuadra` é texto
        //      solto, e duas "Quadra 1" seriam a MESMA quadra pra grade.
        //   3. Quadra com jogo marcado não se apaga nem se renomeia: o jogo apontaria pra um
        //      nome que não existe mais, e o seletor "mudar de quadra" não teria como trazê-lo
        //      de volta.
        //
        // Nulo em `ClubeId` = "no clube do torneio", a mesma convenção de AlterarSedesDoTorneio
        // e de toda quadra de torneio de uma sede só.

        // O alfabeto de nomes automáticos do `Create` tem 26 letras; é o teto que sempre valeu.
        private const int MaximoDeQuadrasDoTorneio = 26;

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SalvarQuadraDoPlanejamento(
            int id, int? quadraId, string? nome, int? clubeId, DateTime? de, DateTime? ate)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var quadras = await _context.Quadras.Where(q => q.TorneioId == id).OrderBy(q => q.Id).ToListAsync();

            // ⚠️ A QUADRA É PROCURADA DENTRO DESTE TORNEIO, nunca por `FindAsync(quadraId)`: o id
            // vem do navegador, e um POST montado à mão renomearia a quadra de outro organizador.
            Quadra? alvo = null;
            if (quadraId is int idDaQuadra)
            {
                alvo = quadras.FirstOrDefault(q => q.Id == idDaQuadra);
                if (alvo == null) return Recusar(id, "Essa quadra não é deste torneio.");
            }

            var nomeLimpo = (nome ?? "").Trim();
            if (nomeLimpo.Length == 0) return Recusar(id, "Dê um nome à quadra.");

            if (alvo == null && quadras.Count >= MaximoDeQuadrasDoTorneio)
                return Recusar(id, $"O torneio já tem {MaximoDeQuadrasDoTorneio} quadras, que é o máximo.");

            // Janela que fecha antes de abrir é quadra que nunca existe: a grade não marcaria
            // nada nela e ninguém saberia por quê. Meio aberta ([De, Ate)), então iguais também
            // é vazia. Um lado só (só "de" ou só "até") vale, e SedesDoTorneio.QuadraAberta sabe.
            // ⚠️ `<`, e não `<=`: desde 09/09/2026 o "até" é a hora do ÚLTIMO JOGO, então "das 8h
            // às 8h" é legítimo — quer dizer "uma rodada, às 8h". Só a janela INVERTIDA é
            // impossível, e recusá-la aqui é o único lugar em que dá pra dizer: uma quadra que
            // nunca abre não recebe jogo nenhum, e ninguém saberia por quê.
            if (de is DateTime abre && ate is DateTime fecha && fecha < abre)
                return Recusar(id, "A quadra não pode fechar antes de abrir — confira \"de\" e \"até\".");

            // Regra 3: renomear quadra com jogo marcado deixaria os jogos com o nome velho e o
            // cadastro com o novo (ver Services/NomesDeQuadra). Clube e janela continuam
            // editáveis — mudam onde e quando, não QUEM a quadra é.
            if (alvo != null && !string.Equals(alvo.Nome.Trim(), nomeLimpo, StringComparison.OrdinalIgnoreCase)
                && await QuadraTemJogoAsync(id, alvo.Nome))
            {
                return Recusar(id, $"\"{alvo.Nome}\" já tem jogo marcado — o nome não muda mais. "
                                   + "Local e horário continuam editáveis.");
            }

            // O clube só é aceito se existir; o do próprio torneio vira nulo, que é a convenção.
            int? clube = null;
            if (clubeId is int clubeEscolhido && clubeEscolhido > 0 && clubeEscolhido != torneio.ClubeId
                && await _context.Clubes.AnyAsync(c => c.Id == clubeEscolhido))
            {
                clube = clubeEscolhido;
            }

            // Regra 2, com a explicação certa: num torneio de duas sedes a saída natural é pôr o
            // clube dentro do nome.
            bool maisDeUmClube = clube != null || quadras.Any(q => q != alvo && q.ClubeId != null);
            var nomes = quadras.Where(q => q != alvo).Select(q => q.Nome).Append(nomeLimpo);
            if (NomeDeQuadraUnico.MotivoParaNaoSalvar(nomes, maisDeUmClube) is { } motivo)
                return Recusar(id, motivo);

            if (alvo == null)
            {
                alvo = new Quadra { TorneioId = id };
                _context.Quadras.Add(alvo);
                quadras.Add(alvo);
            }

            // ⚠️ A JANELA DO TORNEIO INTEIRO VIRA NULO (09/09/2026). A tela passou a MOSTRAR o
            // expediente do torneio nos campos da quadra sem limite — dois campos vazios eram
            // lidos como "faltou preencher" ("ficou bastante dado em branco", disse o Felipe) —,
            // e salvar a linha sem mexer nela não pode transformar "sem limite" numa janela
            // gravada. Nulo é também o que sobrevive a uma mudança de data do torneio: uma
            // janela literal ficaria pra trás e fecharia a quadra em silêncio.
            var doTorneio = JanelaDoTorneioInteiro(torneio);
            bool janelaEhOTorneioInteiro = de == doTorneio.De && ate == doTorneio.Ate;

            alvo.Nome = nomeLimpo;
            alvo.ClubeId = clube;
            alvo.DisponivelDe = janelaEhOTorneioInteiro ? null : de;
            alvo.DisponivelAte = janelaEhOTorneioInteiro ? null : ate;

            // Regra 1.
            torneio.QuantidadeQuadras = quadras.Count;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = quadraId == null
                ? $"Quadra \"{nomeLimpo}\" adicionada — o torneio agora tem {quadras.Count}."
                : $"Quadra \"{nomeLimpo}\" atualizada.";

            return RedirectToAction(nameof(Planejamento), new { id });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoverQuadraDoPlanejamento(int id, int quadraId)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            var torneio = await _context.Torneios.FindAsync(id);
            if (torneio == null) return NotFound();

            var quadras = await _context.Quadras.Where(q => q.TorneioId == id).ToListAsync();
            var alvo = quadras.FirstOrDefault(q => q.Id == quadraId);
            if (alvo == null) return Recusar(id, "Essa quadra não é deste torneio.");

            // Torneio sem quadra é grade sem vaga; o `Editar` sempre garantiu pelo menos uma.
            if (quadras.Count <= 1) return Recusar(id, "O torneio precisa de pelo menos uma quadra.");

            // Regra 3.
            if (await QuadraTemJogoAsync(id, alvo.Nome))
                return Recusar(id, $"\"{alvo.Nome}\" já tem jogo marcado e não pode ser removida.");

            // A preferência de categoria que apontava pra ela sai junto: órfã seria FK quebrada
            // no Postgres, e no InMemory dos testes uma escolha apontando pro nada.
            _context.QuadrasDaCategoria.RemoveRange(
                await _context.QuadrasDaCategoria.Where(p => p.QuadraId == alvo.Id).ToListAsync());
            _context.Quadras.Remove(alvo);

            // Regra 1.
            torneio.QuantidadeQuadras = quadras.Count - 1;

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = $"Quadra \"{alvo.Nome}\" removida — o torneio agora tem {quadras.Count - 1}.";
            return RedirectToAction(nameof(Planejamento), new { id });
        }

        private IActionResult Recusar(int id, string motivo)
        {
            TempData["Erro"] = motivo;
            return RedirectToAction(nameof(Planejamento), new { id });
        }

        // "Já tem jogo nesta quadra?" — pelo NOME, que é como o jogo guarda a quadra, sem
        // diferenciar caixa nem espaço nas pontas (a mesma tolerância de NomeDeQuadraUnico).
        // Comparado em memória de propósito: a lista de nomes distintos de um torneio é
        // pequena, e assim não há tradução de `ToLower` pra confiar no provedor.
        private async Task<bool> QuadraTemJogoAsync(int torneioId, string nomeDaQuadra)
        {
            var nomesEmJogo = await _context.Partidas
                .Where(p => p.TorneioId == torneioId && p.NomeQuadra != null)
                .Select(p => p.NomeQuadra!)
                .Distinct()
                .ToListAsync();

            return nomesEmJogo.Any(n => string.Equals(n.Trim(), nomeDaQuadra.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        // O expediente do torneio inteiro: da abertura do primeiro dia ao último horário em que
        // um jogo pode COMEÇAR no último dia. É o que uma quadra sem janela de fato faz, e é o
        // que a tela mostra nos campos dela.
        //
        // Sem `DataFim` o torneio não tem prazo; aí o fechamento é a própria abertura, e a
        // comparação de "janela == torneio inteiro" simplesmente nunca casa — que é o certo:
        // não há um "inteiro" pra comparar.
        private static (DateTime De, DateTime Ate) JanelaDoTorneioInteiro(Torneio torneio)
        {
            var calendario = HorarioDoDia.Do(torneio);
            var abre = torneio.AberturaDaGrade;
            var fecha = torneio.DataFim is DateTime fim ? fim.Date.Add(calendario.Limite(fim)) : abre;
            return (abre, fecha);
        }

        // Por Id, que é a ordem em que nasceram — a mesma da tabela na tela e a mesma que o
        // `Create` usou pra numerá-las.
        private Task<List<Quadra>> QuadrasDoPlanejamentoAsync(int torneioId) =>
            _context.Quadras.Where(q => q.TorneioId == torneioId).OrderBy(q => q.Id).ToListAsync();

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
            TimeSpan? horaSeguintes = null, TimeSpan? horaFim = null, int? duracao = null,
            // A faixa de cada dia ("yyyy-MM-dd=HH:mm;..."), como a tabela Dia a dia manda.
            // `porDia` = a tela mandou a tabela (mesmo sem nenhum dia próprio, o que LIMPA as
            // faixas); sem ele e sem os textos — aba aberta antes deste deploy — as faixas FICAM.
            string? aberturas = null, string? limites = null, bool? porDia = null,
            Dictionary<string, string>? abertura = null, Dictionary<string, string>? limite = null)
        {
            if (!await EhOrganizadorAsync(id, ObterJogadorIdLogado() ?? 0)) return Forbid();

            // Com as duplas: o recálculo da grade, lá embaixo, encaixa jogo por dupla.
            var torneio = await _context.Torneios
                .Include(t => t.Categorias).ThenInclude(c => c.Duplas)
                .FirstOrDefaultAsync(t => t.Id == id);
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

            // ── A FAIXA DE CADA DIA ─────────────────────────────────────────────────────────
            // Só vira faixa própria o dia que DIFERE do padrão: um dia que veio igual não é
            // gravado, senão mudar o padrão depois não o alcançaria.
            // As caixinhas da tabela COMO ESTÃO na tela (o script do Aplicar copia na hora de
            // enviar) valem mais que os textos escondidos, que são da tela ao ABRIR — 🗣️ Felipe,
            // 11/11: *"não está salvando ali a alteração"*. Caixinha vazia = o padrão do dia.
            if (abertura is { Count: > 0 } || limite is { Count: > 0 })
            {
                static string Juntar(Dictionary<string, string>? caixas) => string.Join(';', (caixas ?? new())
                    .Where(par => !string.IsNullOrWhiteSpace(par.Value))
                    .Select(par => $"{par.Key}={par.Value}"));
                aberturas = Juntar(abertura);
                limites = Juntar(limite);
                porDia = true;
            }

            string? horariosPorDia = torneio.HorariosPorDia;
            if (aberturas != null || limites != null || porDia == true)
            {
                var primeiroDia = (dataInicio ?? torneio.DataInicio ?? DateTime.Today).Date;
                var abrePorDia = PlanejamentoDeQuadras.LerLimites(aberturas);
                var fechaPorDia = PlanejamentoDeQuadras.LerLimites(limites);
                var faixas = new Dictionary<DateTime, HorarioDoDia.Faixa>();

                foreach (var dia in abrePorDia.Keys.Union(fechaPorDia.Keys).OrderBy(d => d))
                {
                    var padraoAbre = dia == primeiroDia ? abre : abreSeguintes;
                    var abreDia = abrePorDia.TryGetValue(dia, out var a) ? a : padraoAbre;
                    var fechaDia = fechaPorDia.TryGetValue(dia, out var f) ? f : fecha;

                    // ⚠️ FRONTEIRA DE CONFIANÇA, a mesma da hora limite geral logo acima.
                    if (fechaDia <= abreDia)
                    {
                        TempData["Erro"] = $"{dia:dd/MM}: o último jogo precisa começar depois da abertura do dia "
                            + $"({abreDia:hh\\:mm}).";
                        return RedirectToAction(nameof(Planejamento), new { id });
                    }

                    if (abreDia != padraoAbre || fechaDia != fecha)
                        faixas[dia] = new HorarioDoDia.Faixa(abreDia, fechaDia);
                }

                horariosPorDia = HorarioDoDia.Escrever(faixas);
            }

            if (dataInicio != null) torneio.DataInicio = dataInicio.Value.Date;
            torneio.HorariosPorDia = horariosPorDia;
            torneio.HoraInicioDoDia = abre;
            torneio.HoraInicioDiasSeguintes = abreSeguintes;
            torneio.HoraFimDoDia = fecha;
            // Mesmo teto da simulação, e pelo mesmo motivo: o valor vem do navegador. Aqui ele
            // é pior que lá — uma duração absurda GRAVADA quebra a grade do próximo sorteio.
            if (duracao is int d && d > 0)
                torneio.TempoPrevistoPartidaMinutos =
                    Math.Clamp(d, MinimoDeMinutosPorJogo, MaximoDeMinutosPorJogo);

            await _context.SaveChangesAsync();

            // ── COM A CHAVE SORTEADA, A GRADE ACOMPANHA (11/11/2026) ───────────────────────
            // 🗣️ Felipe: *"que eu possa alterar mesmo depois de chaves prontas e ele recalcule"*.
            // É o MESMO recálculo do "Recalcular horários" (RefazerGrade): os mesmos confrontos,
            // finalizados e em quadra intocados, e a promessa re-gravada depois.
            //
            // ⚠️ E TAMBÉM DEPOIS DE PÚBLICA (11/11/2026). 🗣️ Felipe, com o torneio publicado: *"não
            // está salvando ali a alteração e refazendo os horários"*. A regra de 10/10
            // (GradePublicadaNaoSeRefazTests) continua valendo pro RefazerGrade e pro Ajustar —
            // aqui é o organizador mudando o EXPEDIENTE de propósito, e a grade tem que segui-lo.
            var todos = await _context.Partidas.Where(p => p.TorneioId == id).ToListAsync();
            if (todos.Any(p => p.Status == "Agendada"))
            {
                var (remarcar, _) = await RecalcularAGradeAsync(torneio, todos);
                var horariosMudados = AvisoDeHorarioMudado.Capturar(_context, id);
                await _context.SaveChangesAsync();
                AnotarHorariosMudados(torneio, horariosMudados);
                await GravarAGradePrevistaAsync(torneio);
                await _context.SaveChangesAsync();

                var marcados = remarcar.Where(j => j.HorarioPrevisto != null).ToList();
                TempData["Sucesso"] = marcados.Count == 0
                    ? "Horários do torneio atualizados — nada foi remarcado: não sobrou horário no expediente."
                    : $"Horários do torneio atualizados e grade recalculada: {marcados.Count} jogos, de "
                      + $"{marcados.Min(j => j.HorarioPrevisto):dd/MM HH:mm} a {marcados.Max(j => j.HorarioPrevisto):dd/MM HH:mm}. "
                      + "Os confrontos não mudaram.";
                return RedirectToAction(nameof(Planejamento), new { id });
            }

            TempData["Sucesso"] = "Horários do torneio atualizados.";
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
            int Duracao, DateTime? Ate, string? Limites, string? Aberturas);

        private static BotoesDoPlanejamento Botoes(
            Torneio torneio, DateTime? dataInicio = null, TimeSpan? horaInicio = null,
            TimeSpan? horaSeguintes = null, TimeSpan? horaFim = null,
            int? duracao = null, DateTime? ate = null, string? limites = null, string? aberturas = null)
            => new(
                (dataInicio ?? torneio.DataInicio ?? DateTime.Today).Date,
                horaInicio ?? torneio.HoraInicioDoDia,
                horaSeguintes ?? torneio.HoraInicioDiasSeguintes,
                horaFim ?? torneio.HoraFimDoDia,
                // ⚠️ TETO: o valor chega pela URL, e a tela é a única coisa que limita o campo.
                // Uma duração absurda faz a conta errada calada — pior que o erro.
                duracao is int d && d > 0
                    ? Math.Clamp(d, MinimoDeMinutosPorJogo, MaximoDeMinutosPorJogo)
                    : VagasDaGrade.Duracao(torneio),
                // O prazo nasce do torneio (`DataFim`): é ele que já responde "até quando eu
                // tenho o clube". Sem prazo não há o que faltar — a grade só empurra pra frente.
                ate ?? torneio.DataFim,
                // Sem tabela enviada (`null`), a faixa que o torneio JÁ TEM por dia; com a tabela,
                // o que ela mandou — vazio inclusive, que é "todos os dias no padrão".
                limites == null ? LimitesGravados(torneio) : (string.IsNullOrWhiteSpace(limites) ? null : limites),
                aberturas == null ? AberturasGravadas(torneio) : (string.IsNullOrWhiteSpace(aberturas) ? null : aberturas));

        // A faixa gravada por dia, no formato da tela ("yyyy-MM-dd=HH:mm;...").
        private static string? LimitesGravados(Torneio torneio) =>
            TextoPorDia(HorarioDoDia.Ler(torneio.HorariosPorDia).ToDictionary(f => f.Key, f => f.Value.UltimoJogo));

        private static string? AberturasGravadas(Torneio torneio) =>
            TextoPorDia(HorarioDoDia.Ler(torneio.HorariosPorDia).ToDictionary(f => f.Key, f => f.Value.Abre));

        private static string? TextoPorDia(Dictionary<DateTime, TimeSpan> horas) =>
            horas.Count == 0 ? null
            : string.Join(';', horas.OrderBy(h => h.Key).Select(h => $"{h.Key:yyyy-MM-dd}={h.Value:hh\\:mm}"));

        // As faixas que um conjunto de botões GRAVARIA — só os dias que diferem do padrão, a
        // mesma régua do AplicarPlanejamento. É o que acende (ou não) o botão de aplicar.
        private static string? FaixasDe(BotoesDoPlanejamento b)
        {
            var abrePorDia = PlanejamentoDeQuadras.LerLimites(b.Aberturas);
            var fechaPorDia = PlanejamentoDeQuadras.LerLimites(b.Limites);
            var faixas = new Dictionary<DateTime, HorarioDoDia.Faixa>();
            foreach (var dia in abrePorDia.Keys.Union(fechaPorDia.Keys))
            {
                var padraoAbre = dia == b.DataInicio ? b.Abre : b.AbreSeguintes;
                var abreDia = abrePorDia.TryGetValue(dia, out var a) ? a : padraoAbre;
                var fechaDia = fechaPorDia.TryGetValue(dia, out var f) ? f : b.Fecha;
                if (abreDia != padraoAbre || fechaDia != b.Fecha) faixas[dia] = new HorarioDoDia.Faixa(abreDia, fechaDia);
            }
            return HorarioDoDia.Escrever(faixas);
        }

        // O recorte dos botões que o `AplicarPlanejamento` escreve no torneio — os outros são
        // de simulação. Sai daqui, e não de uma lista repetida na tela, pra que acrescentar um
        // campo gravável no futuro acenda o botão sem ninguém precisar lembrar de dois lugares.
        private static (DateTime, TimeSpan, TimeSpan, TimeSpan, int, string?) Gravavel(BotoesDoPlanejamento b) =>
            (b.DataInicio, b.Abre, b.AbreSeguintes, b.Fecha, b.Duracao, FaixasDe(b));

        // As quadras entram como LISTA, com a janela de cada uma — é o que faz a quadra
        // alugada das 8h às 14h render 8 rodadas e não 20 (Services/PlanejamentoDeQuadras).
        private static PlanejamentoDeQuadras.Plano MontarPlano(BotoesDoPlanejamento b, int total,
            IReadOnlyList<Quadra> quadras) =>
            PlanejamentoDeQuadras.Montar(
                inicio: b.DataInicio.Add(b.Abre),
                aberturaDiasSeguintes: b.AbreSeguintes,
                limitePadrao: b.Fecha,
                quadras: quadras,
                duracaoMinutos: b.Duracao,
                totalDeJogos: total,
                ate: b.Ate,
                limitesPorDia: PlanejamentoDeQuadras.LerLimites(b.Limites),
                aberturasPorDia: PlanejamentoDeQuadras.LerLimites(b.Aberturas));

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

            // ⚠️ DEPOIS DO SORTEIO SÓ EXISTEM OS GRUPOS (11/11/2026): quartas, semis e finais nascem
            // pelo robô e também precisam de quadra. Contar só os sorteados fez a tela do Chakra
            // dizer "Cabe — 19 jogos em 1 quadra" com o mata-mata inteiro fora da conta; o que não
            // coube foi parar na segunda e na terça. É a mesma régua do aviso do sorteio
            // (`MontarPrevisaoDaGrade(...).TotalDeJogos`). (GradeNuncaPassaDoFimDoTorneioTests)
            if (sorteados > 0)
                return (torneio.Formato == FormatoDoTorneio.Padrao
                    ? Math.Max(sorteados, MontarPrevisaoDaGrade(torneio).TotalDeJogos)
                    : sorteados, true);

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
            return jogos > 0 ? MontarPlano(Botoes(torneio), jogos, await QuadrasDoPlanejamentoAsync(torneio.Id)) : null;
        }
    }
}
