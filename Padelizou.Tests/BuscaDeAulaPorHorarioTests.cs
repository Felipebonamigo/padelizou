using Microsoft.AspNetCore.Mvc;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using System.Text.Json;
using Xunit;

namespace Padelizou.Tests;

// Marcar aula deixou de ser uma escada (professor -> local -> horário) e virou uma grade só:
// a tela recebe TUDO o que a cidade tem livre e filtra por qualquer ponta — dia, horário,
// professor ou local.
//
// O que este arquivo protege é o lado do servidor dessa mudança: a lista tem que chegar
// completa (senão o filtro por horário nasce cego), só com horário de verdade livre, e sem
// oferecer professor que não tem nada a oferecer.
public class BuscaDeAulaPorHorarioTests
{
    private static (DbPadelContext ctx, Cidade cidade, Jogador aluno) Cenario()
    {
        var ctx = TestInfra.NovoContexto();

        var cidade = new Cidade { Nome = "Gravataí", Estado = "RS" };
        ctx.Cidades.Add(cidade);

        var aluno = new Jogador { Nome = "Medina", Login = "medina", Cpf = "99900000009" };
        ctx.Jogadores.Add(aluno);
        ctx.SaveChanges();

        return (ctx, cidade, aluno);
    }

    // Um professor completo: cidade, local ativo e uma janela na grade. `diaDaSemana` é o
    // DayOfWeek — os testes usam o dia de amanhã pra nunca dependerem de que horas são agora.
    private static (Jogador professor, LocalAula local) Professor(
        DbPadelContext ctx, Cidade cidade, string nome, DateTime dia,
        TimeSpan inicio, TimeSpan fim, int duracao = 60, string local = "Chakra")
    {
        var professor = new Jogador
        {
            Nome = nome, Login = nome.ToLowerInvariant(), Cpf = Guid.NewGuid().ToString("N")[..11],
            IsProfessor = true,
        };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        ctx.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = professor.Id, CidadeId = cidade.Id });

        var localAula = new LocalAula
        {
            ProfessorId = professor.Id, Nome = local, PrecoPadrao = 120, Ativo = true,
        };
        ctx.LocaisAula.Add(localAula);
        ctx.SaveChanges();

        ctx.HorariosDisponiveis.Add(new HorarioDisponivel
        {
            ProfessorId = professor.Id, LocalAulaId = localAula.Id,
            DiaSemana = (int)dia.DayOfWeek, HoraInicio = inicio, HoraFim = fim,
            DuracaoMinutos = duracao, Ativo = true,
        });
        ctx.SaveChanges();

        return (professor, localAula);
    }

    private static async Task<JsonElement> BuscarAsync(DbPadelContext ctx, int cidadeId, int alunoId)
    {
        var corpo = Assert.IsType<JsonResult>(
            await TestInfra.NovoAulasController(ctx, alunoId).ObterOfertas(cidadeId)).Value;

        // Serializar em vez de ler o objeto anônimo por reflexão: é exatamente o que o
        // navegador recebe, e é do navegador que o filtro cruzado vive.
        return JsonDocument.Parse(JsonSerializer.Serialize(corpo)).RootElement;
    }

    private static List<JsonElement> Lista(JsonElement raiz, string campo) =>
        raiz.GetProperty(campo).EnumerateArray().ToList();

    // Amanhã, pra o horário nunca cair no passado por causa da hora em que o teste roda.
    private static DateTime Amanha => DateTime.Today.AddDays(1);

    // ===================== A GRADE INTEIRA CHEGA DE UMA VEZ =====================

    [Fact]
    public async Task A_cidade_devolve_os_horarios_de_TODOS_os_professores()
    {
        // É este o coração da mudança: quem procura pelo horário não escolheu professor
        // nenhum ainda, então a lista tem que vir com os dois desde o primeiro carregamento.
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        Professor(ctx, cidade, "Jonatas", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(11, 0, 0));
        Professor(ctx, cidade, "Bruna", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), local: "Batata");

        var corpo = await BuscarAsync(ctx, cidade.Id, aluno.Id);

        Assert.Equal(2, Lista(corpo, "professores").Count);
        Assert.Equal(2, Lista(corpo, "locais").Count);
        // Jonatas rende dois slots de uma hora (9h e 10h); Bruna, um.
        Assert.Equal(3, Lista(corpo, "ofertas").Count);
    }

    [Fact]
    public async Task Cada_oferta_carrega_professor_local_e_horario_juntos()
    {
        // Sem os três dentro da MESMA linha não existe filtro cruzado: escolher as 9h não teria
        // como dizer quem dá aula às 9h.
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        var (professor, local) = Professor(ctx, cidade, "Jonatas", Amanha,
            new TimeSpan(9, 0, 0), new TimeSpan(10, 30, 0), duracao: 90);

        var oferta = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "ofertas").Single();

        Assert.Equal(professor.Id, oferta.GetProperty("professorId").GetInt32());
        Assert.Equal(local.Id, oferta.GetProperty("localId").GetInt32());
        Assert.Equal(Amanha.AddHours(9).ToString("yyyy-MM-ddTHH:mm:ss"), oferta.GetProperty("valor").GetString());
        Assert.Equal("1h30", oferta.GetProperty("duracao").GetString());
    }

    [Fact]
    public async Task O_local_leva_junto_o_preco_e_os_pacotes()
    {
        // O local agora pode ser escolhido POR ÚLTIMO (quem veio pelo horário chega nele no
        // fim). Se preço e pacotes não viajassem junto, a tela teria que voltar ao servidor
        // justamente na hora de mostrar quanto custa.
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        var (_, local) = Professor(ctx, cidade, "Jonatas", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        local.PrecoDupla = 150;
        ctx.PacotesDeAulas.Add(new PacoteDeAulas
        {
            LocalAulaId = local.Id, QuantidadeAulas = 4, Preco = 400, Ativo = true,
        });
        ctx.SaveChanges();

        var noJson = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "locais").Single();

        Assert.Equal(120, noJson.GetProperty("precoPadrao").GetDecimal());
        Assert.Equal(150, noJson.GetProperty("precoDupla").GetDecimal());
        Assert.Equal(JsonValueKind.Null, noJson.GetProperty("precoTrio").ValueKind);
        Assert.Equal(4, noJson.GetProperty("pacotes").EnumerateArray().Single().GetProperty("quantidadeAulas").GetInt32());
    }

    // ===================== O QUE NÃO PODE APARECER =====================

    [Fact]
    public async Task Horario_ja_marcado_nao_e_oferecido()
    {
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        var (professor, local) = Professor(ctx, cidade, "Jonatas", Amanha,
            new TimeSpan(9, 0, 0), new TimeSpan(11, 0, 0));

        ctx.Aulas.Add(new Aula
        {
            ProfessorId = professor.Id, AlunoId = aluno.Id, LocalAulaId = local.Id,
            DataHora = Amanha.AddHours(9), DuracaoMinutos = 60, Preco = 120, Status = "Confirmada",
        });
        ctx.SaveChanges();

        var horarios = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "ofertas")
            .Select(o => o.GetProperty("valor").GetString())
            .ToList();

        Assert.Equal(new[] { Amanha.AddHours(10).ToString("yyyy-MM-ddTHH:mm:ss") }, horarios);
    }

    [Fact]
    public async Task Aula_de_duas_horas_derruba_os_DOIS_slots_de_uma_hora()
    {
        // Comparar só o horário de início oferecia ao aluno o segundo slot de uma aula que já
        // ocupava as duas horas. É a mesma trava do resto do sistema (DuracaoDaAula.Conflita) —
        // o que importa aqui é que ela sobreviveu à mudança de tela.
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        var (professor, local) = Professor(ctx, cidade, "Jonatas", Amanha,
            new TimeSpan(9, 0, 0), new TimeSpan(12, 0, 0));

        ctx.Aulas.Add(new Aula
        {
            ProfessorId = professor.Id, AlunoId = aluno.Id, LocalAulaId = local.Id,
            DataHora = Amanha.AddHours(9), DuracaoMinutos = 120, Preco = 120, Status = "Pendente",
        });
        ctx.SaveChanges();

        var horarios = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "ofertas")
            .Select(o => o.GetProperty("valor").GetString())
            .ToList();

        Assert.Equal(new[] { Amanha.AddHours(11).ToString("yyyy-MM-ddTHH:mm:ss") }, horarios);
    }

    [Fact]
    public async Task Professor_sem_nenhum_horario_livre_some_da_lista()
    {
        // Antes ele aparecia no primeiro select e a pessoa descobria o vazio no TERCEIRO passo.
        // Agora a lista de professores é a lista de quem realmente tem o que oferecer.
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        Professor(ctx, cidade, "Jonatas", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));

        // A Bruna tem cidade e local, mas nenhuma janela na grade.
        var bruna = new Jogador { Nome = "Bruna", Login = "bruna", Cpf = "99900000008", IsProfessor = true };
        ctx.Jogadores.Add(bruna);
        ctx.SaveChanges();
        ctx.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = bruna.Id, CidadeId = cidade.Id });
        ctx.LocaisAula.Add(new LocalAula { ProfessorId = bruna.Id, Nome = "Vazio", PrecoPadrao = 100, Ativo = true });
        ctx.SaveChanges();

        var nomes = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "professores")
            .Select(p => p.GetProperty("nome").GetString())
            .ToList();

        Assert.Equal(new[] { "Jonatas" }, nomes);
    }

    [Fact]
    public async Task Professor_de_outra_cidade_nao_entra()
    {
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        var outra = new Cidade { Nome = "Canoas", Estado = "RS" };
        ctx.Cidades.Add(outra);
        ctx.SaveChanges();

        Professor(ctx, cidade, "Jonatas", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        Professor(ctx, outra, "Bruna", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), local: "Batata");

        var nomes = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "professores")
            .Select(p => p.GetProperty("nome").GetString())
            .ToList();

        Assert.Equal(new[] { "Jonatas" }, nomes);
    }

    // Duas linhas de catálogo pra "Gravataí" é o normal deste banco. Escolher uma e perder
    // metade dos professores era um defeito MUDO — a tela abria, só vinha gente a menos.
    [Fact]
    public async Task Cidade_repetida_no_catalogo_traz_os_professores_das_duas_linhas()
    {
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        var gemea = new Cidade { Nome = "GRAVATAI", Estado = "RS" };
        ctx.Cidades.Add(gemea);
        ctx.SaveChanges();

        Professor(ctx, cidade, "Jonatas", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        Professor(ctx, gemea, "Bruna", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0), local: "Batata");

        var nomes = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "professores")
            .Select(p => p.GetProperty("nome").GetString())
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "Bruna", "Jonatas" }, nomes);
    }

    [Fact]
    public async Task Local_desativado_nao_oferece_horario()
    {
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        var (_, local) = Professor(ctx, cidade, "Jonatas", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        local.Ativo = false;
        ctx.SaveChanges();

        var corpo = await BuscarAsync(ctx, cidade.Id, aluno.Id);

        Assert.Empty(Lista(corpo, "ofertas"));
        Assert.Empty(Lista(corpo, "locais"));
    }

    // ===================== A GRADE, SEM BANCO NO MEIO =====================

    // O gerador é a única cópia da regra "quais horários existem" — antes ela morava dentro do
    // controller, escopada a um professor só. Aqui ele é testado direto.

    [Fact]
    public void A_agenda_que_trava_o_horario_e_a_do_PROFESSOR_nao_a_do_local()
    {
        // O mesmo professor não pode estar em duas quadras às 9h. Filtrar o ocupado por local
        // deixaria a segunda quadra dele parecendo livre.
        var regras = new[]
        {
            new HorarioDisponivel { ProfessorId = 1, LocalAulaId = 10, DiaSemana = (int)Amanha.DayOfWeek,
                HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(10, 0, 0), DuracaoMinutos = 60, Ativo = true },
            new HorarioDisponivel { ProfessorId = 1, LocalAulaId = 20, DiaSemana = (int)Amanha.DayOfWeek,
                HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(10, 0, 0), DuracaoMinutos = 60, Ativo = true },
        };

        // Aula no local 10; o local 20 não pode sobrar livre no mesmo horário.
        var ocupadas = new[] { (ProfessorId: 1, DataHora: Amanha.AddHours(9), DuracaoMinutos: 60) };

        var ofertas = OfertasDeAula.Gerar(regras, ocupadas, DateTime.Now, 14);

        Assert.Empty(ofertas);
    }

    [Fact]
    public void A_grade_de_outro_professor_nao_e_travada_pela_aula_alheia()
    {
        var regras = new[]
        {
            new HorarioDisponivel { ProfessorId = 1, LocalAulaId = 10, DiaSemana = (int)Amanha.DayOfWeek,
                HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(10, 0, 0), DuracaoMinutos = 60, Ativo = true },
            new HorarioDisponivel { ProfessorId = 2, LocalAulaId = 20, DiaSemana = (int)Amanha.DayOfWeek,
                HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(10, 0, 0), DuracaoMinutos = 60, Ativo = true },
        };

        var ocupadas = new[] { (ProfessorId: 1, DataHora: Amanha.AddHours(9), DuracaoMinutos: 60) };

        var ofertas = OfertasDeAula.Gerar(regras, ocupadas, DateTime.Now, 14);

        Assert.Equal(2, Assert.Single(ofertas).ProfessorId);
    }

    [Fact]
    public void Horario_que_ja_passou_hoje_nao_e_oferecido()
    {
        var agora = DateTime.Today.AddHours(14);
        var regras = new[]
        {
            new HorarioDisponivel { ProfessorId = 1, LocalAulaId = 10, DiaSemana = (int)DateTime.Today.DayOfWeek,
                HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(18, 0, 0), DuracaoMinutos = 60, Ativo = true },
        };

        var ofertas = OfertasDeAula.Gerar(regras, Array.Empty<(int, DateTime, int)>(), agora, 1);

        Assert.All(ofertas, o => Assert.True(o.Quando > agora));
        Assert.Equal(DateTime.Today.AddHours(15), ofertas.First().Quando);
    }
}
