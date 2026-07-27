using System.Linq;
using Padelizou.Models;

namespace Padelizou.Data;

// Semeia um conjunto de dados de demonstração coerente (clubes, times, jogadores e torneios
// com resultados) para a produção não ficar vazia. Roda só quando NÃO há jogadores ainda —
// então nunca sobrescreve dados reais. Chamado no startup (Program.cs), dentro do try/catch.
public static class DadosDemo
{
    public static void Seed(DbPadelContext db)
    {
        if (db.Jogadores.Any()) return; // já tem dados (demo ou reais): não mexe

        var agora = DateTime.Now;

        // Clubes
        var arena = new Clube { Nome = "Arena Beira Rio", Endereco = "Av. Beira Rio, 100 - Porto Alegre/RS", Contato = "(51) 3000-0001" };
        var garden = new Clube { Nome = "Padel Garden", Endereco = "Rua das Quadras, 200 - Porto Alegre/RS", Contato = "(51) 3000-0002" };
        var smash = new Clube { Nome = "Smash Padel Club", Endereco = "Av. Central, 300 - Canoas/RS", Contato = "(51) 3000-0003" };
        db.Clubes.AddRange(arena, garden, smash);
        db.SaveChanges();

        // Times (com clube sede)
        var nata = new Time { Nome = "Nata Padel", ClubeId = arena.Id };
        var feras = new Time { Nome = "Clube dos Feras", ClubeId = garden.Id };
        db.Times.AddRange(nata, feras);
        db.SaveChanges();

        Jogador NovoJogador(string nome, string cpf, int timeId) => new Jogador
        {
            Nome = nome,
            Cpf = cpf,
            Cidade = "Porto Alegre",
            Estado = "RS",
            TimeId = timeId
        };

        // Jogadores — 4 no Nata Padel, 4 no Clube dos Feras
        var rafael = NovoJogador("Rafael Souza", "20000000001", nata.Id);
        var diego = NovoJogador("Diego Martins", "20000000002", nata.Id);
        var bruno = NovoJogador("Bruno Alves", "20000000003", nata.Id);
        var lucas = NovoJogador("Lucas Pereira", "20000000004", nata.Id);
        var fernanda = NovoJogador("Fernanda Lima", "20000000005", feras.Id);
        var camila = NovoJogador("Camila Rocha", "20000000006", feras.Id);
        var juliana = NovoJogador("Juliana Costa", "20000000007", feras.Id);
        var marina = NovoJogador("Marina Santos", "20000000008", feras.Id);
        db.Jogadores.AddRange(rafael, diego, bruno, lucas, fernanda, camila, juliana, marina);
        db.SaveChanges();

        // Administradores dos times (um time pode ter vários; no seed basta um cada).
        db.TimeAdministradores.AddRange(
            new TimeAdministrador { TimeId = nata.Id, JogadorId = rafael.Id, ConcedidoPorId = rafael.Id, ConcedidoEm = DateTime.Now },
            new TimeAdministrador { TimeId = feras.Id, JogadorId = fernanda.Id, ConcedidoPorId = fernanda.Id, ConcedidoEm = DateTime.Now });
        db.SaveChanges();

        // Cria um torneio com uma categoria Open Masculino e 4 duplas com a fase alcançada.
        void CriarTorneio(string nome, string codigo, int clubeId, DateTime data, string status,
            (Jogador a, Jogador b, string fase)[] duplas)
        {
            var t = new Torneio
            {
                Nome = nome,
                Codigo = codigo,
                ClubeId = clubeId,
                DataInicio = data,
                Status = status,
                Formato = "Padrao"
            };
            db.Torneios.Add(t);
            db.SaveChanges();

            var cat = new Categoria { Nome = "Categoria Open Masculino", Codigo = "1CatM", TorneioId = t.Id };
            db.Categorias.Add(cat);
            db.SaveChanges();

            foreach (var (a, b, fase) in duplas)
            {
                db.Duplas.Add(new Dupla { CategoriaId = cat.Id, Jogador1Id = a.Id, Jogador2Id = b.Id, UltimaFase = fase });
            }
            db.SaveChanges();
        }

        // Datas espalhadas pra alimentar o indicador de movimento (subiu/desceu no último mês).
        CriarTorneio("Aberto de Verão 2026", "DEMOVER", arena.Id, agora.AddMonths(-3), "Finalizado", new[]
        {
            (rafael, diego, "Campeao"),
            (bruno, lucas, "Final"),
            (fernanda, camila, "Semifinal"),
            (juliana, marina, "Grupos"),
        });
        CriarTorneio("Copa Padelizou 2026", "DEMOCOPA", garden.Id, agora.AddDays(-20), "Finalizado", new[]
        {
            (rafael, diego, "Campeao"),
            (fernanda, camila, "Final"),
            (bruno, lucas, "Semifinal"),
            (juliana, marina, "Grupos"),
        });
        CriarTorneio("Torneio de Inverno 2026", "DEMOINV", arena.Id, agora.AddDays(-3), "Fase de Grupos", new[]
        {
            (rafael, diego, "Grupos"),
            (fernanda, camila, "Grupos"),
            (bruno, lucas, "Grupos"),
            (juliana, marina, "Grupos"),
        });
    }

    // Dados extras pra apresentação de vendas (2026-07-25): mais jogadores, torneios variados
    // (aberto + ao vivo) e elogios distribuídos, além de deixar o Felipe dono/admin de todos os
    // clubes de demo e organizador de um torneio novo. Diferente do Seed() acima, roda mesmo com
    // dados já existentes — a idempotência é pelo Codigo "PRIMAVERA26" (ver Program.cs).
    public static void SeedApresentacao(DbPadelContext db)
    {
        var agora = DateTime.Now;
        var felipe = db.Jogadores.FirstOrDefault(j => j.Email == "felipe.bonamigo@gmail.com");

        var arena = db.Clubes.First(c => c.Nome == "Arena Beira Rio");
        var garden = db.Clubes.First(c => c.Nome == "Padel Garden");
        var smash = db.Clubes.First(c => c.Nome == "Smash Padel Club");

        // Felipe dono/admin de todos os clubes de demo, pra apresentação mostrar tudo.
        if (felipe != null)
        {
            if (arena.DonoId == null) arena.DonoId = felipe.Id;
            if (!db.ClubeAdministradores.Any(a => a.ClubeId == garden.Id && a.JogadorId == felipe.Id))
                db.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = garden.Id, JogadorId = felipe.Id });
            if (!db.ClubeAdministradores.Any(a => a.ClubeId == smash.Id && a.JogadorId == felipe.Id))
                db.ClubeAdministradores.Add(new ClubeAdministrador { ClubeId = smash.Id, JogadorId = felipe.Id });
            db.SaveChanges();
        }

        // Mais jogadores, pra Ranking/Buscar Jogadores terem volume de verdade.
        (string nome, string cpf, string cidade, bool professor)[] novosDados =
        {
            ("Gustavo Ramos", "30000000001", "Porto Alegre", false),
            ("Thiago Nunes", "30000000002", "Canoas", false),
            ("Mariana Duarte", "30000000003", "Gravataí", false),
            ("Patrícia Vieira", "30000000004", "Novo Hamburgo", false),
            ("Rodrigo Fontoura", "30000000005", "Porto Alegre", true),
            ("Leandro Cardoso", "30000000006", "Porto Alegre", false),
            ("Aline Barcellos", "30000000007", "Canoas", false),
            ("Vinícius Moraes", "30000000008", "Porto Alegre", false),
            ("Bianca Ferraz", "30000000009", "Viamão", false),
            ("Eduardo Castro", "30000000010", "Porto Alegre", true),
            ("Larissa Prado", "30000000011", "Porto Alegre", false),
            ("Felipe Azevedo", "30000000012", "Cachoeirinha", false),
            ("Renata Xavier", "30000000013", "Porto Alegre", false),
            ("Marcelo Trindade", "30000000014", "Canoas", false),
            ("Débora Sanches", "30000000015", "Porto Alegre", false),
        };
        foreach (var (nome, cpf, cidade, professor) in novosDados)
        {
            if (db.Jogadores.Any(j => j.Cpf == cpf)) continue;
            db.Jogadores.Add(new Jogador { Nome = nome, Cpf = cpf, Cidade = cidade, Estado = "RS", IsProfessor = professor });
        }
        db.SaveChanges();

        var pool = db.Jogadores.Where(j => felipe == null || j.Id != felipe.Id).OrderBy(j => j.Id).ToList();

        // Torneio A: aberto pra inscrição, com 3 categorias, Felipe como organizador.
        var aberto = new Torneio
        {
            Nome = "Aberto de Primavera 2026",
            Codigo = "PRIMAVERA26",
            ClubeId = arena.Id,
            DataInicio = agora.AddDays(12),
            Status = "Inscrições Abertas",
            Formato = "Padrao",
            PrecoInscricao = 60,
            LocalTorneio = arena.Nome,
        };
        db.Torneios.Add(aberto);
        db.SaveChanges();

        if (felipe != null)
        {
            db.TorneioOrganizadores.Add(new TorneioOrganizador { TorneioId = aberto.Id, JogadorId = felipe.Id, NivelAcesso = "Criador" });
        }

        var catMasc = new Categoria { Nome = "Categoria Open Masculino", Codigo = "1CatM", TorneioId = aberto.Id };
        var catFem = new Categoria { Nome = "Categoria Open Feminina", Codigo = "1CatF", TorneioId = aberto.Id };
        var catMista = new Categoria { Nome = "Categoria Mista A", Codigo = "MISTA-A", TorneioId = aberto.Id };
        db.Categorias.AddRange(catMasc, catFem, catMista);
        db.SaveChanges();

        db.Duplas.AddRange(
            new Dupla { CategoriaId = catMasc.Id, Jogador1Id = pool[0].Id, Jogador2Id = pool[1].Id },
            new Dupla { CategoriaId = catMasc.Id, Jogador1Id = pool[2].Id, Jogador2Id = pool[3].Id },
            new Dupla { CategoriaId = catMasc.Id, Jogador1Id = pool[4].Id, Jogador2Id = pool[5].Id },
            new Dupla { CategoriaId = catFem.Id, Jogador1Id = pool[6].Id, Jogador2Id = pool[7].Id },
            new Dupla { CategoriaId = catFem.Id, Jogador1Id = pool[8].Id, Jogador2Id = pool[9].Id },
            new Dupla { CategoriaId = catMista.Id, Jogador1Id = pool[10].Id, Jogador2Id = pool[11].Id }
        );
        db.SaveChanges();

        // Torneio B: em andamento, com partidas Ao Vivo (placar parcial) pra demonstração.
        var aoVivo = new Torneio
        {
            Nome = "Torneio dos Amigos 2026",
            Codigo = "AMIGOS26",
            ClubeId = smash.Id,
            DataInicio = agora,
            Status = "Fase de Grupos",
            Formato = "Padrao",
            PrecoInscricao = 40,
            LocalTorneio = smash.Nome,
        };
        db.Torneios.Add(aoVivo);
        db.SaveChanges();

        var catAoVivo = new Categoria { Nome = "Categoria Open Masculino", Codigo = "1CatM", TorneioId = aoVivo.Id };
        db.Categorias.Add(catAoVivo);
        db.SaveChanges();

        var duplaA = new Dupla { CategoriaId = catAoVivo.Id, Jogador1Id = pool[12].Id, Jogador2Id = pool[13].Id, UltimaFase = "Grupos" };
        var duplaB = new Dupla { CategoriaId = catAoVivo.Id, Jogador1Id = pool[14].Id, Jogador2Id = pool[15].Id, UltimaFase = "Grupos" };
        var duplaC = new Dupla { CategoriaId = catAoVivo.Id, Jogador1Id = pool[16].Id, Jogador2Id = pool[17].Id, UltimaFase = "Grupos" };
        var duplaD = new Dupla { CategoriaId = catAoVivo.Id, Jogador1Id = pool[18].Id, Jogador2Id = pool[19].Id, UltimaFase = "Grupos" };
        db.Duplas.AddRange(duplaA, duplaB, duplaC, duplaD);
        db.SaveChanges();

        db.Partidas.Add(new Partida
        {
            CategoriaId = catAoVivo.Id,
            TorneioId = aoVivo.Id,
            Dupla1Id = duplaA.Id,
            Dupla2Id = duplaB.Id,
            Codigo = "AMV001",
            Status = "AoVivo",
            Fase = "Fase de Grupos",
            SendoTransmitida = true,
            NomeQuadra = "Quadra Central",
            SetsDupla1 = 1,
            SetsDupla2 = 0,
            GamesDupla1 = 4,
            GamesDupla2 = 3,
            HorarioInicioReal = agora.AddMinutes(-25),
        });
        db.Partidas.Add(new Partida
        {
            CategoriaId = catAoVivo.Id,
            TorneioId = aoVivo.Id,
            Dupla1Id = duplaC.Id,
            Dupla2Id = duplaD.Id,
            Codigo = "AMV002",
            Status = "Agendada",
            Fase = "Fase de Grupos",
            HorarioPrevisto = agora.AddHours(1),
        });
        db.SaveChanges();

        // Elogios distribuídos entre os jogadores (inclusive alguns pro Felipe), pra tela de
        // perfil já nascer com badges na apresentação.
        var tiposElogio = Padelizou.Services.CatalogoElogios.Todos;
        var rng = new Random(2026);
        var todosComFelipe = felipe != null ? pool.Append(felipe).ToList() : pool;
        foreach (var alvo in todosComFelipe)
        {
            int quantidade = rng.Next(0, 4); // 0 a 3 elogios por jogador
            var quemJaDeu = new HashSet<int>();
            for (int i = 0; i < quantidade; i++)
            {
                var candidatos = todosComFelipe.Where(j => j.Id != alvo.Id && !quemJaDeu.Contains(j.Id)).ToList();
                if (candidatos.Count == 0) break;
                var de = candidatos[rng.Next(candidatos.Count)];
                quemJaDeu.Add(de.Id);
                var tipo = tiposElogio[rng.Next(tiposElogio.Count)];
                if (!db.Elogios.Any(e => e.DeJogadorId == de.Id && e.ParaJogadorId == alvo.Id && e.Tipo == tipo.Codigo))
                {
                    db.Elogios.Add(new Elogio { DeJogadorId = de.Id, ParaJogadorId = alvo.Id, Tipo = tipo.Codigo });
                }
            }
        }
        db.SaveChanges();
    }

    // Fecha o conjunto de cenários do ambiente de testes: além dos torneios (finalizado, em
    // andamento e com inscrições abertas), um professor em quem dá pra marcar aula de verdade
    // e um clube em que dá pra reservar quadra. Sem estes dois, metade do sistema fica sem
    // porta de entrada pra quem chega pra testar.
    //
    // Idempotente pelos nomes: rodar de novo não duplica nada.
    public static void SeedProfessorEClube(DbPadelContext db)
    {
        SemearProfessor(db);
        SemearClubeComQuadras(db);
    }

    private const string NomeLocalAula = "Arena Beira Rio — Quadra 3";

    private static void SemearProfessor(DbPadelContext db)
    {
        if (db.LocaisAula.Any(l => l.Nome == NomeLocalAula)) return;

        // Rodrigo já nasce com IsProfessor no SeedApresentacao, mas sem local nem horário
        // ninguém consegue marcar com ele — o perfil aparecia e o botão não levava a nada.
        var professor = db.Jogadores.FirstOrDefault(j => j.Cpf == "30000000005");
        if (professor == null) return;

        professor.IsProfessor = true;
        professor.Celular ??= "51999990005";
        professor.ApresentacaoProfessor ??=
            "Dou aula há 8 anos, de quem nunca pegou na raquete a quem já joga torneio. " +
            "Foco em consistência de fundo de quadra e saída de parede.";
        professor.ExperienciaProfessor ??= "8 anos dando aula · ex-atleta 2ª categoria";
        professor.HorasMinimasCancelamento = 24;
        db.SaveChanges();

        var local = new LocalAula
        {
            ProfessorId = professor.Id,
            Nome = NomeLocalAula,
            Endereco = "Av. Beira Rio, 100 - Porto Alegre/RS",
            PrecoPadrao = 90m,
            CustoPorAula = 35m,
            PacoteAtivo = true,
            PacoteQuantidadeAulas = 4,
            PacotePreco = 320m,
        };
        db.LocaisAula.Add(local);
        db.SaveChanges();

        // Terça, quinta e sábado — o suficiente pra agenda mostrar dia cheio e dia vazio.
        foreach (var (dia, inicio, fim) in new[]
        {
            (2, new TimeSpan(7, 0, 0), new TimeSpan(11, 0, 0)),
            (4, new TimeSpan(7, 0, 0), new TimeSpan(11, 0, 0)),
            (4, new TimeSpan(18, 0, 0), new TimeSpan(22, 0, 0)),
            (6, new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0)),
        })
        {
            db.HorariosDisponiveis.Add(new HorarioDisponivel
            {
                ProfessorId = professor.Id,
                LocalAulaId = local.Id,
                DiaSemana = dia,
                HoraInicio = inicio,
                HoraFim = fim,
                DuracaoMinutos = 60,
            });
        }
        db.SaveChanges();
    }

    private static void SemearClubeComQuadras(DbPadelContext db)
    {
        var clube = db.Clubes.FirstOrDefault(c => c.Nome == "Arena Beira Rio");
        if (clube == null || db.QuadrasClube.Any(q => q.ClubeId == clube.Id)) return;

        clube.MarcacaoHorariosAtiva = true;
        clube.HorasMinimasCancelamento = 12;
        clube.CobraNoShow = true;
        clube.PoliticaCancelamentoTexto ??=
            "Cancelamento até 12h antes é livre. Depois disso, ou faltando sem avisar, a reserva é cobrada.";
        db.SaveChanges();

        var quadras = new[]
        {
            new QuadraClube { ClubeId = clube.Id, Nome = "Quadra 1 (coberta)" },
            new QuadraClube { ClubeId = clube.Id, Nome = "Quadra 2 (coberta)" },
            new QuadraClube { ClubeId = clube.Id, Nome = "Quadra 3 (descoberta)" },
        };
        db.QuadrasClube.AddRange(quadras);
        db.SaveChanges();

        // Segunda a sexta com horário nobre mais caro à noite, e sábado de manhã — é assim
        // que um clube de verdade precifica, e é o que faz o mapa de ocupação ter o que mostrar.
        foreach (var quadra in quadras)
        {
            for (int dia = 1; dia <= 5; dia++)
            {
                AdicionarRegra(db, clube.Id, quadra.Id, dia, new TimeSpan(7, 0, 0), new TimeSpan(17, 0, 0), 80m);
                AdicionarRegra(db, clube.Id, quadra.Id, dia, new TimeSpan(17, 0, 0), new TimeSpan(23, 0, 0), 120m);
            }
            AdicionarRegra(db, clube.Id, quadra.Id, 6, new TimeSpan(8, 0, 0), new TimeSpan(14, 0, 0), 100m);
        }
        db.SaveChanges();
    }

    private static void AdicionarRegra(DbPadelContext db, int clubeId, int quadraId, int dia,
        TimeSpan inicio, TimeSpan fim, decimal preco)
    {
        db.HorariosMarcacaoDisponivel.Add(new HorarioMarcacaoDisponivel
        {
            ClubeId = clubeId,
            QuadraClubeId = quadraId,
            DiaSemana = dia,
            HoraInicio = inicio,
            HoraFim = fim,
            DuracaoMinutos = 60,
            Preco = preco,
        });
    }
}
