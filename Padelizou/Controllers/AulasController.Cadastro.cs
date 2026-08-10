using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using System.Security.Claims;

namespace padelizou.Controllers
{
    // O que o professor cadastra pra existir pro aluno: locais, cidades, horários e a política
    // de cancelamento. É a "escada do professor" de Services/CadastroDeProfessor.
    // O [Authorize] da classe fica no arquivo principal (AulasController.cs).
    public partial class AulasController
    {
        [HttpGet]
        public async Task<IActionResult> MeusLocais()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var locais = await _context.LocaisAula
                .Include(l => l.Pacotes.OrderBy(p => p.QuantidadeAulas))
                .Include(l => l.Horarios)
                .Where(l => l.ProfessorId == professorId)
                .OrderByDescending(l => l.Ativo)
                .ThenBy(l => l.Nome)
                .ToListAsync();

            // Quantas aulas cada local já tem: é o que decide se dá pra apagar de vez ou só
            // desativar (ver Services/RemocaoDeLocal). Uma consulta agrupada, não uma por
            // local — o professor com dez quadras não paga dez idas ao banco por isso.
            var ids = locais.Select(l => l.Id).ToList();
            ViewBag.AulasPorLocal = await _context.Aulas
                .Where(a => ids.Contains(a.LocalAulaId))
                .GroupBy(a => a.LocalAulaId)
                .Select(g => new { LocalId = g.Key, Total = g.Count() })
                .ToDictionaryAsync(x => x.LocalId, x => x.Total);

            return View(locais);
        }

        [HttpPost]
        public async Task<IActionResult> CriarLocal(string nome, string? endereco, decimal precoPadrao,
            decimal? precoDupla, decimal? precoTrio, decimal? custoPorAula)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            _context.LocaisAula.Add(new LocalAula
            {
                ProfessorId = professorId.Value,
                Nome = nome,
                // Endereço é opcional: muita aula é num clube que o aluno já sabe onde fica.
                // Em branco vira nulo pra tela não ter que checar as duas coisas ao exibir.
                Endereco = string.IsNullOrWhiteSpace(endereco) ? null : endereco.Trim(),
                PrecoPadrao = precoPadrao,
                PrecoDupla = precoDupla > 0 ? precoDupla : null,
                PrecoTrio = precoTrio > 0 ? precoTrio : null,
                CustoPorAula = custoPorAula,
                Ativo = true
            });
            await _context.SaveChangesAsync();

            return RedirectToAction("MeusLocais");
        }

        // Editar a tabela de preços de um local que já existe. Antes só o custo era editável:
        // não havia como corrigir o preço da aula depois de cadastrado, e reajuste (ou erro de
        // digitação no primeiro cadastro) é coisa que acontece.
        [HttpPost]
        public async Task<IActionResult> AtualizarPrecos(int id, decimal precoPadrao,
            decimal? precoDupla, decimal? precoTrio, decimal? custoPorAula)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local != null)
            {
                // Zero ou negativo anunciaria aula de graça por escorregão de digitação, então
                // o preço da individual só troca por um valor de verdade. Dupla e trio aceitam
                // branco: é o jeito de dizer "não faço esse tamanho".
                if (precoPadrao > 0) local.PrecoPadrao = precoPadrao;
                local.PrecoDupla = precoDupla > 0 ? precoDupla : null;
                local.PrecoTrio = precoTrio > 0 ? precoTrio : null;
                local.CustoPorAula = custoPorAula;
                await _context.SaveChangesAsync();
                TempData["SucessoPacote"] = $"Preços de {local.Nome} atualizados.";
            }

            return RedirectToAction("MeusLocais");
        }

        // Apagar de vez, não só desativar. Vale só pro local que ainda não tem aula nenhuma —
        // com aula, o banco recusaria (Aula.LocalAulaId é Restrict) e o professor levaria um
        // erro 500 no lugar de uma explicação. A regra está em Services/RemocaoDeLocal.
        [HttpPost]
        public async Task<IActionResult> ExcluirLocal(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local == null) return RedirectToAction("MeusLocais");

            var aulas = await _context.Aulas.CountAsync(a => a.LocalAulaId == id);
            var impedimento = RemocaoDeLocal.MotivoParaNaoApagar(aulas);
            if (impedimento != null)
            {
                TempData["ErroPacote"] = impedimento;
                return RedirectToAction("MeusLocais");
            }

            // Horários e pacotes caem por cascade (ver DbPadelContext). Apagados aqui à mão
            // mesmo assim, pra não depender de a constraint estar do jeito que o modelo diz
            // em cada um dos três bancos.
            var horarios = await _context.HorariosDisponiveis.Where(h => h.LocalAulaId == id).ToListAsync();
            var pacotes = await _context.PacotesDeAulas.Where(p => p.LocalAulaId == id).ToListAsync();
            _context.HorariosDisponiveis.RemoveRange(horarios);
            _context.PacotesDeAulas.RemoveRange(pacotes);
            _context.LocaisAula.Remove(local);
            await _context.SaveChangesAsync();

            TempData["SucessoPacote"] = $"Local “{local.Nome}” apagado.";
            return RedirectToAction("MeusLocais");
        }

        // Preço do pacote de aulas é só informativo por enquanto — o pagamento é combinado
        // direto com o professor (ex: Pix), sem cobrança nem controle de créditos pelo site.
        //
        // São VÁRIOS por local: 4 aulas com desconto pequeno, 12 com desconto grande. Cada um
        // é uma linha em PacoteDeAulas, adicionada e removida separadamente.
        [HttpPost]
        public async Task<IActionResult> AdicionarPacote(int localId, int quantidadeAulas, decimal preco)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula
                .Include(l => l.Pacotes)
                .FirstOrDefaultAsync(l => l.Id == localId && l.ProfessorId == professorId);

            if (local == null) return RedirectToAction("MeusLocais");

            // Pacote de 1 aula é a aula avulsa com outro nome, e preço zero anunciaria aula de
            // graça por engano de digitação.
            if (quantidadeAulas < 2 || preco <= 0)
            {
                TempData["ErroPacote"] = "O pacote precisa ter pelo menos 2 aulas e um preço maior que zero.";
                return RedirectToAction("MeusLocais");
            }

            // Mesma quantidade duas vezes deixaria o aluno escolhendo entre "4 aulas por R$ 400"
            // e "4 aulas por R$ 380" sem saber a diferença — que não existe. Reeditar o preço
            // do que já existe é o que a pessoa quer nesse caso.
            var jaTem = local.Pacotes.FirstOrDefault(p => p.QuantidadeAulas == quantidadeAulas);
            if (jaTem != null)
            {
                jaTem.Preco = preco;
                jaTem.Ativo = true;
                TempData["SucessoPacote"] = $"Já existia um pacote de {quantidadeAulas} aulas — o preço dele virou {preco:C}.";
            }
            else
            {
                _context.PacotesDeAulas.Add(new PacoteDeAulas
                {
                    LocalAulaId = local.Id,
                    QuantidadeAulas = quantidadeAulas,
                    Preco = preco,
                    Ativo = true,
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("MeusLocais");
        }

        [HttpPost]
        public async Task<IActionResult> RemoverPacote(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            // O join com LocalAula é a autorização: sem ele, qualquer professor logado apagaria
            // o pacote de qualquer outro só mandando o id.
            var pacote = await _context.PacotesDeAulas
                .Include(p => p.LocalAula)
                .FirstOrDefaultAsync(p => p.Id == id && p.LocalAula.ProfessorId == professorId);

            if (pacote != null)
            {
                _context.PacotesDeAulas.Remove(pacote);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusLocais");
        }

        [HttpPost]
        public async Task<IActionResult> AlternarLocal(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == id && l.ProfessorId == professorId);
            if (local != null)
            {
                local.Ativo = !local.Ativo;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusLocais");
        }

        // ---- Preço combinado com um aluno ----
        // O professor tem aluno antigo que nunca teve reajuste, filho de amigo, quem fechou o
        // mês. Antes disso ele corrigia o valor na mão em cada aula que marcava — e esquecia
        // numa. Agora o acordo fica guardado e a marcação já nasce com ele.

        [HttpPost]
        public async Task<IActionResult> SalvarPrecoDeAluno(int? alunoId, string? nomeAvulso, decimal preco, string? observacao)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            if (preco <= 0)
            {
                TempData["ErroPrecoAluno"] = "Informe um valor maior que zero. Pra voltar ao preço da tabela, use o botão de tirar o acordo.";
                return RedirectToAction("Dashboard");
            }

            // O aluno tem que ser aluno DESTE professor. Sem esta checagem, um id qualquer no
            // formulário criaria acordo com quem nunca pisou na quadra dele — e o nome que
            // aparece no painel sai daqui, não do que o navegador mandou.
            var ehMeuAluno = alunoId is int id
                ? await _context.Aulas.AnyAsync(a => a.ProfessorId == professorId && a.AlunoId == id)
                : !string.IsNullOrWhiteSpace(nomeAvulso)
                  && await _context.Aulas.AnyAsync(a => a.ProfessorId == professorId && a.NomeAlunoAvulso == nomeAvulso);

            if (!ehMeuAluno)
            {
                TempData["ErroPrecoAluno"] = "Não achei esse aluno na sua agenda.";
                return RedirectToAction("Dashboard");
            }

            var nome = string.IsNullOrWhiteSpace(nomeAvulso) ? null : nomeAvulso.Trim();
            var chave = PrecoDaAula.Chave(alunoId, nome);

            // Um acordo por aluno: gravar de novo é reajustar o que já existe, não empilhar
            // uma segunda linha que ninguém saberia qual vale.
            var existentes = await _context.PrecosDeAluno
                .Where(p => p.ProfessorId == professorId)
                .ToListAsync();
            var jaTem = existentes.FirstOrDefault(p => PrecoDaAula.Chave(p) == chave);

            if (jaTem != null)
            {
                jaTem.Preco = preco;
                jaTem.Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim();
            }
            else
            {
                _context.PrecosDeAluno.Add(new PrecoDeAluno
                {
                    ProfessorId = professorId.Value,
                    AlunoId = alunoId,
                    NomeAvulso = alunoId == null ? nome : null,
                    Preco = preco,
                    Observacao = string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(),
                });
            }

            await _context.SaveChangesAsync();
            TempData["SucessoPrecoAluno"] = $"Preço combinado salvo: {preco:C} na aula individual.";
            return RedirectToAction("Dashboard");
        }

        // Liga um aluno que foi marcado só pelo nome à conta dele no Padelizou.
        //
        // Existe porque a ordem natural é essa: o professor marca a aula de quem ainda não usa
        // o app, e a pessoa cria conta depois. Sem isto, o histórico ficava partido em dois —
        // as aulas antigas penduradas num nome solto, as novas na conta — e o aluno nunca via
        // no próprio app as aulas que já tinha feito.
        [HttpPost]
        public async Task<IActionResult> VincularAlunoAConta(string nomeAvulso, int alunoId)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var nome = (nomeAvulso ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nome))
            {
                TempData["ErroPrecoAluno"] = "Não achei esse aluno na sua agenda.";
                return RedirectToAction("Dashboard");
            }

            // Só as aulas DESTE professor, e só as que ainda não têm dono. Sem os dois
            // recortes, um id no formulário mexeria na agenda de outro professor ou
            // reescreveria o vínculo de uma aula que já estava certa.
            var aulas = await _context.Aulas
                .Where(a => a.ProfessorId == professorId && a.AlunoId == null && a.NomeAlunoAvulso == nome)
                .ToListAsync();

            if (aulas.Count == 0)
            {
                TempData["ErroPrecoAluno"] = $"Não achei aulas de \"{nome}\" sem conta ligada na sua agenda.";
                return RedirectToAction("Dashboard");
            }

            var conta = await _context.Jogadores.FirstOrDefaultAsync(j => j.Id == alunoId);
            if (conta == null)
            {
                TempData["ErroPrecoAluno"] = "Essa conta não existe.";
                return RedirectToAction("Dashboard");
            }

            // O professor não vira aluno de si mesmo.
            if (conta.Id == professorId)
            {
                TempData["ErroPrecoAluno"] = "Essa conta é a sua.";
                return RedirectToAction("Dashboard");
            }

            foreach (var aula in aulas) aula.AlunoId = conta.Id;

            // O acordo de preço segue junto: ele foi combinado com a PESSOA, e ficar preso ao
            // nome antigo faria o desconto sumir no dia em que ela criou conta.
            var acordos = await _context.PrecosDeAluno
                .Where(p => p.ProfessorId == professorId)
                .ToListAsync();
            var acordoDoNome = acordos.FirstOrDefault(p => PrecoDaAula.Chave(p) == PrecoDaAula.Chave(null, nome));
            var jaTinhaDaConta = acordos.Any(p => PrecoDaAula.Chave(p) == PrecoDaAula.Chave(conta.Id, null));

            if (acordoDoNome != null && !jaTinhaDaConta)
            {
                acordoDoNome.AlunoId = conta.Id;
                acordoDoNome.NomeAvulso = null;
            }

            await _context.SaveChangesAsync();

            TempData["SucessoPrecoAluno"] =
                $"Pronto: {aulas.Count} aula(s) de \"{nome}\" agora estão na conta de {conta.ComoChamar}. "
                + "Ele passa a ver o histórico no app dele.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> RemoverPrecoDeAluno(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var preco = await _context.PrecosDeAluno
                .FirstOrDefaultAsync(p => p.Id == id && p.ProfessorId == professorId);

            if (preco != null)
            {
                _context.PrecosDeAluno.Remove(preco);
                await _context.SaveChangesAsync();
                TempData["SucessoPrecoAluno"] = "Acordo removido. Esse aluno volta a pagar o preço do local.";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> MinhasCidades()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var vinculadas = (await _context.ProfessorCidades
                .Where(pc => pc.ProfessorId == professorId)
                .Select(pc => pc.CidadeId)
                .ToListAsync())
                .ToHashSet();

            // Aqui a lista é o catálogo INTEIRO de propósito — o professor precisa poder marcar
            // qualquer cidade, inclusive uma onde ainda não há ninguém. O que não pode é a
            // mesma cidade aparecer duas vezes, com o professor marcando uma e o aluno
            // procurando na outra.
            var itens = CidadesSemRepetir.Agrupar(await _context.Cidades.ToListAsync())
                .Select(c => new MinhaCidadeItem
                {
                    CidadeId = c.Id,
                    Nome = c.Nome,
                    Estado = c.Estado,
                    Vinculada = c.Ids.Any(vinculadas.Contains)
                })
                .ToList();

            return View(itens);
        }

        [HttpPost]
        public async Task<IActionResult> AtualizarCidades(List<int>? cidadeIds, string? novaCidadeNome, string? novaCidadeUf)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var idsSelecionados = (cidadeIds ?? new List<int>()).ToHashSet();

            if (!string.IsNullOrWhiteSpace(novaCidadeNome))
            {
                var nomeNormalizado = NomeDeCidade.Arrumar(novaCidadeNome);

                // A comparação de antes era só `ToLower()`: pegava a caixa e deixava o acento
                // passar, então "gravatai" criava uma cidade NOVA ao lado de "Gravataí". Agora
                // compara sem acento e sem caixa, do jeito que uma pessoa compararia.
                //
                // A conta roda na memória porque `Chave` não vira SQL. Cidades é catálogo — dezenas
                // de linhas, não milhares — então trazer a lista é mais barato que instalar
                // `unaccent` no banco só pra isto.
                var cidadesExistentes = await _context.Cidades.ToListAsync();
                var cidadeExistente = cidadesExistentes.FirstOrDefault(c => NomeDeCidade.Mesma(c.Nome, nomeNormalizado));

                if (cidadeExistente == null)
                {
                    cidadeExistente = new Cidade
                    {
                        Nome = nomeNormalizado,
                        Estado = NomeDeCidade.ArrumarEstado(novaCidadeUf)
                    };
                    _context.Cidades.Add(cidadeExistente);
                    await _context.SaveChangesAsync();
                }

                idsSelecionados.Add(cidadeExistente.Id);
            }

            var vinculosAtuais = await _context.ProfessorCidades
                .Where(pc => pc.ProfessorId == professorId)
                .ToListAsync();

            var vinculosParaRemover = vinculosAtuais.Where(v => !idsSelecionados.Contains(v.CidadeId));
            _context.ProfessorCidades.RemoveRange(vinculosParaRemover);

            var idsJaVinculados = vinculosAtuais.Select(v => v.CidadeId).ToHashSet();
            foreach (var cidadeId in idsSelecionados.Where(id => !idsJaVinculados.Contains(id)))
            {
                _context.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = professorId.Value, CidadeId = cidadeId });
            }

            await _context.SaveChangesAsync();

            TempData["Sucesso"] = "Cidades atualizadas com sucesso.";
            return RedirectToAction("MinhasCidades");
        }

        [HttpGet]
        public async Task<IActionResult> MeusHorarios()
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            ViewBag.Locais = await _context.LocaisAula
                .Where(l => l.ProfessorId == professorId && l.Ativo)
                .ToListAsync();

            var horarios = await _context.HorariosDisponiveis
                .Include(h => h.LocalAula)
                .Where(h => h.ProfessorId == professorId)
                .OrderBy(h => h.DiaSemana)
                .ThenBy(h => h.HoraInicio)
                .ToListAsync();

            return View(horarios);
        }

        // Vários dias de uma vez ("segunda, quarta e sexta das 8h ao meio-dia") — um dia por
        // vez tornava o cadastro da semana uma novela de sete capítulos. A decisão de quais
        // criar/reativar/pular mora em Services/NovoHorarioDoProfessor, testável sem banco.
        [HttpPost]
        public async Task<IActionResult> CriarHorario(int localAulaId, int[] diasSemana, TimeSpan horaInicio, TimeSpan horaFim, int duracaoMinutos)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var local = await _context.LocaisAula.FirstOrDefaultAsync(l => l.Id == localAulaId && l.ProfessorId == professorId);
            if (local == null)
            {
                return RedirectToAction("MeusHorarios");
            }

            var duracao = duracaoMinutos <= 0 ? DuracaoPadraoMinutos : duracaoMinutos;
            var existentes = await _context.HorariosDisponiveis
                .Where(h => h.ProfessorId == professorId)
                .ToListAsync();

            var plano = NovoHorarioDoProfessor.Planejar(
                diasSemana ?? Array.Empty<int>(), horaInicio, horaFim, duracao, localAulaId, existentes);

            if (!plano.Valido)
            {
                TempData["ErroHorario"] = plano.Erro;
                return RedirectToAction("MeusHorarios");
            }

            foreach (var dia in plano.DiasParaCriar)
            {
                _context.HorariosDisponiveis.Add(new HorarioDisponivel
                {
                    ProfessorId = professorId.Value,
                    LocalAulaId = localAulaId,
                    DiaSemana = dia,
                    HoraInicio = horaInicio,
                    HoraFim = horaFim,
                    DuracaoMinutos = duracao,
                    Ativo = true
                });
            }

            foreach (var dia in plano.DiasParaReativar)
            {
                var pausado = existentes.First(h => h.LocalAulaId == localAulaId && h.DiaSemana == dia
                    && h.HoraInicio == horaInicio && h.HoraFim == horaFim);
                pausado.Ativo = true;
            }

            await _context.SaveChangesAsync();
            TempData["SucessoHorario"] = NovoHorarioDoProfessor.Resumo(plano);

            return RedirectToAction("MeusHorarios");
        }

        [HttpPost]
        public async Task<IActionResult> AlternarHorario(int id)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var horario = await _context.HorariosDisponiveis.FirstOrDefaultAsync(h => h.Id == id && h.ProfessorId == professorId);
            if (horario != null)
            {
                horario.Ativo = !horario.Ativo;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("MeusHorarios");
        }

        // Para alunos que combinaram a aula fora do sistema (não têm conta). A aula nasce
        // direto como "Confirmada" — sem o fluxo de solicitação/aceite.

        private const int MinSemanasRecorrencia = 2;
        private const int MaxSemanasRecorrencia = 26;

        // Configuração da política de cancelamento (tela do professor).
        [HttpPost]
        public async Task<IActionResult> SalvarPolitica(int horasMinimas, bool cobraFaltaSemAviso, string? textoPolitica)
        {
            var professorId = await ObterProfessorLogadoAsync();
            if (professorId == null) return RedirectToAction("Perfil", "Auth");

            var professor = await _context.Jogadores.FindAsync(professorId);
            if (professor == null) return NotFound();

            professor.HorasMinimasCancelamento = Math.Clamp(horasMinimas, 0, 168); // teto de 1 semana
            professor.CobraFaltaSemAviso = cobraFaltaSemAviso;
            professor.PoliticaCancelamentoTexto = string.IsNullOrWhiteSpace(textoPolitica) ? null : textoPolitica.Trim();

            await _context.SaveChangesAsync();
            TempData["Sucesso"] = "Política de cancelamento atualizada.";
            return RedirectToAction("Dashboard");
        }

    }
}
