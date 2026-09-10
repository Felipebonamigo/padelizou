using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.Tests;

// ONDE É O JOGO, na etiqueta das telas (Services/LugarDoJogo) — 09/09/2026.
//
// 🗣️ Felipe, num print da lista de jogos do 2º Etapa ER Padel Tour em `dev`: *"falta aparecer
// qual o local e quadra aqui na lista de jogos"*. Os 97 jogos agendados mostravam hora,
// categoria e grupo, e NADA sobre onde. Duas coisas somavam pra isso:
//
//   • o local (o clube) só entrava na etiqueta em torneio de MAIS DE UM clube — decisão de
//     21/08/2026 pra não repetir o cabeçalho da página em cada linha;
//   • jogo SEM quadra escrita não rendia etiqueta nenhuma, e some calado. É o caso do Er, e
//     lá a quadra vazia é LEGÍTIMA: *"nao tem quadra definida, apenas o clube, por que é por
//     ordem de chegada (por ter checkin)"*. Quem decide a quadra é o balcão, na hora — o que
//     a linha precisa dizer é o CLUBE.
//
// A referência que o próprio Felipe mandou é o que o Er já publicou na 1ª Etapa
// (sportscore.com.br/timeline/436): cada linha lá é `17/07 Sex 18:00 - Er Padel - Quadra:
// .Loja 7`, e o clube aparece MESMO quando a quadra vem vazia (`Radar Esportes - Quadra: .`).
// O local é a metade da resposta que não pode faltar — é ele que decide pra que prédio a
// pessoa dirige.
public class OndeEOJogoNaTelaTests
{
    private const int ErPadel = 10;
    private const int Radar = 20;

    private static Quadra QuadraDe(string nome, int? clube) => new() { Nome = nome, ClubeId = clube };

    // O torneio de sempre: um clube só, quadras cadastradas.
    private static SedesDoTorneio UmClubeSo() =>
        SedesDoTorneio.Montar(
            clubePrincipalId: ErPadel,
            minutosParaTrocarDeClube: 0,
            quadras: new[] { QuadraDe("Quadra 1", null), QuadraDe("Quadra 2", null) },
            categorias: Array.Empty<Categoria>(),
            nomesDosClubes: new Dictionary<int, string> { [ErPadel] = "Er Padel" });

    // O torneio que ainda não cadastrou quadra nenhuma — só `QuantidadeQuadras`. É o estado em
    // que a grade marca hora sem nomear lugar (ver GradeDeJogos.Encaixar).
    private static SedesDoTorneio SemQuadraCadastrada() =>
        SedesDoTorneio.Montar(
            clubePrincipalId: ErPadel,
            minutosParaTrocarDeClube: 0,
            quadras: Array.Empty<Quadra>(),
            categorias: Array.Empty<Categoria>(),
            nomesDosClubes: new Dictionary<int, string> { [ErPadel] = "Er Padel" });

    private static SedesDoTorneio DoisClubes() =>
        SedesDoTorneio.Montar(
            clubePrincipalId: ErPadel,
            minutosParaTrocarDeClube: 30,
            quadras: new[] { QuadraDe("Loja 7", null), QuadraDe("Quadra Radar", Radar) },
            categorias: Array.Empty<Categoria>(),
            nomesDosClubes: new Dictionary<int, string> { [ErPadel] = "Er Padel", [Radar] = "Radar Esportes" });

    // ── DOIS CLUBES E JOGO SEM QUADRA: A CATEGORIA RESPONDE ────────────────────────────────
    //
    // 🗣️ Felipe, 10/09/2026, na grade do Er em PRODUÇÃO recém-sorteada: *"falta aparecer em qual
    // clube é os jogos"*. Os 97 jogos, todos sem etiqueta nenhuma.
    //
    // 🕳️ Era a combinação exata que a etiqueta não sabia responder: o Er é POR ORDEM (a quadra
    // é apagada de propósito — `OrdemDeLiberacao.ApagarAsQuadras`) e tem DOIS clubes (Er Padel e
    // Radar). Sem quadra, `ClubeNaEtiqueta` não tinha de onde tirar o clube e calava — certo
    // pra não chutar, errado pra quem já sabia: a 3ª e a 4ª foram "tiradas do externo", logo
    // jogam no Er Padel sem dúvida nenhuma, e a categoria PRESA num clube (o jeito do Dez E
    // Batata) também. O que a etiqueta não pode fazer é adivinhar; o que ela pode é LER o que a
    // categoria já diz.
    private static SedesDoTorneio DoisClubesComCategorias() =>
        SedesDoTorneio.Montar(
            clubePrincipalId: ErPadel,
            minutosParaTrocarDeClube: 30,
            quadras: new[] { QuadraDe("Loja 7", null), QuadraDe("Quadra Radar", Radar) },
            categorias: new[]
            {
                new Categoria { Id = 3, Nome = "3ª Masculina", Codigo = "C3M", PodeJogarNaSedeExtra = false },
                new Categoria { Id = 6, Nome = "6ª Masculina", Codigo = "C6M", PodeJogarNaSedeExtra = true },
                new Categoria { Id = 9, Nome = "Presa no Radar", Codigo = "PR", ClubeId = Radar },
            },
            nomesDosClubes: new Dictionary<int, string> { [ErPadel] = "Er Padel", [Radar] = "Radar Esportes" });

    [Fact]
    public void Sem_quadra_com_dois_clubes_a_categoria_tirada_do_externo_e_do_clube_principal()
    {
        Assert.Equal("Er Padel", LugarDoJogo.Etiqueta(DoisClubesComCategorias(), null, categoriaId: 3));
    }

    [Fact]
    public void Sem_quadra_com_dois_clubes_a_categoria_presa_num_clube_e_daquele_clube()
    {
        Assert.Equal("Radar Esportes", LugarDoJogo.Etiqueta(DoisClubesComCategorias(), null, categoriaId: 9));
    }

    // A contrapartida que segura o chute: a categoria que PODE transbordar joga em qualquer um
    // dos dois, e sem quadra ninguém sabe qual. Aí a etiqueta continua calada — escrever "Er
    // Padel" mandaria metade do torneio pro endereço errado.
    [Fact]
    public void Sem_quadra_com_dois_clubes_a_categoria_livre_continua_sem_etiqueta()
    {
        Assert.Null(LugarDoJogo.Etiqueta(DoisClubesComCategorias(), null, categoriaId: 6));
    }

    // E a quadra, quando existe, continua mandando: ela é o dado mais específico.
    [Fact]
    public void Com_quadra_a_categoria_nao_muda_nada()
    {
        Assert.Equal("Radar Esportes · Quadra Radar",
            LugarDoJogo.Etiqueta(DoisClubesComCategorias(), "Quadra Radar", categoriaId: 3));
    }

    // ── O CARIMBO DO CLUBE NO JOGO VENCE A CATEGORIA ───────────────────────────────────────
    //
    // 🗣️ Felipe, 10/09/2026: *"nao precisa ter a quadra definida, mas o clube sempre tem q estar
    // definido"*. `Partida.ClubeId` é a decisão do motor gravada; a categoria livre (que pode
    // transbordar) deixa de ficar muda quando o jogo já sabe pra que clube foi.
    [Fact]
    public void Sem_quadra_o_clube_carimbado_no_jogo_responde_mesmo_pra_categoria_livre()
    {
        Assert.Equal("Radar Esportes",
            LugarDoJogo.Etiqueta(DoisClubesComCategorias(), null, categoriaId: 6, clubeId: Radar));
    }

    [Fact]
    public void Com_quadra_o_carimbo_nao_muda_a_etiqueta()
    {
        Assert.Equal("Er Padel · Loja 7",
            LugarDoJogo.Etiqueta(DoisClubesComCategorias(), "Loja 7", categoriaId: 6, clubeId: Radar));
    }

    // ── O LOCAL ENTRA EM TODA LINHA ───────────────────────────────────────────────────────

    // ⚠️ INVERTE A DECISÃO DE 21/08/2026, que dizia "repetir o clube em cada linha da lista
    // seria copiar o cabeçalho da página dezenas de vezes". Perguntado sobre exatamente esse
    // custo, o Felipe escolheu o outro lado: *"Sempre: Er Padel · Quadra 2"*. O que mudou desde
    // 21/08 é que o torneio de duas sedes deixou de ser hipótese — o Er aluga o Radar —, e uma
    // etiqueta que muda de forma conforme o torneio ensina o jogador a não confiar nela.
    [Fact]
    public void Com_um_clube_so_a_etiqueta_diz_o_local_e_a_quadra()
    {
        Assert.Equal("Er Padel · Quadra 2", LugarDoJogo.Etiqueta(UmClubeSo(), "Quadra 2"));
    }

    // `Partida.NomeQuadra` é texto solto e a Mesa de Controle escreve nele à mão. Com UM clube
    // não há segundo prédio pra errar: a quadra escrita à mão é dali, e dizer o local é seguro.
    [Fact]
    public void Com_um_clube_so_ate_a_quadra_escrita_a_mao_ganha_o_local()
    {
        Assert.Equal("Er Padel · Quadra do fundo", LugarDoJogo.Etiqueta(UmClubeSo(), "Quadra do fundo"));
    }

    [Fact]
    public void Com_dois_clubes_a_etiqueta_continua_dizendo_o_predio_da_quadra()
    {
        Assert.Equal("Radar Esportes · Quadra Radar", LugarDoJogo.Etiqueta(DoisClubes(), "Quadra Radar"));
        Assert.Equal("Er Padel · Loja 7", LugarDoJogo.Etiqueta(DoisClubes(), "Loja 7"));
    }

    // ⚠️ Com DOIS clubes, nome fora do cadastro NÃO recebe clube chutado — a regra de 21/08 que
    // não muda. Escrever o prédio errado é pior que não escrever nenhum.
    [Fact]
    public void Com_dois_clubes_a_quadra_fora_do_cadastro_sai_sem_predio()
    {
        Assert.Equal("Quadra do vizinho", LugarDoJogo.Etiqueta(DoisClubes(), "Quadra do vizinho"));
    }

    // ── O JOGO SEM QUADRA PAROU DE SUMIR ──────────────────────────────────────────────────

    // O defeito do print: 97 jogos com hora e sem uma palavra sobre onde.
    //
    // ⚠️ E quadra vazia NÃO é defeito — foi o próprio Felipe quem corrigiu a primeira leitura
    // deste teste: *"nesse caso aqui, nao tem quadra definida, apenas o clube, por que é por
    // ordem de chegada (por ter checkin)"*. É o que a 1ª Etapa dele já publicou: `Radar Esportes
    // - Quadra: .`. Quem decide a quadra é o balcão do check-in, na hora. Por isso a etiqueta
    // devolve o LOCAL sozinho, e não um "quadra a definir" que leria como pendência.
    [Fact]
    public void Jogo_sem_quadra_mostra_o_local_sozinho()
    {
        Assert.Equal("Er Padel", LugarDoJogo.Etiqueta(UmClubeSo(), null));
        Assert.Equal("Er Padel", LugarDoJogo.Etiqueta(UmClubeSo(), "   "));
    }

    // Nem quadra cadastrada o torneio precisa ter: o local é do TORNEIO, e responde sozinho.
    [Fact]
    public void Torneio_sem_quadra_cadastrada_nenhuma_tambem_mostra_o_local()
    {
        Assert.Equal("Er Padel", LugarDoJogo.Etiqueta(SemQuadraCadastrada(), null));
    }

    // ⚠️ Com DOIS clubes e nenhuma quadra no jogo, não dá pra saber o prédio — e é exatamente
    // aqui que chutar mandaria a pessoa pro outro lado da cidade. A etiqueta cala.
    //
    // ⚠️ É o estado do 2º Etapa em `dev` HOJE, e a razão de a lista continuar muda mesmo depois
    // desta mudança: sem quadra escrita no jogo, nada no banco diz em qual dos dois clubes ele
    // é. Quem tem que passar a responder isso é a GRADE — ver o bloco de 09/09 no STATUS.md.
    [Fact]
    public void Com_dois_clubes_o_jogo_sem_quadra_nao_chuta_o_predio()
    {
        Assert.Null(LugarDoJogo.Etiqueta(DoisClubes(), null));
    }

    // ── O TEXTO CORRIDO NÃO ANDOU JUNTO, DE PROPÓSITO ─────────────────────────────────────

    // `EmTextoCorrido` entra DENTRO de frase — "A {onde} vagou — seu jogo é o próximo"
    // (Services/AvisosDoDiaDeJogo, que sai por push, e-mail e WhatsApp de uma vez) e no LOCATION
    // do .ics. Lá a pergunta é OUTRA: "que quadra vagou?", e um aviso que dissesse "A Er Padel
    // vagou" mandaria a pessoa se levantar sem dizer pra onde. O .ics também já recebe o local
    // do torneio por fora (AgendaController.LocalDaPartida). Por isso a mudança é só da ETIQUETA,
    // e este teste é o que impede a "uniformização" das duas.
    [Fact]
    public void O_aviso_por_push_nao_vira_o_nome_do_clube()
    {
        Assert.Null(LugarDoJogo.EmTextoCorrido(UmClubeSo(), null));
        Assert.Equal("A quadra vagou — seu jogo é o próximo. Fique por perto.",
            AvisosDoDiaDeJogo.CorpoDoProximo(new Partida { NomeQuadra = null }, UmClubeSo()));
        // Com quadra, o texto corrido segue nomeando a QUADRA — o clube só entra com duas sedes.
        Assert.Equal("Quadra 2", LugarDoJogo.EmTextoCorrido(UmClubeSo(), "Quadra 2"));
    }
}
