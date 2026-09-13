using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// 🗣️ Felipe, 13/09/2026, com o print do perfil e catorze pílulas empilhadas:
// *"tem que fazer uma recurso 'Todos os dias, exceto...' e melhor isso"*.
//
// São dois lados do mesmo fato: o conjunto de dias/períodos tem TAMANHO FIXO (7 × 3 = 21), e
// por isso o complemento dele é tão exato quanto ele. Quem aceita vinte das vinte e uma
// combinações está dizendo uma frase curta — "todos, exceto domingo de manhã" — e a tela
// mostrava vinte pílulas.
//
// ⚠️ A régua de quem recebe aviso (`GruposController`, `AvisosController`,
// `RaqueteLivreController`) trata NENHUMA LINHA como "aceita qualquer dia". Logo as 21
// marcadas e as 0 marcadas já significavam o mesmo, e gravar as 21 era gravar lixo.
public class PreferenciaDeDiaEHorarioTests
{
    private const string Manha = "Manhã";
    private const string Tarde = "Tarde";
    private const string Noite = "Noite";

    private static (int, string)[] Todas() =>
        Enumerable.Range(0, 7)
            .SelectMany(dia => new[] { Manha, Tarde, Noite }.Select(p => (dia, p)))
            .ToArray();

    // ── O resumo que o perfil mostra ──────────────────────────────────────────────────────

    [Fact]
    public void O_quadro_do_print_vira_duas_pilulas_em_vez_de_quatorze()
    {
        // Exatamente as catorze do print: fim de semana à tarde e à noite, semana de manhã e
        // à noite. Agrupar por DIA daria sete pílulas; o que encurta de verdade é agrupar os
        // dias que têm o MESMO conjunto de períodos.
        var escolhas = new[]
        {
            (0, Tarde), (0, Noite),
            (1, Manha), (1, Noite),
            (2, Manha), (2, Noite),
            (3, Manha), (3, Noite),
            (4, Manha), (4, Noite),
            (5, Manha), (5, Noite),
            (6, Tarde), (6, Noite),
        };

        var resumo = ResumoDeDiasEHorarios.Resumir(escolhas);

        Assert.False(resumo.Exceto);
        Assert.Equal(
            new[] { "Fim de semana · Tarde e Noite", "Segunda a Sexta · Manhã e Noite" },
            resumo.Pilulas);
    }

    [Fact]
    public void Faltando_um_horario_so_a_lista_vira_todos_os_dias_exceto()
    {
        var escolhas = Todas().Where(e => e != (0, Manha)).ToArray();

        var resumo = ResumoDeDiasEHorarios.Resumir(escolhas);

        Assert.True(resumo.Exceto);
        Assert.Equal(new[] { "Domingo · Manhã" }, resumo.Pilulas);
    }

    [Fact]
    public void As_vinte_e_uma_combinacoes_dizem_todos_os_dias_e_horarios()
    {
        var resumo = ResumoDeDiasEHorarios.Resumir(Todas());

        Assert.False(resumo.Exceto);
        Assert.Equal(new[] { "Todos os dias e horários" }, resumo.Pilulas);
    }

    [Fact]
    public void Sem_nenhuma_escolha_nao_sobra_nada_pra_mostrar()
    {
        // Nenhuma linha é "aceita qualquer horário", e o perfil já não mostra a seção. O
        // resumo não pode inventar um "todos" aqui: quem não respondeu não escolheu.
        var resumo = ResumoDeDiasEHorarios.Resumir(Array.Empty<(int, string)>());

        Assert.False(resumo.Exceto);
        Assert.Empty(resumo.Pilulas);
    }

    [Fact]
    public void So_as_noites_ficam_na_forma_direta_e_nao_viram_exceto()
    {
        // O complemento ("todos, exceto as manhãs e as tardes") tem o MESMO tamanho da forma
        // direta. Empate vai pra direta — é a que diz o que a pessoa marcou, sem inverter a
        // cabeça de quem lê.
        var escolhas = Enumerable.Range(0, 7).Select(d => (d, Noite)).ToArray();

        var resumo = ResumoDeDiasEHorarios.Resumir(escolhas);

        Assert.False(resumo.Exceto);
        Assert.Equal(new[] { "Todos os dias · Noite" }, resumo.Pilulas);
    }

    [Fact]
    public void Dias_soltos_saem_listados_com_e_antes_do_ultimo()
    {
        var escolhas = new[] { (1, Manha), (3, Manha), (6, Manha) };

        var resumo = ResumoDeDiasEHorarios.Resumir(escolhas);

        Assert.False(resumo.Exceto);
        Assert.Equal(new[] { "Segunda, Quarta e Sábado · Manhã" }, resumo.Pilulas);
    }

    [Fact]
    public void Tres_dias_em_sequencia_viram_de_um_ao_outro()
    {
        var escolhas = new[] { (3, Noite), (4, Noite), (5, Noite) };

        var resumo = ResumoDeDiasEHorarios.Resumir(escolhas);

        Assert.Equal(new[] { "Quarta a Sexta · Noite" }, resumo.Pilulas);
    }

    // ── O que o servidor grava ────────────────────────────────────────────────────────────

    private static async Task<(DbPadelContext ctx, int jogadorId)> ComJogadorAsync()
    {
        var ctx = TestInfra.NovoContexto();
        var jogador = new Jogador { Nome = "Fulano", Cpf = "99900000077", Email = "f@x.com" };
        ctx.Jogadores.Add(jogador);
        await ctx.SaveChangesAsync();
        return (ctx, jogador.Id);
    }

    private static Task SalvarHorariosAsync(DbPadelContext ctx, int jogadorId, params string[] horarios)
    {
        var c = TestInfra.NovoAuthController(ctx, jogadorId);
        return c.Preferencias(
            ladoQuadra: null, lateralidade: null, instagram: null, perfilPrivado: false,
            notificarEmail: false, notificarWhatsApp: false, aceitaConvitesJogo: false,
            notificarTorneiosAbertos: false, notificarSeguidosTorneio: false, notificarAvisoJogo: false,
            notificarJogoAula: false, notificarRaqueteLivre: false, notificarHorarioVagoRegiao: false,
            categoriasSelecionadas: null, clubesSelecionados: null,
            diasHorariosSelecionados: horarios, cidadesSelecionadas: null);
    }

    [Fact]
    public async Task Marcar_as_vinte_e_uma_grava_zero_linhas_porque_e_qualquer_horario()
    {
        // O botão "Todos os dias, exceto…" marca a grade inteira pra pessoa desmarcar o que
        // não serve. Quem não desmarcar nada está dizendo "qualquer horário" — e isso já tem
        // uma representação no banco: linha nenhuma. Gravar 21 linhas inúteis por jogador só
        // aumentaria a tabela e faria o perfil anunciar uma restrição que não existe.
        var (ctx, jogadorId) = await ComJogadorAsync();
        using var _ = ctx;

        await SalvarHorariosAsync(ctx, jogadorId, Todas().Select(e => $"{e.Item1}|{e.Item2}").ToArray());

        Assert.Empty(await ctx.JogadorDiasHorarios.Where(d => d.JogadorId == jogadorId).ToListAsync());
    }

    [Fact]
    public async Task Todos_menos_um_grava_as_vinte_linhas()
    {
        var (ctx, jogadorId) = await ComJogadorAsync();
        using var _ = ctx;

        await SalvarHorariosAsync(ctx, jogadorId,
            Todas().Where(e => e != (0, Manha)).Select(e => $"{e.Item1}|{e.Item2}").ToArray());

        var salvas = await ctx.JogadorDiasHorarios.Where(d => d.JogadorId == jogadorId).ToListAsync();
        Assert.Equal(20, salvas.Count);
        Assert.DoesNotContain(salvas, d => d.DiaSemana == 0 && d.Periodo == Manha);
    }

    [Fact]
    public async Task Dia_ou_periodo_que_nao_existe_nao_vira_linha_no_banco()
    {
        // O POST vem de fora. "9|Manhã" e "1|Madrugada" nunca casariam com a consulta do
        // aviso (`d.Periodo == periodo`), então seriam linha morta no banco — e, pior, linha
        // morta CONTA: bastava uma pra o jogador deixar de ser "sem restrição".
        var (ctx, jogadorId) = await ComJogadorAsync();
        using var _ = ctx;

        await SalvarHorariosAsync(ctx, jogadorId, "9|Manhã", "1|Madrugada", "-1|Noite", "lixo", "2|Tarde");

        var salvas = await ctx.JogadorDiasHorarios.Where(d => d.JogadorId == jogadorId).ToListAsync();
        Assert.Single(salvas);
        Assert.Equal(2, salvas[0].DiaSemana);
        Assert.Equal(Tarde, salvas[0].Periodo);
    }
}
