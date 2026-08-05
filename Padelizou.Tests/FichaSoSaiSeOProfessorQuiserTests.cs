using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// As travas da avaliação, testadas contra o banco e não contra a tela:
//
//  1. A ficha é do professor até ele liberar.
//  2. A anotação privada não chega no aluno NEM com a ficha liberada.
//  3. Tirar um fundamento da régua não pode apagar nota que o aluno já levou.
//
// Esconder qualquer uma dessas coisas só na view seria publicá-las com um passo a mais: basta
// ver o código-fonte da página.
public class FichaSoSaiSeOProfessorQuiserTests
{
    private const string Segredo = "tem medo de bola alta, ir devagar";

    private static (DbPadelContext ctx, Jogador professor, Jogador aluno) Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        var professor = new Jogador { Nome = "Professor", Cpf = "1", IsProfessor = true };
        var aluno = new Jogador { Nome = "Aluno", Cpf = "2" };
        ctx.Jogadores.AddRange(professor, aluno);
        ctx.SaveChanges();
        return (ctx, professor, aluno);
    }

    private static AvaliacaoDeAluno Ficha(DbPadelContext ctx, int professorId, int alunoId, bool liberada)
    {
        var ficha = new AvaliacaoDeAluno
        {
            ProfessorId = professorId,
            AlunoId = alunoId,
            Modulo = CatalogoDeFundamentos.Iniciante,
            RecadoAoAluno = "Evoluiu bem no voleio",
            AnotacaoPrivada = Segredo,
            VisivelParaAluno = liberada,
            LiberadaEm = liberada ? DateTime.Now : null,
        };
        ctx.AvaliacoesDeAluno.Add(ficha);
        ctx.SaveChanges();
        return ficha;
    }

    // A consulta que o lado do aluno faz (AulasController.MinhaEvolucao): projeção explícita,
    // sem a anotação privada. Se alguém trocar isso por um Include, este teste cai.
    private static List<(string? Recado, string Modulo)> OQueOAlunoVe(DbPadelContext ctx, int alunoId) =>
        ctx.AvaliacoesDeAluno
            .Where(a => a.AlunoId == alunoId && a.VisivelParaAluno)
            .Select(a => new ValueTuple<string?, string>(a.RecadoAoAluno, a.Modulo))
            .ToList();

    [Fact]
    public void Ficha_nova_nasce_INVISIVEL_pro_aluno()
    {
        // O padrão seguro: o professor trabalha em rascunho e o aluno não vê nada até ele
        // decidir. Nascer visível publicaria uma ficha pela metade.
        var (ctx, professor, aluno) = Cenario();

        var ficha = new AvaliacaoDeAluno { ProfessorId = professor.Id, AlunoId = aluno.Id, Modulo = "Iniciante" };

        Assert.False(ficha.VisivelParaAluno);
        Assert.Null(ficha.LiberadaEm);
    }

    [Fact]
    public void Enquanto_nao_libera_o_aluno_nao_ve_nada()
    {
        var (ctx, professor, aluno) = Cenario();
        Ficha(ctx, professor.Id, aluno.Id, liberada: false);

        Assert.Empty(OQueOAlunoVe(ctx, aluno.Id));
    }

    [Fact]
    public void Depois_de_liberar_o_aluno_ve_o_recado_mas_NUNCA_a_anotacao_privada()
    {
        var (ctx, professor, aluno) = Cenario();
        Ficha(ctx, professor.Id, aluno.Id, liberada: true);

        var visto = Assert.Single(OQueOAlunoVe(ctx, aluno.Id));

        Assert.Equal("Evoluiu bem no voleio", visto.Recado);
        // O segredo não pode estar em NENHUM campo que chegou ao aluno.
        Assert.DoesNotContain(Segredo, visto.Recado ?? "");
        Assert.DoesNotContain(Segredo, visto.Modulo);
    }

    [Fact]
    public void A_ficha_de_outro_aluno_nao_aparece_pra_mim()
    {
        var (ctx, professor, aluno) = Cenario();
        var outro = new Jogador { Nome = "Outro", Cpf = "3" };
        ctx.Jogadores.Add(outro);
        ctx.SaveChanges();

        Ficha(ctx, professor.Id, outro.Id, liberada: true);

        Assert.Empty(OQueOAlunoVe(ctx, aluno.Id));
    }

    [Fact]
    public async Task Tirar_da_regua_DESATIVA_e_a_nota_ja_dada_continua_existindo()
    {
        // É o motivo de "remover" não ser DELETE: a ficha antiga do aluno não pode ganhar
        // buraco porque o professor mudou o método dele meses depois.
        var (ctx, professor, aluno) = Cenario();
        var regua = new ReguaDoProfessor(ctx);
        var fundamentos = await regua.GarantirAsync(professor.Id);
        var alvo = fundamentos.First();

        var ficha = Ficha(ctx, professor.Id, aluno.Id, liberada: true);
        ctx.NotasDeFundamento.Add(new NotaDeFundamento
        {
            AvaliacaoDeAlunoId = ficha.Id,
            FundamentoDoProfessorId = alvo.Id,
            Execucao = "B",
        });
        ctx.SaveChanges();

        Assert.True(await regua.DesativarAsync(professor.Id, alvo.Id));

        Assert.False((await ctx.FundamentosDoProfessor.FindAsync(alvo.Id))!.Ativo);
        Assert.Single(ctx.NotasDeFundamento.Where(n => n.FundamentoDoProfessorId == alvo.Id));
        // E some das fichas NOVAS:
        var ativos = await regua.AtivosDoModuloAsync(professor.Id, alvo.Modulo);
        Assert.DoesNotContain(ativos, f => f.Id == alvo.Id);
    }

    [Fact]
    public async Task A_regua_e_semeada_UMA_vez_e_nao_ressuscita_o_que_o_professor_tirou()
    {
        // O pior tipo de bug: o sistema desfazendo o trabalho da pessoa a cada visita.
        var (ctx, professor, _) = Cenario();
        var regua = new ReguaDoProfessor(ctx);

        var primeira = await regua.GarantirAsync(professor.Id);
        await regua.DesativarAsync(professor.Id, primeira.First().Id);

        var segunda = await regua.GarantirAsync(professor.Id);

        Assert.Equal(primeira.Count, segunda.Count);
        Assert.False(segunda.First(f => f.Id == primeira.First().Id).Ativo);
    }

    [Fact]
    public async Task Cada_professor_tem_a_propria_regua()
    {
        var (ctx, professor, _) = Cenario();
        var outro = new Jogador { Nome = "Outro professor", Cpf = "9", IsProfessor = true };
        ctx.Jogadores.Add(outro);
        ctx.SaveChanges();

        var regua = new ReguaDoProfessor(ctx);
        var minha = await regua.GarantirAsync(professor.Id);
        var dele = await regua.GarantirAsync(outro.Id);

        // Mesmo tamanho, linhas diferentes: mexer na dele não pode mexer na minha.
        Assert.Equal(minha.Count, dele.Count);
        Assert.Empty(minha.Select(f => f.Id).Intersect(dele.Select(f => f.Id)));
    }

    [Fact]
    public async Task Fundamento_acrescentado_entra_no_fim_do_modulo()
    {
        var (ctx, professor, _) = Cenario();
        var regua = new ReguaDoProfessor(ctx);

        var novo = await regua.AcrescentarAsync(professor.Id, CatalogoDeFundamentos.Iniciante, "", "Meu golpe secreto");

        Assert.NotNull(novo);
        var doModulo = await regua.AtivosDoModuloAsync(professor.Id, CatalogoDeFundamentos.Iniciante);
        Assert.Equal(novo!.Id, doModulo.Last().Id);
    }

    [Fact]
    public async Task Nome_vazio_ou_gigante_nao_vira_fundamento()
    {
        var (ctx, professor, _) = Cenario();
        var regua = new ReguaDoProfessor(ctx);

        Assert.Null(await regua.AcrescentarAsync(professor.Id, "Iniciante", "", "   "));
        Assert.Null(await regua.AcrescentarAsync(professor.Id, "Iniciante", "", new string('x', 200)));
    }

    [Fact]
    public async Task Professor_nao_mexe_na_regua_de_outro()
    {
        // O id vem do formulário: sem esta checagem, um professor renomearia ou apagaria os
        // fundamentos de outro só chutando números.
        var (ctx, professor, _) = Cenario();
        var outro = new Jogador { Nome = "Outro professor", Cpf = "9", IsProfessor = true };
        ctx.Jogadores.Add(outro);
        ctx.SaveChanges();

        var regua = new ReguaDoProfessor(ctx);
        var dele = await regua.GarantirAsync(outro.Id);
        var alvo = dele.First();

        Assert.False(await regua.DesativarAsync(professor.Id, alvo.Id));
        Assert.False(await regua.RenomearAsync(professor.Id, alvo.Id, "invadido"));
        Assert.True((await ctx.FundamentosDoProfessor.FindAsync(alvo.Id))!.Ativo);
        Assert.NotEqual("invadido", (await ctx.FundamentosDoProfessor.FindAsync(alvo.Id))!.Nome);
    }

    [Fact]
    public async Task Os_modulos_saem_na_ordem_pedagogica_e_nao_alfabetica()
    {
        // Alfabética jogaria "Avançado" na frente de "Iniciante" — o contrário de como se
        // ensina, e a primeira coisa que o professor notaria de errado.
        var (ctx, professor, _) = Cenario();
        var regua = new ReguaDoProfessor(ctx);

        var modulos = await regua.ModulosAsync(professor.Id);

        Assert.Equal(new[] { "Iniciante", "Intermediário", "Avançado" }, modulos);
    }

    [Fact]
    public async Task Modulo_inventado_pelo_professor_entra_depois_dos_nossos()
    {
        var (ctx, professor, _) = Cenario();
        var regua = new ReguaDoProfessor(ctx);
        await regua.AcrescentarAsync(professor.Id, "Competição", "", "Ritmo de jogo");

        var modulos = await regua.ModulosAsync(professor.Id);

        Assert.Equal("Competição", modulos.Last());
    }
}
