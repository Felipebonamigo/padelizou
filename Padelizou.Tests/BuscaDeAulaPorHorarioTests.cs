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
    // ⚠️ A janela do servidor é de 60 dias, então TODA regra de grade rende o mesmo dia da
    // semana OITO ou NOVE vezes (amanhã, amanhã + 7, amanhã + 14...). Os testes olham só a
    // primeira ocorrência — é a que os cenários montam — mas quem escrever teste novo aqui
    // precisa saber disso: contar "os horários do professor" sem escolher o dia devolve quase
    // uma ordem de grandeza a mais do que parece.
    private static DateTime Amanha => DateTime.Today.AddDays(1);

    // O mesmo apelido que o MVC usa ao responder um `Json(...)`: camelCase. Serializar com o
    // padrão do System.Text.Json devolveria "Nome" e o teste passaria a checar um JSON que o
    // navegador nunca vê.
    private static readonly JsonSerializerOptions ComoNaWeb = new(JsonSerializerDefaults.Web);

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

    // Um professor completo: cidade, local ativo e uma janela na grade.
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
        return JsonDocument.Parse(JsonSerializer.Serialize(corpo, ComoNaWeb)).RootElement;
    }

    private static List<JsonElement> Lista(JsonElement raiz, string campo) =>
        raiz.GetProperty(campo).EnumerateArray().ToList();

    // As ofertas de UM dia. Sem recortar o dia, a janela de 14 dias devolve cada horário duas
    // vezes e o teste vira aritmética de calendário em vez de regra de negócio.
    private static List<JsonElement> OfertasDe(JsonElement raiz, DateTime dia) =>
        Lista(raiz, "ofertas")
            .Where(o => o.GetProperty("valor").GetString()!.StartsWith(dia.ToString("yyyy-MM-dd")))
            .ToList();

    private static List<string> HorariosDe(JsonElement raiz, DateTime dia) =>
        OfertasDe(raiz, dia)
            .Select(o => o.GetProperty("valor").GetString()!.Substring(11, 5))
            .ToList();

    private static List<string?> Nomes(JsonElement raiz, string campo) =>
        Lista(raiz, campo).Select(p => p.GetProperty("nome").GetString()).OrderBy(n => n).ToList();

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

        Assert.Equal(new[] { "Bruna", "Jonatas" }, Nomes(corpo, "professores"));
        Assert.Equal(2, Lista(corpo, "locais").Count);
        // Jonatas rende dois slots de uma hora (9h e 10h); Bruna, um.
        Assert.Equal(new[] { "09:00", "09:00", "10:00" }, HorariosDe(corpo, Amanha).OrderBy(h => h));
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

        var oferta = Assert.Single(OfertasDe(await BuscarAsync(ctx, cidade.Id, aluno.Id), Amanha));

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
        local.PrecosDeTurma.Add(new PrecoDeTurma { QuantidadeAlunos = 2, Preco = 150 });
        ctx.PacotesDeAulas.Add(new PacoteDeAulas
        {
            LocalAulaId = local.Id, QuantidadeAulas = 4, Preco = 400, Ativo = true,
        });
        ctx.SaveChanges();

        var noJson = Assert.Single(Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "locais"));

        Assert.Equal(120, noJson.GetProperty("precoPadrao").GetDecimal());

        // Só os tamanhos que o professor anunciou viajam: a tela oferece exatamente estes, e
        // oferecer o trio de quem não faz trio é prometer um preço que não existe.
        var turma = Assert.Single(noJson.GetProperty("precosDeTurma").EnumerateArray());
        Assert.Equal(2, turma.GetProperty("quantidadeAlunos").GetInt32());
        Assert.Equal(150, turma.GetProperty("preco").GetDecimal());
        Assert.Equal(4, Assert.Single(noJson.GetProperty("pacotes").EnumerateArray())
            .GetProperty("quantidadeAulas").GetInt32());
    }

    // Marcar pro mês que vem é pedido normal — e antes NÃO era limite de tela, era de dados:
    // com a janela de 14 dias, a data do mês seguinte simplesmente não vinha na resposta, e o
    // calendário não tinha o que desenhar. Este teste é o que segura a janela larga: encurtá-la
    // de volta apaga o mês que vem da tela sem quebrar mais nada.
    [Fact]
    public async Task A_busca_alcanca_o_mes_que_vem()
    {
        var (ctx, cidade, aluno) = Cenario();
        using var _ = ctx;

        Professor(ctx, cidade, "Jonatas", Amanha, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));

        var dias = Lista(await BuscarAsync(ctx, cidade.Id, aluno.Id), "ofertas")
            .Select(o => DateTime.Parse(o.GetProperty("valor").GetString()!).Date)
            .ToList();

        // O mês seguinte ao de hoje tem que aparecer, venha o dia 1 ou o dia 30 — é isso que
        // "o aluno consegue marcar pro mês que vem" significa.
        var mesQueVem = DateTime.Today.AddMonths(1);
        Assert.Contains(dias, d => d.Year == mesQueVem.Year && d.Month == mesQueVem.Month);
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

        Assert.Equal(new[] { "10:00" }, HorariosDe(await BuscarAsync(ctx, cidade.Id, aluno.Id), Amanha));
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

        Assert.Equal(new[] { "11:00" }, HorariosDe(await BuscarAsync(ctx, cidade.Id, aluno.Id), Amanha));
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

        Assert.Equal(new[] { "Jonatas" }, Nomes(await BuscarAsync(ctx, cidade.Id, aluno.Id), "professores"));
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

        Assert.Equal(new[] { "Jonatas" }, Nomes(await BuscarAsync(ctx, cidade.Id, aluno.Id), "professores"));
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

        Assert.Equal(new[] { "Bruna", "Jonatas" }, Nomes(await BuscarAsync(ctx, cidade.Id, aluno.Id), "professores"));
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
        Assert.Empty(Lista(corpo, "professores"));
    }

    // ===================== A GRADE, SEM BANCO NO MEIO =====================

    // O gerador é a única cópia da regra "quais horários existem" — antes ela morava dentro do
    // controller, escopada a um professor só. Aqui ele é testado direto, com janela de 7 dias
    // pra cada dia da semana aparecer UMA vez.

    private static HorarioDisponivel Regra(int professorId, int localId, TimeSpan inicio, TimeSpan fim) =>
        new()
        {
            ProfessorId = professorId, LocalAulaId = localId, DiaSemana = (int)Amanha.DayOfWeek,
            HoraInicio = inicio, HoraFim = fim, DuracaoMinutos = 60, Ativo = true,
        };

    [Fact]
    public void A_agenda_que_trava_o_horario_e_a_do_PROFESSOR_nao_a_do_local()
    {
        // O mesmo professor não pode estar em duas quadras às 9h. Filtrar o ocupado por local
        // deixaria a segunda quadra dele parecendo livre.
        var regras = new[]
        {
            Regra(professorId: 1, localId: 10, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0)),
            Regra(professorId: 1, localId: 20, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0)),
        };

        // Aula no local 10; o local 20 não pode sobrar livre no mesmo horário.
        var ocupadas = new[] { (ProfessorId: 1, DataHora: Amanha.AddHours(9), DuracaoMinutos: 60) };

        Assert.Empty(OfertasDeAula.Gerar(regras, ocupadas, DateTime.Now, dias: 7));
    }

    [Fact]
    public void A_grade_de_outro_professor_nao_e_travada_pela_aula_alheia()
    {
        var regras = new[]
        {
            Regra(professorId: 1, localId: 10, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0)),
            Regra(professorId: 2, localId: 20, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0)),
        };

        var ocupadas = new[] { (ProfessorId: 1, DataHora: Amanha.AddHours(9), DuracaoMinutos: 60) };

        var oferta = Assert.Single(OfertasDeAula.Gerar(regras, ocupadas, DateTime.Now, dias: 7));

        Assert.Equal(2, oferta.ProfessorId);
        Assert.Equal(20, oferta.LocalId);
    }

    [Fact]
    public void Horario_que_ja_passou_hoje_nao_e_oferecido()
    {
        var agora = DateTime.Today.AddHours(14);
        var regras = new[]
        {
            new HorarioDisponivel
            {
                ProfessorId = 1, LocalAulaId = 10, DiaSemana = (int)DateTime.Today.DayOfWeek,
                HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(18, 0, 0),
                DuracaoMinutos = 60, Ativo = true,
            },
        };

        var ofertas = OfertasDeAula.Gerar(regras, Array.Empty<(int, DateTime, int)>(), agora, dias: 1);

        Assert.All(ofertas, o => Assert.True(o.Quando > agora));
        Assert.Equal(DateTime.Today.AddHours(15), ofertas.First().Quando);
    }

    [Fact]
    public void Regra_desligada_nao_gera_horario()
    {
        var regra = Regra(professorId: 1, localId: 10, new TimeSpan(9, 0, 0), new TimeSpan(10, 0, 0));
        regra.Ativo = false;

        Assert.Empty(OfertasDeAula.Gerar(new[] { regra }, Array.Empty<(int, DateTime, int)>(), DateTime.Now, dias: 7));
    }
}
