using Padelizou.Services;

namespace Padelizou.Tests;

// Nome a cada 6 meses, apelido a cada 1 mês.
//
// O nome é como as pessoas te acham: lista de inscritos, placar da mesa, ranking, histórico
// de torneio. Trocar toda semana faz o parceiro de terça não reconhecer quem entrou na dupla
// dele, e desliga a pessoa de hoje dos resultados dela de seis meses atrás.
public class TrocaDeNomeTests
{
    private static readonly DateTime Hoje = new(2026, 8, 5, 14, 0, 0);

    [Fact]
    public void Quem_nunca_trocou_troca_na_hora()
    {
        // O caso de quem se cadastrou com o nome errado (ou com "asdf" na pressa): não pode
        // ficar seis meses preso ao erro.
        Assert.True(TrocaDeNome.PodeTrocarNome(null, Hoje).Pode);
        Assert.True(TrocaDeNome.PodeTrocarApelido(null, Hoje).Pode);
    }

    [Fact]
    public void Nome_trava_por_6_meses()
    {
        var ontem = Hoje.AddDays(-1);

        var r = TrocaDeNome.PodeTrocarNome(ontem, Hoje);

        Assert.False(r.Pode);
        Assert.Equal(ontem.AddMonths(6), r.LiberaEm);
    }

    [Fact]
    public void Apelido_trava_por_1_mes_so()
    {
        // De propósito mais solto que o nome: o apelido existe pra ser o "como me chamam
        // agora", e trocá-lo é bem mais inocente.
        var faz10Dias = Hoje.AddDays(-10);

        Assert.False(TrocaDeNome.PodeTrocarApelido(faz10Dias, Hoje).Pode);
        Assert.True(TrocaDeNome.PodeTrocarApelido(Hoje.AddMonths(-2), Hoje).Pode);
    }

    [Fact]
    public void Passados_os_6_meses_libera()
    {
        var seisMesesAtras = Hoje.AddMonths(-6);

        Assert.True(TrocaDeNome.PodeTrocarNome(seisMesesAtras, Hoje).Pode);
    }

    [Fact]
    public void No_dia_exato_ja_pode()
    {
        // Fronteira: quem trocou em 05/02 pode de novo em 05/08, não em 06/08. Um `>` no
        // lugar do `>=` faria a pessoa voltar no dia seguinte sem entender por quê.
        var seisMesesAtras = new DateTime(2026, 2, 5, 14, 0, 0);

        Assert.True(TrocaDeNome.PodeTrocarNome(seisMesesAtras, new DateTime(2026, 8, 5, 14, 0, 0)).Pode);
    }

    [Fact]
    public void Faltando_um_minuto_ainda_nao_pode()
    {
        var seisMesesAtras = new DateTime(2026, 2, 5, 14, 0, 0);

        Assert.False(TrocaDeNome.PodeTrocarNome(seisMesesAtras, new DateTime(2026, 8, 5, 13, 59, 0)).Pode);
    }

    // ── O que conta como troca ────────────────────────────────────────────────────────────

    [Fact]
    public void Salvar_o_perfil_sem_mexer_no_nome_NAO_gasta_a_troca()
    {
        // O caso que mais importa: a pessoa entra pra corrigir o telefone e sai sem poder
        // trocar o nome por seis meses. Seria uma armadilha silenciosa.
        Assert.False(TrocaDeNome.Mudou("Felipe Bonamigo", "Felipe Bonamigo"));
    }

    [Fact]
    public void Espaco_sobrando_nao_conta_como_troca()
    {
        // O navegador e o teclado do celular mandam espaço à toa; ninguém trocou nada.
        Assert.False(TrocaDeNome.Mudou("Felipe", " Felipe "));
    }

    [Fact]
    public void Nulo_e_vazio_sao_a_mesma_coisa_no_apelido()
    {
        // "Sem apelido" e "apelido em branco" são o mesmo estado — trocar entre os dois não
        // é troca nenhuma.
        Assert.False(TrocaDeNome.Mudou(null, ""));
        Assert.False(TrocaDeNome.Mudou("", null));
    }

    [Fact]
    public void Trocar_de_verdade_conta()
    {
        Assert.True(TrocaDeNome.Mudou("Felipe", "Felipe Bonamigo"));
        Assert.True(TrocaDeNome.Mudou(null, "Bona"));
        Assert.True(TrocaDeNome.Mudou("Bona", null));
    }

    [Fact]
    public void Caixa_diferente_CONTA_como_troca()
    {
        // "FELIPE" → "Felipe" é uma correção que a pessoa quis fazer, e ela vale a carência.
        // Ignorar a caixa faria a correção passar sem carimbar a data — e aí ela trocaria de
        // novo no dia seguinte de graça.
        Assert.True(TrocaDeNome.Mudou("FELIPE", "Felipe"));
    }

    // ── O que a pessoa lê ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_recusa_diz_ATE_QUANDO_e_nao_so_que_nao_pode()
    {
        // Negar sem data faz a pessoa tentar amanhã, e no outro dia, sem nunca entender.
        var faz1Mes = Hoje.AddMonths(-1);
        var r = TrocaDeNome.PodeTrocarNome(faz1Mes, Hoje);

        var texto = TrocaDeNome.Recusa("O nome", r, Hoje);

        Assert.Contains(r.LiberaEm!.Value.ToString("dd/MM/yyyy"), texto);
        Assert.Contains("faltam", texto);
    }

    [Fact]
    public void A_tranquilizada_do_resto_do_perfil_NAO_vem_colada_na_recusa()
    {
        // Quem trocou nome E apelido no mesmo save lia "o resto do perfil você salva
        // normalmente" duas vezes seguidas. O controller acrescenta essa frase uma vez só,
        // no fim — texto repetido faz a tela parecer feita às pressas.
        var r = TrocaDeNome.PodeTrocarNome(Hoje.AddMonths(-1), Hoje);

        Assert.DoesNotContain(TrocaDeNome.ORestoFoiSalvo, TrocaDeNome.Recusa("O nome", r, Hoje));
        Assert.DoesNotContain(TrocaDeNome.ORestoFoiSalvo, TrocaDeNome.Recusa("O apelido", r, Hoje));
    }

    [Fact]
    public void O_aviso_depois_de_trocar_diz_a_data_da_proxima()
    {
        var texto = TrocaDeNome.AvisoDepoisDeTrocar("Nome", new DateTime(2027, 2, 5));

        Assert.Contains("05/02/2027", texto);
    }

    [Fact]
    public void O_aviso_ANTES_de_trocar_usa_singular_no_mes()
    {
        // "daqui a 1 meses" é o tipo de detalhe que faz o texto parecer feito às pressas.
        Assert.Contains("1 mês.", TrocaDeNome.AvisoAntesDeTrocar("o apelido", 1));
        Assert.Contains("6 meses.", TrocaDeNome.AvisoAntesDeTrocar("o nome", 6));
    }

    [Fact]
    public void Os_dias_que_faltam_arredondam_pra_CIMA()
    {
        // "Faltam 0 dias" não é resposta pra quem ainda não pode.
        var r = new TrocaDeNome.Resultado(false, Hoje.AddHours(3));

        Assert.Equal(1, r.DiasQueFaltam(Hoje));
    }

    [Fact]
    public void Quem_ja_pode_nao_tem_dias_faltando()
    {
        Assert.Equal(0, TrocaDeNome.PodeTrocarNome(null, Hoje).DiasQueFaltam(Hoje));
    }
}
