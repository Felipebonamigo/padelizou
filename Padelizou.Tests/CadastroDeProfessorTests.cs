using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Em 28/07/2026, 7 professores de 7 nos três ambientes estavam sem cidade — e portanto ninguém no
// site inteiro conseguia marcar uma aula, sem nenhum erro aparecer em tela. Um aviso no painel não
// resolveu. Estes testes seguram a regra que agora obriga.
public class CadastroDeProfessorTests
{
    [Fact]
    public void Sem_cidade_a_primeira_cobranca_e_a_cidade()
    {
        // A ordem não é arbitrária: a tela de marcar aula pergunta a cidade primeiro, então
        // mandar cadastrar o local antes deixaria a pessoa fora da lista do mesmo jeito.
        Assert.Equal(PendenciaDoProfessor.Cidade, CadastroDeProfessor.Pendencia(false, false, false));
        Assert.Equal(PendenciaDoProfessor.Cidade, CadastroDeProfessor.Pendencia(false, true, true));
    }

    [Fact]
    public void Com_cidade_mas_sem_local_cobra_o_local()
    {
        Assert.Equal(PendenciaDoProfessor.Local, CadastroDeProfessor.Pendencia(true, false, false));
        Assert.Equal(PendenciaDoProfessor.Local, CadastroDeProfessor.Pendencia(true, false, true));
    }

    [Fact]
    public void Com_cidade_e_local_mas_sem_horario_cobra_o_horario()
    {
        // O caso que produção mostrou: 1 professor com cidade, 0 locais, 0 horários. Sem esta
        // linha, o aluno percorreria quatro degraus pra descobrir que não há horário nenhum.
        Assert.Equal(PendenciaDoProfessor.Horario, CadastroDeProfessor.Pendencia(true, true, false));
    }

    [Fact]
    public void Com_os_tres_nao_cobra_nada()
    {
        // Se isto quebrar, o professor fica preso num laço de redirecionamento e não abre o painel.
        Assert.Equal(PendenciaDoProfessor.Nenhuma, CadastroDeProfessor.Pendencia(true, true, true));
    }

    [Theory]
    [InlineData(PendenciaDoProfessor.Cidade, "MinhasCidades")]
    [InlineData(PendenciaDoProfessor.Local, "MeusLocais")]
    [InlineData(PendenciaDoProfessor.Horario, "MeusHorarios")]
    [InlineData(PendenciaDoProfessor.Nenhuma, "Dashboard")]
    public void Cada_pendencia_leva_pra_tela_que_resolve_ela(PendenciaDoProfessor pendencia, string acaoEsperada)
    {
        Assert.Equal(acaoEsperada, CadastroDeProfessor.AcaoPara(pendencia));
    }

    [Fact]
    public void Quem_esta_em_dia_nao_recebe_mensagem_de_cobranca()
    {
        Assert.Null(CadastroDeProfessor.MensagemPara(PendenciaDoProfessor.Nenhuma));
    }

    [Theory]
    [InlineData(PendenciaDoProfessor.Cidade)]
    [InlineData(PendenciaDoProfessor.Local)]
    [InlineData(PendenciaDoProfessor.Horario)]
    public void A_mensagem_diz_a_consequencia_e_nao_so_a_tarefa(PendenciaDoProfessor pendencia)
    {
        var texto = CadastroDeProfessor.MensagemPara(pendencia);

        Assert.False(string.IsNullOrWhiteSpace(texto));
        // "Cadastre sua cidade" soa burocrático e a pessoa deixa pra depois. A mensagem precisa
        // falar do ALUNO — é ele que some quando falta o dado.
        Assert.Contains("aluno", texto, StringComparison.OrdinalIgnoreCase);
    }

    // ---- A CONSULTA (PendenciaAsync) ----
    //
    // Estes testes existem porque a regra pura acima passava enquanto o sistema errava: a
    // decisão era certa, mas quem a alimentava perguntava a coisa errada ao banco. Achado
    // em 29/07/2026 testando a escada de ponta a ponta.

    private const int Prof = 10;

    private static async Task<DbPadelContext> MontarAsync(bool localAtivo, bool horarioAtivo,
        bool comCidade = true, bool comLocal = true, bool comHorario = true)
    {
        var ctx = TestInfra.NovoContexto();

        ctx.Jogadores.Add(new Jogador { Id = Prof, Nome = "Professor", Cpf = "10000000010", IsProfessor = true });
        ctx.Cidades.Add(new Cidade { Id = 1, Nome = "Gravataí", Estado = "RS" });
        if (comCidade) ctx.ProfessorCidades.Add(new ProfessorCidade { ProfessorId = Prof, CidadeId = 1 });

        if (comLocal)
        {
            ctx.LocaisAula.Add(new LocalAula
            {
                Id = 1, ProfessorId = Prof, Nome = "Chakra", Endereco = "Rua X",
                PrecoPadrao = 120, Ativo = localAtivo
            });
        }

        if (comHorario)
        {
            ctx.HorariosDisponiveis.Add(new HorarioDisponivel
            {
                Id = 1, ProfessorId = Prof, LocalAulaId = 1, DiaSemana = 1,
                HoraInicio = TimeSpan.FromHours(8), HoraFim = TimeSpan.FromHours(12),
                DuracaoMinutos = 60, Ativo = horarioAtivo
            });
        }

        await ctx.SaveChangesAsync();
        return ctx;
    }

    [Fact]
    public async Task Professor_completo_e_ativo_nao_tem_pendencia()
    {
        using var ctx = await MontarAsync(localAtivo: true, horarioAtivo: true);

        Assert.Equal(PendenciaDoProfessor.Nenhuma, await CadastroDeProfessor.PendenciaAsync(ctx, Prof));
    }

    [Fact]
    public async Task Local_DESATIVADO_conta_como_sem_local()
    {
        // O defeito real: a checagem antiga só perguntava se existia local, e "Desativar" não
        // pede confirmação. O painel do professor abria normalmente (200, nenhum aviso) enquanto
        // o aluno escolhia a cidade, escolhia o professor e batia em "nenhum local cadastrado
        // para este professor" — porque a tela do aluno só enxerga local ATIVO.
        using var ctx = await MontarAsync(localAtivo: false, horarioAtivo: true);

        Assert.Equal(PendenciaDoProfessor.Local, await CadastroDeProfessor.PendenciaAsync(ctx, Prof));
    }

    [Fact]
    public async Task Horario_ativo_pendurado_em_local_desativado_nao_conta()
    {
        // Mesma família, um degrau adiante: o professor tem um local ativo (sem horário) e outro
        // desativado (com horário). Contar o horário do local morto o deixaria passar na escada
        // e travar o aluno no passo 5, que é exatamente o que a escada existe pra evitar.
        using var ctx = await MontarAsync(localAtivo: false, horarioAtivo: true);
        ctx.LocaisAula.Add(new LocalAula
        {
            Id = 2, ProfessorId = Prof, Nome = "Outro", Endereco = "Rua Y",
            PrecoPadrao = 100, Ativo = true
        });
        await ctx.SaveChangesAsync();

        // Local ativo existe agora, então a cobrança anda pro horário — e o horário do local
        // desativado não vale.
        Assert.Equal(PendenciaDoProfessor.Horario, await CadastroDeProfessor.PendenciaAsync(ctx, Prof));
    }

    [Fact]
    public async Task Horario_desativado_conta_como_sem_horario()
    {
        using var ctx = await MontarAsync(localAtivo: true, horarioAtivo: false);

        Assert.Equal(PendenciaDoProfessor.Horario, await CadastroDeProfessor.PendenciaAsync(ctx, Prof));
    }

    [Fact]
    public async Task A_ordem_da_escada_vale_tambem_na_consulta()
    {
        using var semCidade = await MontarAsync(true, true, comCidade: false);
        Assert.Equal(PendenciaDoProfessor.Cidade, await CadastroDeProfessor.PendenciaAsync(semCidade, Prof));

        using var semLocal = await MontarAsync(true, true, comLocal: false, comHorario: false);
        Assert.Equal(PendenciaDoProfessor.Local, await CadastroDeProfessor.PendenciaAsync(semLocal, Prof));
    }
}
