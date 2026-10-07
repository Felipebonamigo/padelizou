using Padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O TURNO OFERECIDO PRECISA EXISTIR NO TORNEIO.
//
// 🗣️ Felipe, 07/10/2026, com o print do painel do NATA PADEL TOUR aberto: *"aqui está marcando
// quinta e sexta, mas o torneio só inicia no sábado"*.
//
// ⚠️ NÃO É COSMÉTICO, E A PROVA ESTÁ NO `JanelasDeImpedimento.Da`: ele só devolve janela
// quando `DiaDoTorneio` acha aquele dia da semana dentro dos 3 dias que seguem o início. Num
// torneio que começa no sábado, "Sexta à noite" não gera janela nenhuma — a dupla marca, PAGA
// a `TaxaPorImpedimento` por ela, e o sorteio ignora. Dinheiro por uma janela que não existe,
// sem erro em lugar nenhum.
//
// A régua antiga era `Torneio.QuintaEhDiaDoTorneio` (`DataInicio é quinta || PermiteQuinta`),
// e ela não segurava nada no lugar em que era usada: `A && (B || A)` é `A`, então
// `QuintaEhDiaDoTorneio && PermiteImpedimentoQuintaNoite` sempre deu `PermiteImpedimentoQuintaNoite`.
// A sexta nunca teve checagem de dia nenhuma.
public class TurnosQueOTorneioTemTests
{
    private static Torneio TorneioQueComecaEm(DateTime inicio) => new()
    {
        Nome = "T", Codigo = "T1", Status = "Inscrições Abertas",
        DataInicio = inicio,
        PermiteImpedimentos = true,
        PermiteImpedimentoQuintaNoite = true,
        PermiteImpedimentoSextaNoite = true,
        PermiteImpedimentoSabadoManha = true,
        PermiteImpedimentoSabadoTarde = true,
    };

    private static readonly DateTime Sabado = new(2026, 10, 10);
    private static readonly DateTime Sexta = new(2026, 10, 9);
    private static readonly DateTime Quinta = new(2026, 10, 8);

    [Fact]
    public void Torneio_que_comeca_no_sabado_nao_oferece_quinta_nem_sexta()
    {
        var torneio = TorneioQueComecaEm(Sabado);

        var oferecidos = TurnosDoTorneio.Oferecidos(torneio);

        Assert.DoesNotContain(TurnoDoImpedimento.QuintaNoite, oferecidos);
        Assert.DoesNotContain(TurnoDoImpedimento.SextaNoite, oferecidos);
        Assert.Contains(TurnoDoImpedimento.SabadoManha, oferecidos);
        Assert.Contains(TurnoDoImpedimento.SabadoTarde, oferecidos);
        Assert.Contains(TurnoDoImpedimento.Nenhum, oferecidos);
    }

    [Fact]
    public void Torneio_que_comeca_na_sexta_oferece_sexta_e_sabado_mas_nao_quinta()
    {
        var torneio = TorneioQueComecaEm(Sexta);

        var oferecidos = TurnosDoTorneio.Oferecidos(torneio);

        Assert.DoesNotContain(TurnoDoImpedimento.QuintaNoite, oferecidos);
        Assert.Contains(TurnoDoImpedimento.SextaNoite, oferecidos);
        Assert.Contains(TurnoDoImpedimento.SabadoManha, oferecidos);
    }

    [Fact]
    public void Torneio_que_comeca_na_quinta_oferece_os_tres_dias()
    {
        var torneio = TorneioQueComecaEm(Quinta);

        var oferecidos = TurnosDoTorneio.Oferecidos(torneio);

        Assert.Contains(TurnoDoImpedimento.QuintaNoite, oferecidos);
        Assert.Contains(TurnoDoImpedimento.SextaNoite, oferecidos);
        Assert.Contains(TurnoDoImpedimento.SabadoManha, oferecidos);
    }

    [Fact]
    public void O_organizador_ainda_manda_no_que_desligou()
    {
        // O dia existir não basta: o interruptor da criação continua valendo.
        var torneio = TorneioQueComecaEm(Sexta);
        torneio.PermiteImpedimentoSextaNoite = false;

        Assert.DoesNotContain(TurnoDoImpedimento.SextaNoite, TurnosDoTorneio.Oferecidos(torneio));
    }

    [Fact]
    public void O_turno_JA_MARCADO_continua_na_lista_mesmo_que_o_torneio_nao_o_tenha()
    {
        // Senão a dupla que marcou "Sexta à noite" antes desta regra fica PRESA nela: a opção
        // some da tela e não há como escolher outra coisa — nem o organizador consegue tirar.
        var torneio = TorneioQueComecaEm(Sabado);

        var oferecidos = TurnosDoTorneio.Oferecidos(torneio, TurnoDoImpedimento.SextaNoite);

        Assert.Contains(TurnoDoImpedimento.SextaNoite, oferecidos);
        Assert.DoesNotContain(TurnoDoImpedimento.QuintaNoite, oferecidos);
    }

    [Fact]
    public void Sem_data_de_inicio_vale_o_que_o_organizador_permitiu()
    {
        // Torneio em montagem ainda não tem dia nenhum. Esconder os quatro turnos aqui seria
        // trocar um defeito por outro: a tela de inscrição ficaria sem a seção inteira.
        var torneio = TorneioQueComecaEm(Sabado);
        torneio.DataInicio = null;

        var oferecidos = TurnosDoTorneio.Oferecidos(torneio);

        Assert.Contains(TurnoDoImpedimento.SextaNoite, oferecidos);
        Assert.Contains(TurnoDoImpedimento.QuintaNoite, oferecidos);
    }

    [Fact]
    public void TODO_turno_oferecido_vira_janela_de_verdade_no_sorteio()
    {
        // A invariante que dá sentido a tudo isto: oferecer um turno é prometer que marcar ele
        // tira a dupla daquele horário. `JanelasDeImpedimento.Da` é quem cumpre a promessa —
        // se ele devolve vazio, o turno não devia estar na lista.
        foreach (var inicio in new[] { Quinta, Sexta, Sabado })
        {
            var torneio = TorneioQueComecaEm(inicio);

            foreach (var turno in TurnosDoTorneio.Oferecidos(torneio))
            {
                if (turno == TurnoDoImpedimento.Nenhum) continue;

                var dupla = new Dupla();
                AlteracaoDeImpedimento.Aplicar(dupla, torneio, turno, quemAlterou: 1, DateTime.Now);

                Assert.True(JanelasDeImpedimento.Da(torneio, dupla).Any(),
                    $"torneio que começa em {inicio:ddd} oferece \"{AlteracaoDeImpedimento.Rotulo(turno)}\", "
                    + "mas esse turno não vira janela nenhuma no sorteio.");
            }
        }
    }

    [Fact]
    public void A_concentracao_segue_a_mesma_regua()
    {
        // O card do organizador tem os DOIS seletores lado a lado, e o de concentração oferecia
        // "Os 2 jogos na sexta à noite" no torneio de sábado — `ConcentracaoDeJogos.JanelaDoTurno`
        // devolve null ali, então o favor era concedido e não acontecia nada.
        var torneio = TorneioQueComecaEm(Sabado);

        var oferecidos = TurnosDoTorneio.OferecidosParaConcentrar(torneio);

        Assert.DoesNotContain(TurnoDeConcentracao.SextaNoite, oferecidos);
        Assert.Contains(TurnoDeConcentracao.SabadoManha, oferecidos);
        Assert.Contains(TurnoDeConcentracao.Nenhuma, oferecidos);
    }

    [Fact]
    public void A_concentracao_ja_posta_tambem_continua_na_lista()
    {
        var torneio = TorneioQueComecaEm(Sabado);

        Assert.Contains(TurnoDeConcentracao.SextaNoite,
            TurnosDoTorneio.OferecidosParaConcentrar(torneio, TurnoDeConcentracao.SextaNoite));
    }

    // ── A REGRA NÃO PODE MORAR SÓ NA TELA ────────────────────────────────────────────────
    //
    // Página velha em cache, POST feito à mão e o aplicativo instalado (que guarda JS por mais
    // uma abertura) continuam mandando o turno que a lista deixou de oferecer. Se o servidor
    // aceitar, a dupla volta a pagar por uma janela que não existe — que é o defeito inteiro.

    [Fact]
    public void O_jogador_nao_consegue_marcar_um_turno_que_o_torneio_nao_tem()
    {
        var torneio = TorneioQueComecaEm(Sabado);
        var dupla = new Dupla { Id = 7, Jogador1Id = 1 };

        var motivo = AlteracaoDeImpedimento.MotivoParaNaoAlterar(
            dupla, torneio, quemPede: 1, TurnoDoImpedimento.SextaNoite);

        Assert.NotNull(motivo);
    }

    [Fact]
    public void O_jogador_continua_podendo_TIRAR_um_turno_que_o_torneio_nao_tem()
    {
        // O avesso da linha de cima, e tão importante quanto: quem já está preso numa sexta
        // que não existe precisa conseguir sair dela.
        var torneio = TorneioQueComecaEm(Sabado);
        var dupla = new Dupla { Id = 7, Jogador1Id = 1, ImpedimentoSextaNoite = true };

        Assert.Null(AlteracaoDeImpedimento.MotivoParaNaoAlterar(
            dupla, torneio, quemPede: 1, TurnoDoImpedimento.Nenhum));

        Assert.Null(AlteracaoDeImpedimento.MotivoParaNaoAlterar(
            dupla, torneio, quemPede: 1, TurnoDoImpedimento.SabadoManha));
    }

    [Fact]
    public void O_organizador_tambem_nao_marca_turno_que_o_torneio_nao_tem()
    {
        var torneio = TorneioQueComecaEm(Sabado);
        var dupla = new Dupla { Id = 7, Jogador1Id = 1 };

        var motivo = AlteracaoDeImpedimento.MotivoParaOrganizadorNaoAlterar(
            dupla, torneio, jaSorteou: false, TurnoDoImpedimento.QuintaNoite);

        Assert.NotNull(motivo);

        // E o que o torneio TEM continua passando.
        Assert.Null(AlteracaoDeImpedimento.MotivoParaOrganizadorNaoAlterar(
            dupla, torneio, jaSorteou: false, TurnoDoImpedimento.SabadoTarde));
    }

    [Fact]
    public void O_organizador_nao_concentra_num_turno_que_o_torneio_nao_tem()
    {
        // Sem esta trava a concentração é gravada, o aviso sai pros dois jogadores ("vamos
        // tentar pôr os 2 jogos na sexta") e `JanelaDoTurno` devolve null — o favor nunca
        // acontece e ninguém fica sabendo.
        var torneio = TorneioQueComecaEm(Sabado);
        var dupla = new Dupla { Id = 7, Jogador1Id = 1 };

        Assert.NotNull(ConcentracaoDeJogos.MotivoParaOrganizadorNaoConcentrar(
            dupla, torneio, jaSorteou: false, TurnoDeConcentracao.SextaNoite));

        Assert.Null(ConcentracaoDeJogos.MotivoParaOrganizadorNaoConcentrar(
            dupla, torneio, jaSorteou: false, TurnoDeConcentracao.SabadoManha));
    }
}
