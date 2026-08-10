using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// Força-bruta nas portas de entrada. Duas travas (ver TravaDeEntrada):
// - Login: janela POR CONTA, contando só falhas — o Wi-Fi lotado do clube no dia de
//   torneio não pode trancar gente legítima, e ataque distribuído não pode escapar.
// - Portão/recuperação/cadastro: janela POR IP, no rate limiter do ASP.NET.
//
// Desde 10/08/2026 o rate limiter também protege uma ação que NÃO é porta de entrada: a
// consulta de CPF da inscrição, que diz quem é o dono de um documento. Como ela exige login,
// a janela dela é por CONTA — e o mesmo raciocínio do Wi-Fi do clube vale ali.
public class TravaDeEntradaTests
{
    // ---------- A regra pura ----------

    [Fact]
    public void Nao_bloqueia_antes_do_limite()
    {
        var trava = new TravaDeEntrada();
        var agora = new DateTime(2026, 7, 30, 10, 0, 0);

        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela - 1; i++)
            trava.RegistrarFalha("felipe@exemplo.com", agora);

        Assert.False(trava.Bloqueada("felipe@exemplo.com", agora));
    }

    [Fact]
    public void Bloqueia_ao_atingir_o_limite_e_destrava_quando_a_janela_vence()
    {
        var trava = new TravaDeEntrada();
        var agora = new DateTime(2026, 7, 30, 10, 0, 0);

        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela; i++)
            trava.RegistrarFalha("felipe@exemplo.com", agora);

        Assert.True(trava.Bloqueada("felipe@exemplo.com", agora));
        // Passou a janela: a conta volta sozinha — ninguém precisa desbloquear à mão.
        Assert.False(trava.Bloqueada("felipe@exemplo.com", agora + TravaDeEntrada.Janela));
    }

    [Fact]
    public void Falha_depois_da_janela_vencida_comeca_contagem_nova()
    {
        var trava = new TravaDeEntrada();
        var agora = new DateTime(2026, 7, 30, 10, 0, 0);

        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela; i++)
            trava.RegistrarFalha("felipe@exemplo.com", agora);
        trava.RegistrarFalha("felipe@exemplo.com", agora + TravaDeEntrada.Janela);

        Assert.False(trava.Bloqueada("felipe@exemplo.com", agora + TravaDeEntrada.Janela));
    }

    [Fact]
    public void Acertar_a_senha_zera_a_janela_da_conta()
    {
        var trava = new TravaDeEntrada();
        var agora = new DateTime(2026, 7, 30, 10, 0, 0);

        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela; i++)
            trava.RegistrarFalha("felipe@exemplo.com", agora);
        trava.Zerar("felipe@exemplo.com");

        Assert.False(trava.Bloqueada("felipe@exemplo.com", agora));
    }

    [Fact]
    public void Caixa_e_espacos_nao_abrem_janelas_separadas_da_mesma_conta()
    {
        // A mesma normalização da entrada (BuscaJogador): "Bona" e "bona" são UMA conta.
        // Se cada grafia tivesse janela própria, o robô ganharia 10 tentativas por caixa.
        var trava = new TravaDeEntrada();
        var agora = new DateTime(2026, 7, 30, 10, 0, 0);

        trava.RegistrarFalha("Bona", agora);
        trava.RegistrarFalha("  bona  ", agora);
        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela - 2; i++)
            trava.RegistrarFalha("BONA", agora);

        Assert.True(trava.Bloqueada("bOnA", agora));
    }

    [Fact]
    public void Contas_diferentes_nao_dividem_janela()
    {
        // É o que salva o Wi-Fi do clube: cada pessoa gasta a própria janela.
        var trava = new TravaDeEntrada();
        var agora = new DateTime(2026, 7, 30, 10, 0, 0);

        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela; i++)
            trava.RegistrarFalha("robo@exemplo.com", agora);

        Assert.False(trava.Bloqueada("inocente@exemplo.com", agora));
    }

    // ---------- A trava dentro do Login ----------

    private static DbPadelContext ComJogadorDeSenhaConhecida(out string senhaCerta)
    {
        senhaCerta = "senha-certa-123";
        var ctx = TestInfra.NovoContexto();
        var jogador = new Jogador
        {
            Nome = "Felipe Bonamigo",
            Cpf = "11144477735",
            Email = "felipe@exemplo.com",
            Login = "bona",
        };
        jogador.SenhaHash = new PasswordHasher<Jogador>().HashPassword(jogador, senhaCerta);
        ctx.Jogadores.Add(jogador);
        ctx.SaveChanges();
        return ctx;
    }

    [Fact]
    public async Task Conta_trancada_recusa_ate_a_senha_certa()
    {
        // A checagem tem que vir ANTES de conferir a senha: se a senha certa passasse,
        // o robô só precisaria continuar tentando até acertar — a trava não travaria nada.
        using var ctx = ComJogadorDeSenhaConhecida(out var senhaCerta);
        var trava = new TravaDeEntrada();
        var controller = TestInfra.NovoAuthController(ctx, trava: trava);

        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela; i++)
        {
            var recusa = (ViewResult)await controller.Login("felipe@exemplo.com", "senha-errada");
            Assert.Equal("Login ou senha incorretos.", recusa.ViewData["Erro"]);
        }

        var bloqueio = (ViewResult)await controller.Login("felipe@exemplo.com", senhaCerta);
        Assert.Equal("Muitas tentativas seguidas. Espere alguns minutos e tente de novo.",
            bloqueio.ViewData["Erro"]);
    }

    [Fact]
    public async Task Conta_trancada_nao_tranca_o_vizinho_de_wifi()
    {
        using var ctx = ComJogadorDeSenhaConhecida(out _);
        var trava = new TravaDeEntrada();
        var controller = TestInfra.NovoAuthController(ctx, trava: trava);

        for (var i = 0; i < TravaDeEntrada.TentativasPorJanela; i++)
            await controller.Login("felipe@exemplo.com", "senha-errada");

        // Outra conta, mesmo "IP" (mesmo controller): recusa normal, não bloqueio.
        var recusa = (ViewResult)await controller.Login("outro@exemplo.com", "senha-errada");
        Assert.Equal("Login ou senha incorretos.", recusa.ViewData["Erro"]);
    }

    // ---------- O vigia das políticas do rate limiter ----------

    // Quem carrega janela do rate limiter, e por quê. Mudar esta lista é decisão de segurança.
    //
    // Quase todas são por IP: são ações ANÔNIMAS, e antes de alguém entrar não há conta pra
    // particionar. A exceção é a consulta de CPF, que exige login — ver o porquê logo abaixo.
    private static readonly Dictionary<string, string> AcoesComJanela = new()
    {
        ["AcessoAntecipadoController.Entrar"] =
            "O portão tem UMA senha compartilhada — sem trava seria o alvo mais barato do site.",
        ["AuthController.EsqueciSenha"] =
            "Cada POST dispara um e-mail; sem trava vira máquina de inundar caixa de entrada.",
        ["AuthController.RedefinirSenha"] =
            "Recebe token de recuperação; a trava encarece chutar tokens.",
        ["AuthController.Cadastro"] =
            "Cria conta; sem trava um robô enche o banco de conta falsa. Janela própria e " +
            "mais larga (PoliticaCadastro): formulário longo erra legitimamente várias vezes.",
        ["AuthController.BuscarCep"] =
            "Consulta anônima que sai pra internet (ViaCEP); sem trava, o site vira " +
            "ferramenta de varredura do serviço dos outros. Janela LARGA (PoliticaConsulta): " +
            "não é porta de entrada, é leitura em cache pra preencher formulário.",
        ["TorneiosController.BuscarJogadorPorCpf"] =
            "Diz quem é o dono de um CPF. 'Exigir o CPF inteiro' não é defesa: CPF válido se " +
            "GERA, e sem trava uma sessão logada varria a base. Janela por CONTA " +
            "(PoliticaConsultaLogada), não por IP: em dia de torneio o clube inteiro consulta " +
            "o CPF do parceiro pelo mesmo Wi-Fi, e por IP a trava prenderia gente legítima.",
    };

    private static List<(string Nome, EnableRateLimitingAttribute Attr)> AcoesComRateLimiting()
    {
        var assembly = typeof(TravaDeEntrada).Assembly;
        return assembly.GetTypes()
            .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Select(m => (Nome: $"{t.Name}.{m.Name}", Attr: m.GetCustomAttribute<EnableRateLimitingAttribute>())))
            .Where(x => x.Attr != null)
            .Select(x => (x.Nome, x.Attr!))
            .ToList();
    }

    [Fact]
    public void As_acoes_com_janela_sao_exatamente_as_previstas()
    {
        var deVerdade = AcoesComRateLimiting().Select(x => x.Nome).Distinct().OrderBy(n => n).ToList();
        var previstas = AcoesComJanela.Keys.OrderBy(n => n).ToList();

        Assert.Equal(previstas, deVerdade);
    }

    [Fact]
    public void Toda_janela_usa_uma_politica_registrada_no_Program()
    {
        // Nome de política que não existe no Program.cs não protege: o middleware lança
        // exceção na primeira requisição — mas só em quem RODA o app, não no build.
        var registradas = new[]
        {
            TravaDeEntrada.PoliticaPorIp,
            TravaDeEntrada.PoliticaCadastro,
            TravaDeEntrada.PoliticaConsulta,
            TravaDeEntrada.PoliticaConsultaLogada,
        };

        Assert.All(AcoesComRateLimiting(), x => Assert.Contains(x.Attr.PolicyName, registradas));
    }

    [Fact]
    public void A_consulta_de_CPF_e_a_unica_com_janela_por_CONTA()
    {
        // As outras são anônimas: antes de a pessoa entrar não há conta pra particionar, e
        // usar a política por conta ali cairia todo mundo no mesmo balde "sem-ip".
        var porConta = AcoesComRateLimiting()
            .Where(x => x.Attr.PolicyName == TravaDeEntrada.PoliticaConsultaLogada)
            .Select(x => x.Nome)
            .ToList();

        Assert.Equal(new[] { "TorneiosController.BuscarJogadorPorCpf" }, porConta);
    }

    [Fact]
    public void O_cadastro_tem_janela_propria_e_mais_larga_que_as_outras()
    {
        // Formulário longo: cada recusa (CPF já usado, login curto, e-mail repetido) gasta
        // uma tentativa, e duas pessoas criando conta no mesmo Wi-Fi somam no mesmo IP.
        // Encostar no limite aqui é o pior primeiro contato possível com o sistema.
        var cadastro = AcoesComRateLimiting().Single(x => x.Nome == "AuthController.Cadastro");

        Assert.Equal(TravaDeEntrada.PoliticaCadastro, cadastro.Attr.PolicyName);
        Assert.True(TravaDeEntrada.TentativasDeCadastro > TravaDeEntrada.TentativasPorJanela);
    }

    [Fact]
    public void O_login_fica_fora_da_janela_por_ip_de_proposito()
    {
        // No dia de torneio o clube inteiro loga pelo mesmo Wi-Fi (mesmo IP). O login usa
        // a janela por CONTA dentro da ação; ganhar TAMBÉM a janela por IP traria de volta
        // exatamente o problema que a separação resolve.
        Assert.DoesNotContain("AuthController.Login", AcoesComRateLimiting().Select(x => x.Nome));
    }
}
