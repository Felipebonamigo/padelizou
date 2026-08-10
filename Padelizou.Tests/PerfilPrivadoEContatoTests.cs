using Microsoft.AspNetCore.Mvc;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O QUE A CHAVE "PERFIL PRIVADO" ESCONDE — e o que ela NUNCA escondeu.
//
// Duas coisas diferentes moravam na mesma chave. O perfil inteiro sumia pra quem só queria
// esconder o telefone, e a frase da tela de preferências prometia exatamente isso ("quem
// visitar seu perfil só vê sua foto e seu nome").
//
// A régua é do Felipe (10/08/2026): "todos os dados públicos aparecem mesmo para quem tem
// perfil privado — se foi campeão, pontos do ranking, etc.; o perfil privado é para evitar
// ver o Instagram, o WhatsApp da pessoa". Resultado de padel já está no ranking, na chave do
// torneio e no histórico do parceiro: escondê-lo no perfil não esconderia nada de ninguém.
//
// ⚠️ O que continua fechando o perfil INTEIRO é a CONTA EXCLUÍDA (LGPD) — e ela liga o
// `PerfilPrivado` junto (ver Services/ExclusaoDeConta), que foi como as duas regras se
// misturaram. Por isso o bloqueio olha `ExcluidoEm`, não a chave que a pessoa aperta.
public class PerfilPrivadoEContatoTests
{
    private static DbPadelContext Cenario()
    {
        var ctx = TestInfra.NovoContexto();
        ctx.Jogadores.AddRange(
            new Jogador { Id = 1, Nome = "Quem Visita", Cpf = "1", SenhaHash = "hash" },
            new Jogador
            {
                Id = 2, Nome = "Marina Reservada", Cpf = "2", SenhaHash = "hash",
                PerfilPrivado = true, Instagram = "marina.padel", Celular = "51999990000",
            },
            new Jogador
            {
                Id = 3, Nome = "Jogador removido", Cpf = "3",
                PerfilPrivado = true, ExcluidoEm = new DateTime(2026, 8, 1),
            },
            // Perfil ABERTO, com conta de verdade: é ele que separa "esconder do visitante
            // deslogado" de "esconder porque marcou privado". Sem ele, o teste do deslogado
            // passava pelo motivo errado — era o que acontecia até 10/08/2026.
            new Jogador
            {
                Id = 4, Nome = "Gilberto Aberto", Cpf = "4", SenhaHash = "hash",
                Instagram = "gilberto.padel", Celular = "51999995650",
            },
            // Sem SenhaHash: entrou como PARCEIRO de dupla, por CPF, e nunca assumiu a conta.
            new Jogador { Id = 5, Nome = "Parceiro Sem Conta", Cpf = "5", Celular = "51999991111" });
        ctx.SaveChanges();
        return ctx;
    }

    private static async Task<JogadoresController> PerfilAsync(DbPadelContext ctx, int deQuem, int? logadoComo)
    {
        var controller = TestInfra.NovoJogadoresController(ctx, logadoComo);
        Assert.IsType<ViewResult>(await controller.Perfil(deQuem));
        return controller;
    }

    [Fact]
    public async Task Perfil_privado_esconde_o_contato_pra_quem_visita()
    {
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: 1);

        Assert.True(controller.ViewBag.ContatoEscondido);
    }

    [Fact]
    public async Task Perfil_privado_NAO_esconde_o_resto_do_perfil()
    {
        // ⚠️ O teste que segura a régua: o perfil não pode mais parar antes de calcular. Se
        // alguém reintroduzir o retorno antecipado, `Pontos` volta a ser nulo aqui.
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: 1);

        // Nulo, não `false`: o ViewBag só ganha essa chave quando o perfil FECHA.
        Assert.Null(controller.ViewBag.PerfilBloqueado);
        Assert.NotNull(controller.ViewBag.Pontos);
        Assert.NotNull(controller.ViewBag.Titulos);
        Assert.NotNull(controller.ViewBag.Conquistas);
        // Os números da rede também: seguidores são tão públicos quanto os pontos.
        Assert.NotNull(controller.ViewBag.QuantosSeguidores);
    }

    [Fact]
    public async Task O_DONO_continua_vendo_o_proprio_contato()
    {
        // A chave é sobre quem VISITA. Esconder do dono seria esconder dele o que ele mesmo
        // cadastrou — e ele não teria como conferir se está certo.
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: 2);

        Assert.False(controller.ViewBag.ContatoEscondido);
    }

    [Fact]
    public async Task Conta_excluida_continua_com_o_perfil_FECHADO()
    {
        // Aqui o sumiço é o ponto: a pessoa pediu pra sair e deixou de ser identificável.
        var controller = await PerfilAsync(Cenario(), deQuem: 3, logadoComo: 1);

        Assert.True(controller.ViewBag.PerfilBloqueado);
        // E o perfil PARA antes de calcular qualquer coisa — este é o sinal de que parou.
        Assert.Null(controller.ViewBag.Pontos);
    }

    [Fact]
    public async Task Visitante_deslogado_tambem_nao_ve_o_contato()
    {
        // Deslogado é o caso que mais importa: é o link que circula em grupo de WhatsApp.
        var controller = await PerfilAsync(Cenario(), deQuem: 2, logadoComo: null);

        Assert.True(controller.ViewBag.ContatoEscondido);
        Assert.NotNull(controller.ViewBag.Pontos);
    }

    // ===================== DESLOGADO NÃO VÊ CONTATO DE NINGUÉM =====================
    // Conferido em produção em 10/08/2026: `GET /Jogadores/Perfil/169` sem login devolvia 200
    // com um `wa.me/55…` no HTML. Esta página é linkada do ranking público e o robots.txt do
    // site diz `Allow: /` — ou seja, telefone de jogador real ao alcance do buscador, enquanto
    // a Política de Privacidade prometia que celular nunca é público.

    [Fact]
    public async Task Deslogado_nao_ve_contato_nem_de_quem_deixou_o_perfil_ABERTO()
    {
        // ⚠️ O teste acima usa a Marina, que é PRIVADA — ele passaria mesmo sem esta regra.
        // Aqui o perfil está aberto: o único motivo pra esconder é não ter ninguém logado.
        var controller = await PerfilAsync(Cenario(), deQuem: 4, logadoComo: null);

        Assert.True(controller.ViewBag.ContatoEscondido);
        // E o resto do perfil segue à mostra: esconder contato não é fechar a página.
        Assert.NotNull(controller.ViewBag.Pontos);
    }

    [Fact]
    public async Task Jogador_LOGADO_continua_vendo_quem_deixou_o_perfil_aberto()
    {
        // O contrapeso: a trava do deslogado não pode ter matado o recurso pra quem usa o
        // site. Achar parceiro pra jogar é o que a rede serve.
        var controller = await PerfilAsync(Cenario(), deQuem: 4, logadoComo: 1);

        Assert.False(controller.ViewBag.ContatoEscondido);
    }

    [Fact]
    public void Deslogado_nao_pode_chamar_no_whatsapp_nem_perfil_aberto()
    {
        var gilberto = new Jogador { Id = 4, Nome = "Gilberto Aberto", SenhaHash = "hash", Celular = "51999995650" };

        Assert.False(ContatoDoJogador.PodeChamarNoWhatsApp(gilberto, quemOlhaId: null));
    }

    // ===================== PRÉ-CADASTRO NÃO EXPÕE CONTATO =====================
    // Quem foi cadastrado por um TERCEIRO (parceiro de dupla informado por CPF) nunca viu o
    // site, nunca leu a política e NÃO TEM LOGIN — o interruptor "perfil privado" existe, mas
    // não pra ele. Enquanto a conta não for assumida, o padrão é fechado.

    [Fact]
    public async Task Pre_cadastro_nao_tem_o_contato_a_mostra_nem_pra_quem_esta_logado()
    {
        var controller = await PerfilAsync(Cenario(), deQuem: 5, logadoComo: 1);

        Assert.True(controller.ViewBag.ContatoEscondido);
        // Não é perfil FECHADO: os jogos que ele disputou continuam sendo dele.
        Assert.Null(controller.ViewBag.PerfilBloqueado);
    }

    [Fact]
    public void Pre_cadastro_nao_pode_ser_chamado_no_whatsapp()
    {
        var semConta = new Jogador { Id = 5, Nome = "Parceiro Sem Conta", Celular = "51999991111" };

        Assert.True(semConta.EhPreCadastro);
        Assert.False(ContatoDoJogador.PodeChamarNoWhatsApp(semConta, quemOlhaId: 1));
    }

    [Fact]
    public void Quem_ASSUME_a_conta_volta_a_mandar_no_proprio_contato()
    {
        // O fecho é temporário de propósito: definir a senha é assumir a ficha (ver
        // AuthController.Cadastro), e daí em diante quem decide é o dono.
        var assumiu = new Jogador
        {
            Id = 5, Nome = "Parceiro Sem Conta", Celular = "51999991111", SenhaHash = "hash",
        };

        Assert.False(assumiu.EhPreCadastro);
        Assert.True(ContatoDoJogador.PodeChamarNoWhatsApp(assumiu, quemOlhaId: 1));
    }

    [Fact]
    public void Conta_excluida_NAO_e_pre_cadastro()
    {
        // As duas ficam sem SenhaHash (ver ExclusaoDeConta), e confundir as duas faria a
        // conta excluída "voltar a ser gente esperando entrar" em qualquer regra futura.
        var saiu = new Jogador { Id = 3, Nome = "Jogador removido", ExcluidoEm = new DateTime(2026, 8, 1) };

        Assert.False(saiu.EhPreCadastro);
    }

    // ===================== A MESMA RÉGUA FORA DO PERFIL =====================
    // Desde 10/08/2026 o aviso de jogo (Views/Avisos/Index) tem o botão "Chamar no WhatsApp":
    // quem publica um aviso está pedindo que alguém o chame, e a tela mostrava o dono do
    // horário sem dar jeito nenhum de falar com ele.
    //
    // O botão usa o MESMO ContatoDoJogador que o perfil. Estes testes existem pra que a
    // segunda tela a mostrar telefone não nasça mais frouxa que a primeira.

    [Fact]
    public void Perfil_privado_tambem_nao_pode_ser_chamado_no_whatsapp()
    {
        var marina = new Jogador
        {
            Id = 2, Nome = "Marina Reservada", SenhaHash = "hash",
            PerfilPrivado = true, Celular = "51999990000",
        };

        Assert.False(ContatoDoJogador.PodeChamarNoWhatsApp(marina, quemOlhaId: 1));
    }

    [Fact]
    public void Quem_deixou_o_perfil_aberto_pode_ser_chamado()
    {
        // ⚠️ `SenhaHash` não é enfeite do cenário: sem ela isto é um PRÉ-CADASTRO, e desde
        // 10/08/2026 pré-cadastro não mostra contato (ver mais acima).
        var gilberto = new Jogador
        {
            Id = 6, Nome = "Gilberto Gatti", SenhaHash = "hash", Celular = "(51) 99239-5650",
        };

        Assert.True(ContatoDoJogador.PodeChamarNoWhatsApp(gilberto, quemOlhaId: 1));
    }

    [Fact]
    public void Sem_celular_cadastrado_nao_ha_botao()
    {
        // Celular é campo opcional no cadastro, e um `wa.me/55` sem número atrás abre o
        // WhatsApp num contato que não existe.
        var semNumero = new Jogador { Id = 7, Nome = "Sem Numero", SenhaHash = "hash", Celular = "  " };

        Assert.False(ContatoDoJogador.PodeChamarNoWhatsApp(semNumero, quemOlhaId: 1));
    }

    [Fact]
    public void Conta_excluida_nao_pode_ser_chamada_nem_com_numero_gravado()
    {
        // A exclusão apaga o celular e liga o `PerfilPrivado` (ver ExclusaoDeConta) — o número
        // aqui é de propósito: finge um registro em que ela não terminou o serviço. Quem decide
        // é `ExcluidoEm`, não o resto ter dado certo.
        var saiu = new Jogador
        {
            Id = 7, Nome = "Jogador removido", Celular = "51999990000",
            ExcluidoEm = new DateTime(2026, 8, 1),
        };

        Assert.False(ContatoDoJogador.PodeChamarNoWhatsApp(saiu, quemOlhaId: 1));
    }
}
