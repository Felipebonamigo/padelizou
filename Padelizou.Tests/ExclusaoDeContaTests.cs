using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Exclusão de conta pelo titular (LGPD).
//
// A conta é ANONIMIZADA, não apagada — e não por preguiça: "Pagamento.JogadorId" é
// ON DELETE CASCADE (apagar levaria junto o registro fiscal que o MEI obriga a guardar) e
// "Dupla.Jogador1Id" é NO ACTION (o banco RECUSA apagar quem já jogou). Além disso, o
// placar de uma partida é dado de quatro pessoas.
//
// O que a lei exige é que a pessoa deixe de ser identificável. Estes testes são sobre isso.
public class ExclusaoDeContaTests
{
    private static readonly DateTime Agora = new(2026, 7, 28, 10, 0, 0);

    private static Jogador Cheio() => new()
    {
        Id = 42,
        Nome = "Felipe Carboni Bonamigo",
        Apelido = "Bona",
        Cpf = "11144477735",
        Email = "felipe@exemplo.com",
        Login = "bona",
        Celular = "51999998888",
        Cidade = "Porto Alegre",
        Estado = "RS",
        Cep = "90000-000",
        Logradouro = "Rua Exemplo, 123",
        Bairro = "Centro",
        Instagram = "felipebona",
        FotoPerfil = "/uploads/fotos-perfil/abc.jpg",
        SenhaHash = "hash-secreto",
        TokenRecuperacao = "token-vivo",
        TokenRecuperacaoExpiraEm = Agora.AddHours(1),
        IsProfessor = true,
        IsAdminGeral = true,
        IsAdminRaiz = true,
        NotificarEmail = true,
        NotificarWhatsApp = true,
    };

    // ── O que precisa sumir ───────────────────────────────────────────────────────────

    [Fact]
    public void Nenhum_dado_que_identifica_a_pessoa_sobrevive()
    {
        var j = Cheio();

        ExclusaoDeConta.Anonimizar(j, Agora);

        Assert.Equal("Jogador removido", j.Nome);
        Assert.Null(j.Apelido);
        Assert.Null(j.Email);
        Assert.Null(j.Login);
        Assert.Null(j.Celular);
        Assert.Null(j.Cidade);
        Assert.Null(j.Estado);
        Assert.Null(j.Cep);
        Assert.Null(j.Logradouro);
        Assert.Null(j.Bairro);
        Assert.Null(j.Instagram);
        Assert.Null(j.FotoPerfil);

        // O CPF não pode continuar lá: é O documento, e a busca por CPF acha qualquer um.
        Assert.DoesNotContain("11144477735", j.Cpf);
    }

    [Fact]
    public void A_conta_deixa_de_abrir_por_senha_e_por_link_de_recuperacao()
    {
        // Sem limpar o token, um "esqueci minha senha" antigo na caixa de e-mail ainda
        // abriria a conta depois de excluída.
        var j = Cheio();

        ExclusaoDeConta.Anonimizar(j, Agora);

        Assert.Null(j.SenhaHash);
        Assert.Null(j.TokenRecuperacao);
        Assert.Null(j.TokenRecuperacaoExpiraEm);
    }

    [Fact]
    public void Nenhum_canal_de_aviso_continua_ligado()
    {
        // Mandar e-mail pra quem pediu pra sair é o oposto do que ela pediu.
        var j = Cheio();

        ExclusaoDeConta.Anonimizar(j, Agora);

        Assert.False(j.NotificarEmail);
        Assert.False(j.NotificarWhatsApp);
        Assert.False(j.NotificarTorneiosAbertos);
        Assert.False(j.NotificarSeguidosTorneio);
        Assert.False(j.NotificarAvisoJogo);
        Assert.False(j.NotificarJogoAula);
        Assert.False(j.NotificarRaqueteLivre);
        Assert.False(j.AceitaConvitesJogo);
    }

    [Fact]
    public void Poder_de_administrador_nao_sobrevive_a_saida()
    {
        var j = Cheio();

        ExclusaoDeConta.Anonimizar(j, Agora);

        Assert.False(j.IsAdminGeral);
        Assert.False(j.IsAdminRaiz);
        Assert.False(j.IsProfessor);
    }

    [Fact]
    public void Fica_marcada_como_excluida_com_a_data()
    {
        var j = Cheio();

        ExclusaoDeConta.Anonimizar(j, Agora);

        Assert.Equal(Agora, j.ExcluidoEm);
        Assert.True(j.Excluido);
        Assert.True(j.PerfilPrivado);
    }

    // ── Nenhuma coluna string nova passa sem decisão ──────────────────────────────────
    //
    // O buraco do endereço nasceu assim: Cep/Logradouro/Bairro entraram no modelo em
    // 10/08/2026, esta exclusão já existia desde 28/07, e ninguém voltou aqui pra decidir.
    // Este teste enumera TODA propriedade string gravável de Jogador por reflexão — a próxima
    // coluna nova cai automaticamente em "nem apagada nem decidida" e QUEBRA o teste, em vez
    // de vazar em silêncio até alguém notar (ou até um usuário perguntar por que o dado
    // "excluído" ainda está lá).
    //
    // Continuam de propósito (não identificam quem saiu, ou são registro administrativo/fiscal
    // de responsabilidade de OUTRA pessoa, não do titular que saiu):
    //   Codigo — não usado como identificador de jogador (não é o Conquista.Codigo).
    //   VersaoDosTermosAceita — carimbo de versão de documento, não dado da pessoa.
    //   MolduraEscolhida, LadoQuadra, Lateralidade — preferência de uso, não identificam.
    //   Sexo — usado pra casar categoria (Masculina/Feminina/Mista); decisão do Felipe se deve
    //     ser tratado como dado sensível na saída (ver LGPD, art. 5º II) — hoje fica.
    //   AsaasWalletId — identificador financeiro ligado a Pagamento (mesma razão de Pagamento
    //     não cascatear: registro fiscal do MEI). Apagar quebraria reconciliação de split
    //     antigo; decisão de retenção fiscal também caberia ao Felipe, não a este teste.
    //   ApresentacaoProfessor, ExperienciaProfessor, PoliticaCancelamentoTexto — texto que o
    //     PRÓPRIO professor escreveu sobre o próprio negócio; IsProfessor já cai pra false.
    //   MotivoDoPedidoDeOrganizador, MotivoDaCortesia — registro administrativo do porquê de
    //     uma decisão (concedida por outra pessoa ou sobre ela), não dado de contato do titular.
    //   ModoComissao, PlanoProfessor — configuração/enum, não dado pessoal.
    private static readonly HashSet<string> CamposQueDevemSumir = new()
    {
        "Nome", "Apelido", "Cpf", "Login", "Celular", "Cidade", "Estado",
        "Cep", "Logradouro", "Bairro", "Email", "SenhaHash", "TokenRecuperacao",
        "FotoPerfil", "Instagram",
    };

    private static readonly HashSet<string> CamposQueContinuamDePropostio = new()
    {
        "Codigo", "VersaoDosTermosAceita", "MolduraEscolhida", "LadoQuadra", "Lateralidade",
        "Sexo", "AsaasWalletId", "ApresentacaoProfessor", "ExperienciaProfessor",
        "PoliticaCancelamentoTexto", "MotivoDoPedidoDeOrganizador", "MotivoDaCortesia",
        "ModoComissao", "PlanoProfessor",
    };

    [Fact]
    public void Toda_propriedade_string_gravavel_de_jogador_tem_decisao_explicita()
    {
        var propriedades = typeof(Jogador).GetProperties()
            .Where(p => p.PropertyType == typeof(string) && p.CanWrite && p.SetMethod!.IsPublic)
            .Select(p => p.Name)
            .ToList();

        var semDecisao = propriedades
            .Where(nome => !CamposQueDevemSumir.Contains(nome) && !CamposQueContinuamDePropostio.Contains(nome))
            .ToList();

        Assert.True(semDecisao.Count == 0,
            "Coluna(s) nova(s) sem decisão em ExclusaoDeContaTests: " + string.Join(", ", semDecisao)
            + ". Decida se ExclusaoDeConta.Anonimizar deve limpá-la e adicione ao conjunto certo aqui.");
    }

    // ── O CPF de quem saiu ────────────────────────────────────────────────────────────

    [Fact]
    public void Cpf_de_quem_saiu_nao_pode_parecer_cpf_de_verdade()
    {
        // Se fosse só um número, poderia bater com o CPF real de outra pessoa — e o índice
        // único derrubaria a exclusão dela. Começando com letra, isso é impossível.
        var cpf = ExclusaoDeConta.CpfDeQuemSaiu(42);

        Assert.StartsWith("EX", cpf);
        Assert.False(BuscaJogador.PareceCpf(cpf));
        Assert.True(cpf.Length <= 11);   // a coluna é varchar(11)
    }

    [Fact]
    public void Cada_conta_excluida_tem_seu_proprio_cpf()
    {
        // Duas contas excluídas não podem colidir no índice único de CPF.
        var cpfs = new[] { 1, 42, 999, 999999999 }.Select(ExclusaoDeConta.CpfDeQuemSaiu).ToList();

        Assert.Equal(cpfs.Count, cpfs.Distinct().Count());
        Assert.All(cpfs, c => Assert.True(c.Length <= 11));
    }

    // ── Quem NÃO pode sair sem antes resolver ─────────────────────────────────────────

    [Fact]
    public void Quem_nao_deixa_ninguem_na_mao_pode_sair()
    {
        Assert.Null(ExclusaoDeConta.ProblemaParaExcluir(
            ehUnicoAdminDoSistema: false, torneiosEmQueEhUnicoOrganizador: 0));
    }

    [Fact]
    public void Ultimo_admin_do_sistema_nao_sai()
    {
        // Sairia e ninguém mais conseguiria administrar a plataforma.
        var problema = ExclusaoDeConta.ProblemaParaExcluir(true, 0);

        Assert.NotNull(problema);
        Assert.Contains("único administrador", problema!);
    }

    [Fact]
    public void Organizador_unico_de_torneio_em_andamento_nao_sai()
    {
        // O torneio ficaria sem ninguém pra lançar placar, com gente jogando.
        var problema = ExclusaoDeConta.ProblemaParaExcluir(false, torneiosEmQueEhUnicoOrganizador: 2);

        Assert.NotNull(problema);
        Assert.Contains("2", problema!);
        Assert.Contains("organizador", problema!);
    }

    // ── Sumir das buscas ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Conta_excluida_some_da_busca_de_jogadores()
    {
        using var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.Add(new Jogador { Nome = "Ana Prado", Cpf = "11144477735" });
        var saiu = new Jogador { Nome = "Ana Removida", Cpf = "52998224725" };
        ctx.Jogadores.Add(saiu);
        await ctx.SaveChangesAsync();

        ExclusaoDeConta.Anonimizar(saiu, Agora);
        await ctx.SaveChangesAsync();

        var achados = await BuscaJogador.BuscarAsync(ctx, "Ana");

        Assert.Single(achados);
        Assert.Equal("Ana Prado", achados[0].Nome);
    }

    [Fact]
    public async Task Conta_excluida_nao_entra_nem_em_listagem_sem_termo()
    {
        // Filtrar sem termo é usado por telas que combinam outros filtros (cidade, clube).
        // Se o filtro de excluído não valesse aí, a pessoa reapareceria nessas listas.
        using var ctx = TestInfra.NovoContexto();
        var saiu = new Jogador { Nome = "Quem Saiu", Cpf = "52998224725" };
        ctx.Jogadores.Add(saiu);
        await ctx.SaveChangesAsync();

        ExclusaoDeConta.Anonimizar(saiu, Agora);
        await ctx.SaveChangesAsync();

        Assert.Empty(BuscaJogador.Filtrar(ctx.Jogadores, null).ToList());
    }

    [Fact]
    public async Task Conta_excluida_nao_entra_mais_pelo_email_antigo()
    {
        using var ctx = TestInfra.NovoContexto();
        var saiu = new Jogador
        {
            Nome = "Quem Saiu", Cpf = "52998224725",
            Email = "quemsaiu@exemplo.com", Login = "quemsaiu", SenhaHash = "hash",
        };
        ctx.Jogadores.Add(saiu);
        await ctx.SaveChangesAsync();

        ExclusaoDeConta.Anonimizar(saiu, Agora);
        await ctx.SaveChangesAsync();

        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, "quemsaiu@exemplo.com"));
        Assert.Null(await BuscaJogador.PorIdentificadorAsync(ctx, "quemsaiu"));
    }
}
