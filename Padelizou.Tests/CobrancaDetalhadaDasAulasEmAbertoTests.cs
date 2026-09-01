using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// 🗣️ Pedido do Felipe, 01/09/2026, num print da lista "Quem está devendo": *"caso o professor
// tenha um aluno com varias aulas em atraso, quando ele clicar para cobrar, caso o aluno tenha
// mais de uma aula devendo, perguntar se ele quer que envie todas as cobranças desse aluno para
// o whats detalhadamente, ai crie essa mensagem detalhada para encaminhar"*.
//
// Até aqui o botão do WhatsApp mandava UMA frase pra qualquer devedor: "das 7 aula(s) em aberto,
// total de R$ 700,00". Quem deve sete aulas de três meses diferentes não tem como conferir isso
// — e a resposta padrão do aluno é "que aulas?", que devolve o professor pra agenda dia a dia.
//
// ⚠️ Quem manda continua sendo o PROFESSOR, num toque, pelo WhatsApp DELE (link wa.me). Nada
// aqui passa pelo chip do Padelizou — é a mesma decisão escrita em Services/ConviteDaAulaMarcada.
public class CobrancaDetalhadaDasAulasEmAbertoTests
{
    // A suíte não passa pelo Program.cs, então a cultura da thread aqui é a da máquina. O
    // dinheiro da mensagem é escrito em pt-BR pelo serviço: comparar com o mesmo pt-BR é o que
    // impede o teste de quebrar por causa do separador da máquina do CI, sem afrouxar nada do
    // que ele checa (posição, marcador e total).
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static string Reais(decimal v) => v.ToString("C", PtBr);

    private static DevedorVM Devedor(params AulaEmAbertoVM[] aulas) => new()
    {
        Nome = "Cleiton Alves Branco",
        Celular = "51999990000",
        AulasEmAberto = aulas.Length,
        Valor = aulas.Sum(a => a.Preco),
        AulaMaisAntiga = aulas.Min(a => a.DataHora),
        AulaIds = aulas.Select((_, i) => i + 1).ToList(),
        Aulas = aulas.ToList(),
    };

    private static AulaEmAbertoVM Aula(DateTime quando, decimal preco = 100m, string status = PoliticaAula.Realizada) =>
        new() { DataHora = quando, Preco = preco, Status = status };

    // ─── A mensagem detalhada ─────────────────────────────────────────────────────────

    [Fact]
    public void Detalhada_escreve_uma_linha_por_aula_com_data_dia_hora_e_valor()
    {
        var texto = CobrancaDasAulasEmAberto.Detalhada(Devedor(
            Aula(new DateTime(2026, 8, 4, 19, 0, 0)),
            Aula(new DateTime(2026, 8, 6, 20, 30, 0))));

        // 04/08/2026 é uma terça; 06/08, uma quinta.
        Assert.Contains($"• 04/08 (ter) 19:00 · {Reais(100m)}", texto);
        Assert.Contains($"• 06/08 (qui) 20:30 · {Reais(100m)}", texto);
    }

    // O nome inteiro numa saudação de WhatsApp soa cobrança de banco. O primeiro nome é o que
    // o professor escreveria, e é o que a mensagem curta já fazia.
    [Fact]
    public void Detalhada_chama_o_aluno_pelo_primeiro_nome()
    {
        var texto = CobrancaDasAulasEmAberto.Detalhada(Devedor(
            Aula(new DateTime(2026, 8, 4, 19, 0, 0)),
            Aula(new DateTime(2026, 8, 6, 19, 0, 0))));

        Assert.StartsWith("Oi Cleiton!", texto);
        Assert.DoesNotContain("Alves Branco", texto);
    }

    // O total é o número que o aluno vai pagar: ele tem que ser a soma EXATA das linhas que
    // estão ali em cima, senão a mensagem se contradiz sozinha.
    [Fact]
    public void Detalhada_fecha_com_o_total_que_e_a_soma_das_linhas()
    {
        var texto = CobrancaDasAulasEmAberto.Detalhada(Devedor(
            Aula(new DateTime(2026, 8, 4, 19, 0, 0), 100m),
            Aula(new DateTime(2026, 8, 6, 19, 0, 0), 120m),
            Aula(new DateTime(2026, 8, 11, 19, 0, 0), 105m)));

        Assert.Contains($"Total: {Reais(325m)}", texto);
    }

    // ⚠️ A falta cobrada é a linha que mais gera resposta: o aluno olha a data, lembra que não
    // teve aula naquele dia e a cobrança inteira perde a credibilidade. Dizer "(falta)" é o que
    // faz a linha se explicar sozinha.
    [Fact]
    public void Detalhada_marca_a_falta_cobrada()
    {
        var texto = CobrancaDasAulasEmAberto.Detalhada(Devedor(
            Aula(new DateTime(2026, 8, 4, 19, 0, 0)),
            Aula(new DateTime(2026, 8, 6, 19, 0, 0), status: PoliticaAula.Faltou)));

        Assert.Contains($"• 06/08 (qui) 19:00 · {Reais(100m)} (falta)", texto);
        // Contraprova: a aula DADA não ganha marcador nenhum.
        Assert.Contains($"• 04/08 (ter) 19:00 · {Reais(100m)}\n", texto);
    }

    // A da fila de reposição também é cobrada (CobrarMesmoFaltando fica ligado), mas ela NÃO é
    // uma falta perdida: o aluno tem a aula pra repor, e é isso que ele precisa ler.
    [Fact]
    public void Detalhada_marca_a_aula_que_ainda_vai_ser_reposta()
    {
        var texto = CobrancaDasAulasEmAberto.Detalhada(Devedor(
            Aula(new DateTime(2026, 8, 4, 19, 0, 0)),
            Aula(new DateTime(2026, 8, 6, 19, 0, 0), status: PoliticaAula.ARecuperar)));

        Assert.Contains($"• 06/08 (qui) 19:00 · {Reais(100m)} (a repor)", texto);
    }

    // A lista chega da tela ordenada, mas quem escreve o texto não pode depender disso: uma
    // cobrança com as datas fora de ordem é lida como erro de conta.
    [Fact]
    public void Detalhada_ordena_da_aula_mais_antiga_pra_mais_nova()
    {
        var texto = CobrancaDasAulasEmAberto.Detalhada(Devedor(
            Aula(new DateTime(2026, 8, 20, 19, 0, 0)),
            Aula(new DateTime(2026, 8, 4, 19, 0, 0)),
            Aula(new DateTime(2026, 8, 11, 19, 0, 0))));

        Assert.True(texto.IndexOf("04/08") < texto.IndexOf("11/08"));
        Assert.True(texto.IndexOf("11/08") < texto.IndexOf("20/08"));
    }

    // ⚠️ O texto viaja DENTRO da URL do wa.me. Cinquenta linhas viram uma URL de vários KB, que
    // é onde navegador e app começam a truncar — e mensagem truncada mente sem avisar.
    [Fact]
    public void Detalhada_corta_no_teto_de_linhas_e_diz_quantas_ficaram_de_fora()
    {
        var aulas = Enumerable.Range(0, CobrancaDasAulasEmAberto.MaximoDeLinhas + 3)
            .Select(i => Aula(new DateTime(2026, 3, 2, 19, 0, 0).AddDays(i), 50m))
            .ToArray();

        var texto = CobrancaDasAulasEmAberto.Detalhada(Devedor(aulas));

        Assert.Equal(CobrancaDasAulasEmAberto.MaximoDeLinhas, texto.Split("· R$").Length - 1);
        Assert.Contains("e mais 3 aula(s)", texto);
        // O TOTAL continua sendo o da dívida inteira: cortar a lista não pode encolher a conta.
        Assert.Contains($"Total: {Reais(50m * (CobrancaDasAulasEmAberto.MaximoDeLinhas + 3))}", texto);
    }

    // ─── A pergunta, e a mensagem curta que já existia ────────────────────────────────

    // A pergunta só faz sentido com mais de uma aula: com uma só, "detalhar" é repetir a mesma
    // linha que o resumo já tem, e um clique a mais por nada é como se ensina a ignorar avisos.
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void A_pergunta_so_aparece_pra_quem_deve_mais_de_uma_aula(int aulas, bool esperado)
    {
        var devedor = Devedor(Enumerable.Range(0, aulas)
            .Select(i => Aula(new DateTime(2026, 8, 4, 19, 0, 0).AddDays(i)))
            .ToArray());

        Assert.Equal(esperado, CobrancaDasAulasEmAberto.CabeDetalhe(devedor));
    }

    // O resumo é o texto que o botão manda desde sempre, e ele continua sendo a resposta
    // "Cancelar" da pergunta. Fixado aqui pra não mudar sem querer ao mexer no detalhado.
    [Fact]
    public void Resumo_continua_sendo_a_frase_curta_de_antes()
    {
        var texto = CobrancaDasAulasEmAberto.Resumo(Devedor(
            Aula(new DateTime(2026, 8, 4, 19, 0, 0)),
            Aula(new DateTime(2026, 8, 6, 19, 0, 0))));

        Assert.Equal($"Oi Cleiton! Passando pra lembrar das 2 aula(s) em aberto, "
                   + $"total de {Reais(200m)}. Abraço!", texto);
    }

    // Aluno avulso sem nome anotado cai no rótulo genérico da lista. "Oi !" é o tipo de coisa
    // que o professor manda sem ver e o aluno vê.
    [Fact]
    public void Sem_nome_a_mensagem_nao_abre_com_saudacao_vazia()
    {
        var devedor = Devedor(Aula(new DateTime(2026, 8, 4, 19, 0, 0)), Aula(new DateTime(2026, 8, 6, 19, 0, 0)));
        devedor.Nome = "   ";

        Assert.StartsWith("Oi! Passando", CobrancaDasAulasEmAberto.Detalhada(devedor));
        Assert.StartsWith("Oi! Passando", CobrancaDasAulasEmAberto.Resumo(devedor));
    }

    // ─── A tela: de onde saem as linhas ───────────────────────────────────────────────

    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Jonatas", Login = "jonatas", Cpf = "99900000041", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Batata Padel", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Aula Linha(Jogador professor, LocalAula local, DateTime quando, decimal preco = 110m) => new()
    {
        ProfessorId = professor.Id,
        LocalAulaId = local.Id,
        DataHora = quando,
        DuracaoMinutos = 60,
        Preco = preco,
        Status = PoliticaAula.Realizada,
        QuantidadeAlunos = 1,
        NomeAlunoAvulso = "Cleiton",
    };

    private static async Task<FinanceiroProfessorVM> FinanceiroDoMes(DbPadelContext ctx, int professorId)
    {
        var resultado = await TestInfra.NovoAulasController(ctx, professorId).Financeiro("mes");
        return Assert.IsType<FinanceiroProfessorVM>(Assert.IsType<ViewResult>(resultado).Model);
    }

    [Fact]
    public async Task O_devedor_leva_pra_tela_as_aulas_que_a_mensagem_detalhada_vai_listar()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var hoje = DateTime.Today;
        ctx.Aulas.AddRange(
            Linha(professor, local, hoje.AddHours(9), 110m),
            Linha(professor, local, hoje.AddHours(11), 90m));
        await ctx.SaveChangesAsync();

        var devedor = Assert.Single((await FinanceiroDoMes(ctx, professor.Id)).Devedores);

        // A mesma invariante do botão "Recebi": a lista da mensagem e a lista da baixa são as
        // MESMAS aulas, e a soma delas é o valor que a tela mostra ao lado do nome.
        Assert.Equal(devedor.AulasEmAberto, devedor.Aulas.Count);
        Assert.Equal(devedor.AulaIds.Count, devedor.Aulas.Count);
        Assert.Equal(devedor.Valor, devedor.Aulas.Sum(a => a.Preco));
    }

    // ⚠️ A lista detalhada é a do PERÍODO ESCOLHIDO na tela — a mesma que soma o valor mostrado
    // ao lado do nome. Mandar "todas de sempre" faria a mensagem cobrar um total diferente do
    // que está escrito na linha logo acima do botão, que é o defeito que a tela por local já
    // levou uma correção pra não ter.
    [Fact]
    public async Task A_mensagem_so_lista_as_aulas_do_periodo_que_esta_na_tela()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var hoje = DateTime.Today;
        var mesPassado = new DateTime(hoje.Year, hoje.Month, 1).AddMonths(-1).AddHours(9);
        ctx.Aulas.AddRange(
            Linha(professor, local, hoje.AddHours(9), 110m),
            Linha(professor, local, mesPassado, 110m));
        await ctx.SaveChangesAsync();

        var devedor = Assert.Single((await FinanceiroDoMes(ctx, professor.Id)).Devedores);

        Assert.Single(devedor.Aulas);
        Assert.Equal(devedor.Valor, devedor.Aulas.Sum(a => a.Preco));
        Assert.DoesNotContain(devedor.Aulas, a => a.DataHora.Month == mesPassado.Month);
    }

    // A falta cobrada e a aula da fila de reposição chegam na lista com o status que o texto
    // usa pra marcar a linha — sem ele, as duas sairiam escritas como aula dada.
    [Fact]
    public async Task O_status_de_cada_aula_chega_na_tela_pra_mensagem_marcar_a_linha()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var hoje = DateTime.Today;
        var falta = Linha(professor, local, hoje.AddHours(9));
        falta.Status = PoliticaAula.Faltou;
        falta.CobrarMesmoFaltando = true;

        var reposicao = Linha(professor, local, hoje.AddHours(11));
        reposicao.Status = PoliticaAula.ARecuperar;
        reposicao.CobrarMesmoFaltando = true;

        ctx.Aulas.AddRange(falta, reposicao, Linha(professor, local, hoje.AddHours(15)));
        await ctx.SaveChangesAsync();

        var devedor = Assert.Single((await FinanceiroDoMes(ctx, professor.Id)).Devedores);
        var texto = CobrancaDasAulasEmAberto.Detalhada(devedor);

        Assert.Contains("(falta)", texto);
        Assert.Contains("(a repor)", texto);
        Assert.Contains($"Total: {Reais(devedor.Valor)}", texto);
    }
}
