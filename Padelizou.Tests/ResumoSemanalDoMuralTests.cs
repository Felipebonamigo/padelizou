using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// O ÚNICO empurrão que os Desafios dão (DESAFIOS.md, seção 5).
//
// ⚠️ O que estes testes guardam não é o texto: é a RÉGUA DE QUEM RECEBE. O mural é pull de
// propósito, e a única exceção só continua sendo exceção enquanto a lista for estreita. Cada
// `Assert.False` aqui é uma pessoa fora de uma lista de e-mail semanal.
public class ResumoSemanalDoMuralTests
{
    private static Jogador Jogador(int id = 1, string? cidade = "Gravataí") => new()
    {
        Id = id,
        Nome = $"Jogador {id:00}",
        Cpf = $"999000000{id:00}",
        Cidade = cidade,
        SenhaHash = "tem-conta",
    };

    private static AnuncioDeDesafio Anuncio(int j1, int j2, params string[] cidades)
    {
        var anuncio = new AnuncioDeDesafio
        {
            Jogador1Id = j1,
            Jogador2Id = j2,
            Status = AnuncioDeDesafio.Publicado,
            ValeAte = new DateTime(2026, 8, 23, 23, 59, 59),
        };

        foreach (var nome in cidades)
        {
            anuncio.Cidades.Add(new AnuncioDesafioCidade
            {
                Cidade = new Cidade { Nome = nome },
            });
        }

        return anuncio;
    }

    // ── Quando ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void So_manda_na_quinta_de_manha()
    {
        var quintaCedo = new DateTime(2026, 8, 20, 8, 0, 0);      // quinta, antes das 9h
        var quintaManha = new DateTime(2026, 8, 20, 9, 0, 0);
        var quarta = new DateTime(2026, 8, 19, 10, 0, 0);
        var sexta = new DateTime(2026, 8, 21, 10, 0, 0);

        Assert.Equal(DayOfWeek.Thursday, quintaManha.DayOfWeek);

        Assert.True(ResumoSemanalDoMural.EhHoraDeEnviar(quintaManha, null));
        Assert.False(ResumoSemanalDoMural.EhHoraDeEnviar(quintaCedo, null));
        Assert.False(ResumoSemanalDoMural.EhHoraDeEnviar(quarta, null));
        Assert.False(ResumoSemanalDoMural.EhHoraDeEnviar(sexta, null));
    }

    [Fact]
    public void Nao_manda_duas_vezes_no_mesmo_dia()
    {
        // ⚠️ Aviso repetido é o que faz a pessoa desligar a notificação — e ela leva junto o
        // aviso que importa. A marca é lida do banco justamente pra sobreviver a deploy: um
        // restart numa quinta às 10h reenviaria pra base inteira sem isto.
        var dezDaManha = new DateTime(2026, 8, 20, 10, 0, 0);
        var quatroDaTarde = new DateTime(2026, 8, 20, 16, 0, 0);

        Assert.False(ResumoSemanalDoMural.EhHoraDeEnviar(quatroDaTarde, ultimoEnvio: dezDaManha));

        // Mas na quinta SEGUINTE volta a valer.
        Assert.True(ResumoSemanalDoMural.EhHoraDeEnviar(
            new DateTime(2026, 8, 27, 10, 0, 0), ultimoEnvio: dezDaManha));
    }

    // ── Quantas duplas ────────────────────────────────────────────────────────────────

    [Fact]
    public void Nao_conta_o_proprio_anuncio()
    {
        // Contar a si mesma faria o número prometer uma dupla que ela não pode desafiar.
        var noMural = new[] { Anuncio(1, 2), Anuncio(3, 4), Anuncio(5, 6) };

        Assert.Equal(2, ResumoSemanalDoMural.QuantasDuplasPara(noMural, jogadorId: 1, "Gravataí"));
        Assert.Equal(2, ResumoSemanalDoMural.QuantasDuplasPara(noMural, jogadorId: 2, "Gravataí"));
        Assert.Equal(3, ResumoSemanalDoMural.QuantasDuplasPara(noMural, jogadorId: 9, "Gravataí"));
    }

    [Fact]
    public void Cidade_casa_por_chave_e_nao_por_texto()
    {
        // ⚠️ "GRAVATAI" e "Gravataí" são a mesma cidade. Comparar na lata faria o resumo dizer
        // "nenhuma dupla" pra quem digitou o nome com acento diferente do catálogo — e o defeito
        // seria mudo: o aviso simplesmente não sai.
        var noMural = new[] { Anuncio(3, 4, "GRAVATAI"), Anuncio(5, 6, "Gravatai") };

        Assert.Equal(2, ResumoSemanalDoMural.QuantasDuplasPara(noMural, 1, "Gravataí"));
    }

    [Fact]
    public void Anuncio_de_outra_cidade_nao_conta()
    {
        var noMural = new[] { Anuncio(3, 4, "Gravataí"), Anuncio(5, 6, "São Paulo") };

        Assert.Equal(1, ResumoSemanalDoMural.QuantasDuplasPara(noMural, 1, "Gravataí"));
    }

    [Fact]
    public void Sem_cidade_dos_dois_lados_o_anuncio_conta()
    {
        // Anúncio sem cidade aceita qualquer lugar; pessoa sem cidade no perfil não tem por
        // onde ser filtrada. Nos dois casos, esconder seria pior que mostrar.
        var semRestricao = new[] { Anuncio(3, 4) };
        Assert.Equal(1, ResumoSemanalDoMural.QuantasDuplasPara(semRestricao, 1, "Gravataí"));

        var comCidade = new[] { Anuncio(3, 4, "São Paulo") };
        Assert.Equal(1, ResumoSemanalDoMural.QuantasDuplasPara(comCidade, 1, cidadeDoJogador: null));
    }

    // ── Quem recebe ───────────────────────────────────────────────────────────────────

    [Fact]
    public void Quem_nunca_usou_os_desafios_nao_recebe()
    {
        // ⚠️ É a linha que separa RETENÇÃO de divulgação. Mandar pra quem nunca entrou é o
        // broadcast que a seção 5 proíbe, só que uma vez por semana.
        Assert.False(ResumoSemanalDoMural.VaiReceber(Jogador(),
            jaUsouDesafios: false, temAnuncioNoMural: false, duplasDisponiveis: 7));
    }

    [Fact]
    public void Quem_ja_esta_no_mural_nao_recebe()
    {
        // Pra ela o aviso é ruído — ela já fez o que ele pede.
        Assert.False(ResumoSemanalDoMural.VaiReceber(Jogador(),
            jaUsouDesafios: true, temAnuncioNoMural: true, duplasDisponiveis: 7));
    }

    [Fact]
    public void Mural_fraco_nao_gasta_o_empurrao_da_semana()
    {
        Assert.False(ResumoSemanalDoMural.VaiReceber(Jogador(),
            jaUsouDesafios: true, temAnuncioNoMural: false, duplasDisponiveis: 0));

        Assert.False(ResumoSemanalDoMural.VaiReceber(Jogador(),
            jaUsouDesafios: true, temAnuncioNoMural: false, duplasDisponiveis: 1));

        Assert.True(ResumoSemanalDoMural.VaiReceber(Jogador(),
            jaUsouDesafios: true, temAnuncioNoMural: false,
            duplasDisponiveis: ResumoSemanalDoMural.MinimoDeDuplas));
    }

    [Fact]
    public void O_interruptor_de_aviso_de_jogo_e_respeitado()
    {
        var desligou = Jogador();
        desligou.NotificarAvisoJogo = false;

        Assert.False(ResumoSemanalDoMural.VaiReceber(desligou,
            jaUsouDesafios: true, temAnuncioNoMural: false, duplasDisponiveis: 7));
    }

    [Fact]
    public void Conta_excluida_e_pre_cadastro_ficam_de_fora()
    {
        // Excluída (LGPD) pediu pra sair. Pré-cadastro nunca viu tela nenhuma e não tem login
        // pra abrir o mural — o aviso levaria a uma porta que não abre pra ela.
        var excluida = Jogador();
        excluida.ExcluidoEm = new DateTime(2026, 8, 1);

        var preCadastro = Jogador();
        preCadastro.SenhaHash = null;

        Assert.False(ResumoSemanalDoMural.VaiReceber(excluida, true, false, 7));
        Assert.False(ResumoSemanalDoMural.VaiReceber(preCadastro, true, false, 7));
    }

    [Fact]
    public void Quem_passa_em_tudo_recebe()
    {
        Assert.True(ResumoSemanalDoMural.VaiReceber(Jogador(),
            jaUsouDesafios: true, temAnuncioNoMural: false, duplasDisponiveis: 7));
    }

    // ── O texto ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void O_texto_diz_o_numero_e_o_lugar()
    {
        var comCidade = AvisoDoDesafio.ResumoDoMural(7, "Gravataí");
        Assert.Contains("7", comCidade.Titulo);
        Assert.Contains("Gravataí", comCidade.Corpo);

        // Sem cidade a frase continua fazendo sentido — nada de "duplas em  esperando".
        var semCidade = AvisoDoDesafio.ResumoDoMural(3, null);
        Assert.Contains("3", semCidade.Corpo);
        Assert.DoesNotContain("  ", semCidade.Corpo);
        Assert.DoesNotContain(" em ,", semCidade.Corpo);
    }
}
