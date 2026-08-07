using Padelizou.Services;

namespace Padelizou.Tests;

// Nome: UMA troca só. Apelido: a cada 1 mês.
//
// O nome é como as pessoas te acham: lista de inscritos, placar da mesa, ranking, histórico
// de torneio — e, desde 06/08/2026, é também como o **Ranking RS** casa o atleta (o
// casamento lá é por nome, testado contra a API deles). Nome que dança é jogador que some do
// ranking parceiro no meio da temporada. A troca única existe pra um caso só: quem digitou
// errado na pressa não pode ficar preso ao erro pra sempre.
public class TrocaDeNomeTests
{
    private static readonly DateTime Hoje = new(2026, 8, 5, 14, 0, 0);

    [Fact]
    public void Quem_nunca_trocou_troca_na_hora()
    {
        // O caso de quem se cadastrou com o nome errado (ou com "asdf" na pressa).
        Assert.True(TrocaDeNome.PodeTrocarNome(null, Hoje).Pode);
        Assert.True(TrocaDeNome.PodeTrocarApelido(null, Hoje).Pode);
    }

    [Fact]
    public void O_nome_trava_DE_VEZ_depois_da_primeira_troca()
    {
        var r = TrocaDeNome.PodeTrocarNome(Hoje.AddDays(-1), Hoje);

        Assert.False(r.Pode);
        Assert.True(r.Definitivo);
        // Sem data prometida: não existe "libera em", e prometer uma seria mentira.
        Assert.Null(r.LiberaEm);
    }

    [Fact]
    public void Nem_depois_de_anos_o_nome_libera()
    {
        // A tentação seria "depois de muito tempo tanto faz" — mas o ranking parceiro casa
        // por nome o tempo todo, não só no primeiro semestre.
        Assert.False(TrocaDeNome.PodeTrocarNome(Hoje.AddYears(-5), Hoje).Pode);
    }

    [Fact]
    public void Apelido_trava_por_1_mes_so()
    {
        // De propósito mais solto que o nome: o apelido existe pra ser o "como me chamam
        // agora", e trocá-lo é bem mais inocente — ele não vai pro Ranking RS.
        var faz10Dias = Hoje.AddDays(-10);

        Assert.False(TrocaDeNome.PodeTrocarApelido(faz10Dias, Hoje).Pode);
        Assert.True(TrocaDeNome.PodeTrocarApelido(Hoje.AddMonths(-2), Hoje).Pode);
    }

    [Fact]
    public void No_dia_exato_o_apelido_ja_pode()
    {
        // Fronteira: quem trocou em 05/07 pode de novo em 05/08, não em 06/08. Um `>` no
        // lugar do `>=` faria a pessoa voltar no dia seguinte sem entender por quê.
        var umMesAtras = new DateTime(2026, 7, 5, 14, 0, 0);

        Assert.True(TrocaDeNome.PodeTrocarApelido(umMesAtras, new DateTime(2026, 8, 5, 14, 0, 0)).Pode);
    }

    [Fact]
    public void Faltando_um_minuto_o_apelido_ainda_nao_pode()
    {
        var umMesAtras = new DateTime(2026, 7, 5, 14, 0, 0);

        Assert.False(TrocaDeNome.PodeTrocarApelido(umMesAtras, new DateTime(2026, 8, 5, 13, 59, 0)).Pode);
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
    public void A_recusa_do_apelido_diz_ATE_QUANDO_e_nao_so_que_nao_pode()
    {
        // Negar sem data faz a pessoa tentar amanhã, e no outro dia, sem nunca entender.
        var faz10Dias = Hoje.AddDays(-10);
        var r = TrocaDeNome.PodeTrocarApelido(faz10Dias, Hoje);

        var texto = TrocaDeNome.Recusa("O apelido", r, Hoje);

        Assert.Contains(r.LiberaEm!.Value.ToString("dd/MM/yyyy"), texto);
        Assert.Contains("faltam", texto);
    }

    [Fact]
    public void A_recusa_do_nome_diz_o_PORQUE_e_oferece_saida()
    {
        // Sem data pra prometer, a recusa precisa dar duas coisas no lugar: o motivo (senão
        // parece implicância do site) e um caminho pra quem realmente precisa mudar — senão
        // vira beco sem saída pra quem casou, mudou de nome e não tem a quem recorrer.
        var r = TrocaDeNome.PodeTrocarNome(Hoje.AddDays(-1), Hoje);

        var texto = TrocaDeNome.Recusa("O nome", r, Hoje);

        Assert.Contains("rankings", texto);
        Assert.Contains("Reportar problema", texto);
        // E nunca uma data inventada: 01/01/0001 na cara do usuário é o erro clássico aqui.
        Assert.DoesNotContain("0001", texto);
    }

    [Fact]
    public void A_tranquilizada_do_resto_do_perfil_NAO_vem_colada_na_recusa()
    {
        // Quem trocou nome E apelido no mesmo save lia "o resto do perfil você salva
        // normalmente" duas vezes seguidas. O controller acrescenta essa frase uma vez só,
        // no fim — texto repetido faz a tela parecer feita às pressas.
        var doNome = TrocaDeNome.PodeTrocarNome(Hoje.AddMonths(-1), Hoje);
        var doApelido = TrocaDeNome.PodeTrocarApelido(Hoje.AddDays(-2), Hoje);

        Assert.DoesNotContain(TrocaDeNome.ORestoFoiSalvo, TrocaDeNome.Recusa("O nome", doNome, Hoje));
        Assert.DoesNotContain(TrocaDeNome.ORestoFoiSalvo, TrocaDeNome.Recusa("O apelido", doApelido, Hoje));
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
        Assert.Contains("2 meses.", TrocaDeNome.AvisoAntesDeTrocar("o apelido", 2));
    }

    [Fact]
    public void Os_avisos_do_nome_sempre_dizem_o_porque()
    {
        // A decisão do Felipe foi "uma troca só, AVISANDO que é pra manter o padrão dos
        // rankings e torneios". O aviso sem o motivo é meia entrega.
        Assert.Contains("rankings", TrocaDeNome.AvisoAntesDeTrocarNome());
        Assert.Contains("uma única vez", TrocaDeNome.AvisoAntesDeTrocarNome());
        Assert.Contains("rankings", TrocaDeNome.AvisoDepoisDeTrocarNome());
        Assert.Contains("fixo", TrocaDeNome.AvisoDepoisDeTrocarNome());
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
