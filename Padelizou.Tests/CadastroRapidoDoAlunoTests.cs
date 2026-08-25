using Microsoft.EntityFrameworkCore;
using padelizou.Controllers;   // AulasController ficou no namespace legado, em minúsculo
using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;
using Padelizou.ViewModels;
using Xunit;

namespace Padelizou.Tests;

// O cadastro rápido do aluno e o RESPONSÁVEL pela cobrança (ver Models/CadastroDoAluno).
//
// O pedido do Rafael tinha duas metades. "Tem muita gente que não manda os dados e me quebra"
// — o professor precisa cadastrar com o que tem na mão, que é nome e celular, nunca CPF. E
// "nem sempre o aluno é o responsável pelo próprio pagamento (crianças)" — a cobrança do mês
// tem que sair no nome de quem paga, sem o aluno deixar de ser o aluno na agenda.
public class CadastroRapidoDoAlunoTests
{
    private static (DbPadelContext ctx, Jogador professor, LocalAula local) Montar()
    {
        var ctx = TestInfra.NovoContexto();

        var professor = new Jogador { Nome = "Joao", Login = "joao", Cpf = "99900000021", IsProfessor = true };
        ctx.Jogadores.Add(professor);
        ctx.SaveChanges();

        var local = new LocalAula { ProfessorId = professor.Id, Nome = "Arena", PrecoPadrao = 110, Ativo = true };
        ctx.LocaisAula.Add(local);
        ctx.SaveChanges();

        return (ctx, professor, local);
    }

    private static Task<Microsoft.AspNetCore.Mvc.IActionResult> Marcar(
        DbPadelContext ctx, Jogador professor, LocalAula local, string nome, string? telefone,
        int diasAFrente = 2, int? alunoId = null) =>
        TestInfra.NovoAulasController(ctx, professor.Id).AdicionarManual(
            localId: local.Id, nomeAluno: nome, telefoneAluno: telefone,
            data: DataEHoraDoFormulario.ParaCampoDeData(DateTime.Today.AddDays(diasAFrente).AddHours(7)), hora: DataEHoraDoFormulario.ParaCampoDeHora(DateTime.Today.AddDays(diasAFrente).AddHours(7)), preco: null,
            recorrente: false, semanasRecorrencia: 0, alunoId: alunoId);

    // ─── O cadastro nasce ao marcar a aula ────────────────────────────────────────────

    [Fact]
    public async Task Marcar_a_aula_ja_cadastra_o_aluno_com_o_celular()
    {
        // O cadastro rápido acontece no fluxo que o professor já faz. Pedir os mesmos dados
        // numa tela à parte é o atrito que faz ele não cadastrar ninguém.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Medina", "(51) 99999-0000");

        var cadastro = await ctx.CadastrosDeAlunos.SingleAsync();
        Assert.Equal("Medina", cadastro.NomeAvulso);
        // Só dígitos: o professor digita o mesmo número de três jeitos em três dias, e
        // guardar do jeito que veio faria a mesma pessoa virar três.
        Assert.Equal("51999990000", cadastro.Celular);
        Assert.Null(cadastro.AlunoId);
    }

    [Fact]
    public async Task Duas_aulas_do_mesmo_aluno_nao_criam_dois_cadastros()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Medina", "51999990000", diasAFrente: 2);
        await Marcar(ctx, professor, local, "Medina", "51999990000", diasAFrente: 3);

        Assert.Equal(1, await ctx.CadastrosDeAlunos.CountAsync());
    }

    [Fact]
    public async Task Marcar_aula_SEM_telefone_nao_apaga_o_celular_ja_cadastrado()
    {
        // Telefone é opcional na marcação desde 04/08, e o professor marca sem ele o tempo
        // todo. Se o branco apagasse, cada aula nova destruiria o cadastro da anterior.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Medina", "51999990000", diasAFrente: 2);
        await Marcar(ctx, professor, local, "Medina", null, diasAFrente: 3);

        Assert.Equal("51999990000", (await ctx.CadastrosDeAlunos.SingleAsync()).Celular);
    }

    [Fact]
    public async Task Celular_curto_demais_nao_e_guardado()
    {
        // Número com 8 dígitos não disca nem casa com conta nenhuma — guardá-lo daria a
        // impressão de cadastro feito.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Medina", "99990000");

        Assert.Null((await ctx.CadastrosDeAlunos.SingleAsync()).Celular);
    }

    [Fact]
    public async Task Aluno_com_conta_e_cadastrado_pela_CONTA_e_nao_pelo_nome()
    {
        // Mesma divisão que a agenda e o acordo de preço já fazem: quem tem conta entra por
        // ela. Cadastrar pelo nome criaria a segunda ficha da mesma pessoa.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        var aluno = new Jogador { Nome = "Eduarda", Login = "duda", Cpf = "99900000022" };
        ctx.Jogadores.Add(aluno);
        await ctx.SaveChangesAsync();

        await Marcar(ctx, professor, local, "Duda", "51988880000", alunoId: aluno.Id);

        var cadastro = await ctx.CadastrosDeAlunos.SingleAsync();
        Assert.Equal(aluno.Id, cadastro.AlunoId);
        Assert.Null(cadastro.NomeAvulso);
    }

    // ─── O responsável pela cobrança ──────────────────────────────────────────────────

    [Fact]
    public async Task O_responsavel_fica_guardado_e_e_quem_paga()
    {
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Pedrinho", "51999990000");

        await TestInfra.NovoAulasController(ctx, professor.Id).SalvarCadastroDoAluno(
            alunoId: null, nomeAvulso: "Pedrinho", celular: "51999990000",
            responsavelNome: "Marcia (mãe)", responsavelCelular: "(51) 98888-1111",
            responsavelCpf: "529.982.247-25", observacao: null);

        var cadastro = await ctx.CadastrosDeAlunos.SingleAsync();
        Assert.Equal("Marcia (mãe)", cadastro.ResponsavelNome);
        Assert.Equal("51988881111", cadastro.ResponsavelCelular);
        Assert.Equal("52998224725", cadastro.ResponsavelCpf);

        Assert.Equal("Marcia (mãe)", CadastrosDeAlunos.QuemPaga(cadastro, "Pedrinho"));
        // A cobrança vai pro celular de quem paga: mandar pro aluno uma conta que a mãe vai
        // pagar é o WhatsApp do professor virando meio de campo de novo.
        Assert.Equal("51988881111", CadastrosDeAlunos.CelularDeCobranca(cadastro, "51999990000"));
    }

    [Fact]
    public async Task CPF_errado_do_responsavel_e_recusado_na_hora()
    {
        // Sem esta trava, o erro só apareceria no dia da cobrança — com o mês já fechado.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Pedrinho", "51999990000");

        await TestInfra.NovoAulasController(ctx, professor.Id).SalvarCadastroDoAluno(
            alunoId: null, nomeAvulso: "Pedrinho", celular: null,
            responsavelNome: "Marcia", responsavelCelular: null,
            responsavelCpf: "11111111111", observacao: null);

        Assert.Null((await ctx.CadastrosDeAlunos.SingleAsync()).ResponsavelNome);
    }

    [Fact]
    public void CPF_sem_nome_do_responsavel_nao_passa()
    {
        // CPF solto não diz quem paga — e é o nome que vai na cobrança.
        Assert.NotNull(CadastrosDeAlunos.ProblemaComOResponsavel(null, "52998224725"));
        Assert.Null(CadastrosDeAlunos.ProblemaComOResponsavel(null, null));
        Assert.Null(CadastrosDeAlunos.ProblemaComOResponsavel("Marcia", null));
    }

    [Fact]
    public async Task Limpar_o_responsavel_na_tela_APAGA_ele()
    {
        // Ao contrário do cadastro rápido, aqui o branco apaga: é a única forma de dizer
        // "voltou a pagar por conta própria".
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Pedrinho", "51999990000");
        var controller = TestInfra.NovoAulasController(ctx, professor.Id);

        await controller.SalvarCadastroDoAluno(null, "Pedrinho", "51999990000", "Marcia", null, null, null);
        await controller.SalvarCadastroDoAluno(null, "Pedrinho", "51999990000", null, null, null, null);

        var cadastro = await ctx.CadastrosDeAlunos.SingleAsync();
        Assert.Null(cadastro.ResponsavelNome);
        Assert.False(CadastrosDeAlunos.TemResponsavel(cadastro));
    }

    [Fact]
    public async Task Nao_da_pra_cadastrar_quem_nao_e_meu_aluno()
    {
        // Sem esta checagem, um id no formulário criaria cadastro — com CPF de terceiro
        // dentro — de quem nunca pisou na quadra dele.
        var (ctx, professor, _) = Montar();
        using var _d = ctx;

        var estranho = new Jogador { Nome = "Estranho", Login = "estranho", Cpf = "99900000023" };
        ctx.Jogadores.Add(estranho);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).SalvarCadastroDoAluno(
            alunoId: estranho.Id, nomeAvulso: null, celular: "51999990000",
            responsavelNome: null, responsavelCelular: null, responsavelCpf: null, observacao: null);

        Assert.Empty(await ctx.CadastrosDeAlunos.ToListAsync());
    }

    // ─── O encontro com a conta ───────────────────────────────────────────────────────

    [Fact]
    public async Task O_painel_avisa_quando_o_celular_cadastrado_virou_conta()
    {
        // É o encontro entre o cadastro rápido do professor e a campanha de "se cadastrem
        // certinho". Sem isto ele nunca saberia, e a mesma pessoa ficaria em dois lugares.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Medina", "51999990000");

        ctx.Jogadores.Add(new Jogador
        {
            Nome = "Gabriel Medina", Login = "medina", Cpf = "99900000024", Celular = "51999990000",
        });
        await ctx.SaveChangesAsync();

        // O painel só abre com a "escada do professor" completa (cidade, local e horário
        // ativos) — ver Services/CadastroDeProfessor. Sem isso ele redireciona.
        var cidade = new Cidade { Nome = "Porto Alegre" };
        ctx.Cidades.Add(cidade);
        await ctx.SaveChangesAsync();
        ctx.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = professor.Id, CidadeId = cidade.Id });
        ctx.HorariosDisponiveis.Add(new HorarioDisponivel
        {
            ProfessorId = professor.Id, LocalAulaId = local.Id,
            DiaSemana = 1, HoraInicio = new TimeSpan(9, 0, 0), HoraFim = new TimeSpan(10, 0, 0),
            DuracaoMinutos = 60, Ativo = true,
        });
        await ctx.SaveChangesAsync();

        var vista = await TestInfra.NovoAulasController(ctx, professor.Id).Dashboard();
        var vm = Assert.IsType<PainelProfessorViewModel>(Assert.IsType<Microsoft.AspNetCore.Mvc.ViewResult>(vista).Model);

        var aluno = Assert.Single(vm.Alunos);
        Assert.Equal("Gabriel Medina", aluno.ContaSugeridaNome);
    }

    [Fact]
    public async Task Ligar_a_conta_leva_o_responsavel_junto()
    {
        // Dentro do cadastro está QUEM PAGA. Deixá-lo preso ao nome antigo faria a mãe sumir
        // da cobrança no dia em que o filho criasse conta — e ninguém veria até o mês fechar.
        var (ctx, professor, local) = Montar();
        using var _ = ctx;

        await Marcar(ctx, professor, local, "Pedrinho", "51999990000");
        await TestInfra.NovoAulasController(ctx, professor.Id).SalvarCadastroDoAluno(
            null, "Pedrinho", "51999990000", "Marcia", "51988881111", "52998224725", null);

        var conta = new Jogador { Nome = "Pedro Silva", Login = "pedrinho", Cpf = "99900000025" };
        ctx.Jogadores.Add(conta);
        await ctx.SaveChangesAsync();

        await TestInfra.NovoAulasController(ctx, professor.Id).VincularAlunoAConta("Pedrinho", conta.Id);

        var cadastro = await ctx.CadastrosDeAlunos.SingleAsync();
        Assert.Equal(conta.Id, cadastro.AlunoId);
        Assert.Null(cadastro.NomeAvulso);
        Assert.Equal("Marcia", cadastro.ResponsavelNome);
    }

    // ─── Quem o gateway vai cobrar ────────────────────────────────────────────────────

    [Fact]
    public void O_CPF_da_cobranca_e_o_do_pagador_e_nunca_o_do_aluno()
    {
        var comResponsavel = new CadastroDoAluno { ResponsavelNome = "Marcia", ResponsavelCpf = "52998224725" };
        var contaDoAluno = new Jogador { Nome = "Pedro", Cpf = "39053344705" };

        // Com responsável, é o dele — mesmo o aluno tendo conta e CPF.
        Assert.Equal("52998224725", CadastrosDeAlunos.CpfDoPagador(comResponsavel, contaDoAluno));

        // Sem responsável, quem paga é o aluno.
        Assert.Equal("39053344705", CadastrosDeAlunos.CpfDoPagador(null, contaDoAluno));

        // Sem responsável e sem conta: não há CPF nenhum, e a tela precisa saber disso ANTES
        // de o professor tentar fechar o mês.
        Assert.Null(CadastrosDeAlunos.CpfDoPagador(null, null));

        // Responsável cadastrado sem CPF ainda não dá pra cobrar pelo app.
        Assert.Null(CadastrosDeAlunos.CpfDoPagador(new CadastroDoAluno { ResponsavelNome = "Marcia" }, contaDoAluno));
    }
}
