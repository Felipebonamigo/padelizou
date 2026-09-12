using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Padelizou.Models;
using padelizou.Models;
using Padelizou.Services;
using Xunit;

namespace Padelizou.Tests;

// O CHAVEAMENTO DESENHADO À MÃO — quem cruza com quem na primeira eliminatória.
//
// 🗣️ Felipe, 10/09/2026: *"permita alterar na mao o chaveamento, como funciona a chave de cada um,
// se o primeiro passar quem enfrenta, etc (obviamente que apenas organizadores e adm do sistema
// podem fazer isso)"* — e, no mesmo fôlego: *"cuidado para nao mexer nada no que ja tem do ER hoje,
// isso é para os próximos torneios"*.
//
// ⚠️ É ESSA SEGUNDA FRASE QUE DESENHA O ARQUIVO. O cruzamento vive num campo NOVO e ANULÁVEL
// (`Categoria.CruzamentoDoMataMata`), e **null significa "o motor decide", que é o comportamento de
// hoje, letra por letra**. Categoria que já existe no banco nasce e continua null: o Er não muda
// porque não há caminho de código novo passando por ele. Toda a régua nova mora atrás de um `if`
// que só abre quando alguém desenhou.
//
// O texto guardado é legível de propósito ("1A×2C|1B×2D;bye:1E"): quem for ler o banco às 3h da
// manhã de um torneio precisa entender sem consultar código.
public class CruzamentoDoMataMataTests
{
    // ── O TEXTO ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Le_o_desenho_escrito_pelo_organizador()
    {
        var mapa = CruzamentoDoMataMata.Ler("1A×2C|1B×2D;bye:1E");

        Assert.NotNull(mapa);
        Assert.Equal(2, mapa!.Confrontos.Count);
        Assert.Equal(new CruzamentoDoMataMata.Vaga(1, "A"), mapa.Confrontos[0].Lado1);
        Assert.Equal(new CruzamentoDoMataMata.Vaga(2, "C"), mapa.Confrontos[0].Lado2);
        Assert.Equal(new CruzamentoDoMataMata.Vaga(1, "E"), Assert.Single(mapa.Byes));
    }

    [Fact]
    public void Escreve_o_desenho_de_volta_no_mesmo_formato()
    {
        var texto = "1A×2C|1B×2D;bye:1E";
        Assert.Equal(texto, CruzamentoDoMataMata.Ler(texto)!.Escrever());
    }

    // ⚠️ Vazio e lixo caem no MESMO lugar: null, que quer dizer "o motor decide". Um desenho
    // corrompido no banco não pode deixar a categoria sem mata-mata nenhum — ela volta a ser o
    // que era antes de alguém mexer.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("isso não é um cruzamento")]
    [InlineData("1A×")]
    [InlineData("XA×2C")]
    public void Texto_vazio_ou_corrompido_devolve_null(string? texto)
    {
        Assert.Null(CruzamentoDoMataMata.Ler(texto));
    }

    [Fact]
    public void Sem_bye_o_ponto_e_virgula_nao_aparece()
    {
        Assert.Equal("1A×2B", CruzamentoDoMataMata.Ler("1A×2B")!.Escrever());
    }

    // ── O DESENHO APLICADO ───────────────────────────────────────────────────────────────────

    private static List<ChaveamentoMataMata.Classificado> Classificados(params (int Posicao, string Grupo)[] vagas) =>
        vagas.Select((v, i) => new ChaveamentoMataMata.Classificado(
            DuplaId: (i + 1) * 10, Grupo: $"Grupo {v.Grupo}", Vitorias: 0, Saldo: 0, Posicao: v.Posicao))
            .ToList();

    [Fact]
    public void O_desenho_manda_no_lugar_da_semeadura_automatica()
    {
        // 8 duplas, 4 grupos: o motor cruzaria 1º×2º pela campanha. O organizador quer outra coisa.
        var classificados = Classificados(
            (1, "A"), (1, "B"), (1, "C"), (1, "D"),
            (2, "A"), (2, "B"), (2, "C"), (2, "D"));

        var mapa = CruzamentoDoMataMata.Ler("1A×2B|1B×2A|1C×2D|1D×2C")!;
        var (fase, confrontos, byes) = CruzamentoDoMataMata.Aplicar(mapa, classificados);

        Assert.Equal("Quartas de Final", fase);
        Assert.Empty(byes);
        Assert.Equal(4, confrontos.Count);

        int IdDe(int posicao, string grupo) =>
            classificados.First(c => c.Posicao == posicao && c.Grupo == $"Grupo {grupo}").DuplaId;

        Assert.Equal(IdDe(1, "A"), confrontos[0].Dupla1Id);
        Assert.Equal(IdDe(2, "B"), confrontos[0].Dupla2Id);
        Assert.Equal(IdDe(1, "D"), confrontos[3].Dupla1Id);
        Assert.Equal(IdDe(2, "C"), confrontos[3].Dupla2Id);
    }

    [Fact]
    public void O_bye_desenhado_pula_a_primeira_rodada()
    {
        var classificados = Classificados((1, "A"), (1, "B"), (1, "C"), (2, "A"), (2, "B"), (2, "C"));

        var mapa = CruzamentoDoMataMata.Ler("1A×2C|1B×2A|1C×2B")!;
        var (_, confrontos, byes) = CruzamentoDoMataMata.Aplicar(mapa, classificados);

        Assert.Equal(3, confrontos.Count);
        Assert.Empty(byes);

        // Agora com bye explícito: 5 vagas, 2 jogos e 1 bye.
        var cinco = Classificados((1, "A"), (1, "B"), (1, "C"), (2, "A"), (2, "B"));
        var comBye = CruzamentoDoMataMata.Ler("1A×2B|1B×2A;bye:1C")!;
        var (fase, jogos, folgados) = CruzamentoDoMataMata.Aplicar(comBye, cinco);

        Assert.Equal(2, jogos.Count);
        Assert.Equal(cinco.First(c => c.Posicao == 1 && c.Grupo == "Grupo C").DuplaId, Assert.Single(folgados));
        Assert.Equal("Quartas de Final", fase);
    }

    // ⚠️ VAGA QUE NÃO EXISTE NÃO PODE VIRAR JOGO FANTASMA. O organizador desenha ANTES de a fase de
    // grupos acabar; se ele apagar um grupo depois, o desenho fica falando de gente que não
    // classificou. Aí o desenho inteiro é descartado e o motor decide — o mesmo que null.
    [Fact]
    public void Desenho_que_cita_vaga_inexistente_e_descartado()
    {
        var classificados = Classificados((1, "A"), (1, "B"), (2, "A"), (2, "B"));
        var mapa = CruzamentoDoMataMata.Ler("1A×2Z|1B×2A")!;

        // `Conferir` devolve o MOTIVO pra não usar o desenho — null seria "pode usar".
        Assert.Contains("não existe", CruzamentoDoMataMata.Conferir(mapa, classificados) ?? "");
    }

    [Fact]
    public void Desenho_que_deixa_alguem_de_fora_e_descartado()
    {
        // O 2º do B classificou e não aparece em jogo nem em bye: sumiria do torneio.
        var classificados = Classificados((1, "A"), (1, "B"), (2, "A"), (2, "B"));
        var mapa = CruzamentoDoMataMata.Ler("1A×2A|1B×")!;

        Assert.Null(mapa);
    }

    [Fact]
    public void Desenho_que_repete_a_mesma_vaga_e_descartado()
    {
        var classificados = Classificados((1, "A"), (1, "B"), (2, "A"), (2, "B"));
        var mapa = CruzamentoDoMataMata.Ler("1A×2A|1A×2B")!;

        Assert.Contains("mesma vaga", CruzamentoDoMataMata.Conferir(mapa, classificados) ?? "");
    }

    // ── O AVISO DAS METADES (decisão do Felipe: avisa e deixa passar) ────────────────────────

    // ⚠️ A GEOMETRIA DA CHAVE É CONTRAINTUITIVA, e as duas primeiras versões destes testes
    // erraram por causa disso — o código estava certo nas duas. A rodada seguinte é montada
    // cruzando PRIMEIRO × ÚLTIMO (AvancoDaChave.ParearVencedores), então com 4 jogos quem se
    // encontra na semifinal é o jogo 1 com o jogo 4, e o 2 com o 3:
    //
    //     metade de cima  = jogos 1 e 4        metade de baixo = jogos 2 e 3
    //
    // "Jogos vizinhos na lista" é justamente o que NÃO se encontra antes da final.
    [Fact]
    public void Avisa_quando_dois_do_mesmo_grupo_caem_na_mesma_metade()
    {
        var classificados = Classificados(
            (1, "A"), (1, "B"), (1, "C"), (1, "D"),
            (2, "A"), (2, "B"), (2, "C"), (2, "D"));

        // 1A no jogo 1 e 2A no jogo 4 — a MESMA metade: eles se reencontram na semifinal.
        var mapa = CruzamentoDoMataMata.Ler("1A×2B|1C×2D|1D×2C|1B×2A")!;
        var avisos = CruzamentoDoMataMata.Avisos(mapa, classificados);

        Assert.Contains(avisos, a => a.Contains("Grupo A"));
    }

    [Fact]
    public void Metades_opostas_nao_geram_aviso()
    {
        var classificados = Classificados(
            (1, "A"), (1, "B"), (1, "C"), (1, "D"),
            (2, "A"), (2, "B"), (2, "C"), (2, "D"));

        // 1A no jogo 1 (metade de cima) e 2A no jogo 2 (metade de baixo): só na final.
        var mapa = CruzamentoDoMataMata.Ler("1A×2B|1B×2A|1C×2D|1D×2C")!;

        Assert.DoesNotContain(CruzamentoDoMataMata.Avisos(mapa, classificados), a => a.Contains("Grupo A"));
    }

    // ── O PADRÃO: o que o organizador vê ao abrir a tela ─────────────────────────────────────
    //
    // ⚠️ É O DESENHO QUE O MOTOR FARIA, e isso não é enfeite: abrir a tela e salvar sem mexer em
    // nada tem que produzir a MESMA chave de antes. Se o padrão fosse outra coisa, a primeira
    // visita ao formulário já mudaria o torneio.
    [Fact]
    public void O_desenho_padrao_e_exatamente_o_que_o_motor_faria()
    {
        var grupos = new[] { "Grupo A", "Grupo B", "Grupo C", "Grupo D" };
        var doMotor = ChaveProjetada.Montar(grupos, classificadosPorGrupo: 2);

        var padrao = CruzamentoDoMataMata.Padrao(grupos, classificadosPorGrupo: 2);

        Assert.NotNull(padrao);
        Assert.Equal(doMotor.Confrontos.Count, padrao!.Confrontos.Count);
        for (int i = 0; i < doMotor.Confrontos.Count; i++)
        {
            Assert.Equal(doMotor.Confrontos[i].Lado1.Rotulo, padrao.Confrontos[i].Lado1.Rotulo);
            Assert.Equal(doMotor.Confrontos[i].Lado2.Rotulo, padrao.Confrontos[i].Lado2.Rotulo);
        }
        Assert.Equal(doMotor.Byes.Count, padrao.Byes.Count);
    }

    // ── A PROMESSA AO FELIPE: O ER NÃO MUDA ──────────────────────────────────────────────────
    //
    // 🗣️ *"cuidado para nao mexer nada no que ja tem do ER hoje, isso é para os próximos torneios"*.
    // Toda categoria que já existe no banco tem `CruzamentoDoMataMata` null (a migration só ADICIONA
    // a coluna), e este teste trava o que isso significa: null produz a chave IDÊNTICA à de antes.
    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(12)]
    public void Sem_desenho_a_chave_sai_exatamente_como_o_motor_sempre_fez(int quantasVagas)
    {
        var vagas = Enumerable.Range(0, quantasVagas)
            .Select(i => (Posicao: i % 2 + 1, Grupo: ((char)('A' + i / 2)).ToString()))
            .ToArray();
        var classificados = Classificados(vagas);

        var comParametroNovo = ChaveamentoMataMata.MontarPrimeiraFase(classificados, 2, cruzamentoDesenhado: null);
        var comoSempre = ChaveamentoMataMata.MontarPrimeiraFase(classificados, 2);

        Assert.Equal(comoSempre.Fase, comParametroNovo.Fase);
        Assert.Equal(comoSempre.Byes, comParametroNovo.Byes);
        Assert.Equal(comoSempre.Confrontos.Count, comParametroNovo.Confrontos.Count);
        for (int i = 0; i < comoSempre.Confrontos.Count; i++)
        {
            Assert.Equal(comoSempre.Confrontos[i].Dupla1Id, comParametroNovo.Confrontos[i].Dupla1Id);
            Assert.Equal(comoSempre.Confrontos[i].Dupla2Id, comParametroNovo.Confrontos[i].Dupla2Id);
        }
    }

    // E desenho CORROMPIDO no banco também cai no motor — nunca numa categoria sem mata-mata.
    [Fact]
    public void Desenho_corrompido_no_banco_nao_deixa_a_categoria_sem_chave()
    {
        var classificados = Classificados((1, "A"), (1, "B"), (2, "A"), (2, "B"));

        var comLixo = ChaveamentoMataMata.MontarPrimeiraFase(classificados, 2, "isso não é um cruzamento");
        var comoSempre = ChaveamentoMataMata.MontarPrimeiraFase(classificados, 2);

        Assert.Equal(comoSempre.Confrontos.Count, comLixo.Confrontos.Count);
        Assert.NotEmpty(comLixo.Confrontos);
    }

    // ── A FIAÇÃO: quem pode, quando pode, e o que chega no banco ─────────────────────────────

    [Fact]
    public async Task Quem_nao_organiza_nao_desenha_a_chave()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        var intruso = new Jogador { Nome = "Intruso", Cpf = "99900000066" };
        ctx.Jogadores.Add(intruso);
        await ctx.SaveChangesAsync();

        var resultado = await TestInfra.NovoTorneiosController(ctx, intruso.Id)
            .SalvarCruzamento(torneio.Id, categoria.Id, "1A×2B");

        Assert.IsType<ForbidResult>(resultado);
        await ctx.Entry(categoria).ReloadAsync();
        Assert.Null(categoria.CruzamentoDoMataMata);
    }

    // ⚠️ Depois de aprovada a chave é pública: tem gente que já viu o caminho e se organizou.
    // Mesma régua do "trocar duplas de grupo" e do "desfazer sorteio".
    [Fact]
    public async Task Chave_ja_aprovada_nao_aceita_desenho_novo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, org, controller) = await SortearAsync(ctx);
        torneio.Status = "Em Andamento";
        await ctx.SaveChangesAsync();

        await controller.SalvarCruzamento(torneio.Id, categoria.Id, "1A×2B");

        await ctx.Entry(categoria).ReloadAsync();
        Assert.Null(categoria.CruzamentoDoMataMata);
    }

    // ⚠️ O DESENHO PARTE DO PADRÃO E INVERTE UM CONFRONTO — e não de um texto inventado. Foi o
    // que a primeira versão deste teste fez, e ela falhou por um motivo REAL: com 8 duplas o
    // sorteio faz A(2), B(3), C(3), e um desenho citando só dois grupos deixa vagas de fora, que é
    // exatamente o que o `Conferir` recusa. Partir do padrão é o caminho do organizador de verdade
    // (a tela abre com ele preenchido) e não depende do tamanho dos grupos.
    [Fact]
    public async Task O_organizador_desenha_e_o_desenho_fica_guardado()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx);
        var grupos = await ctx.Set<GrupoTorneio>().Where(g => g.CategoriaId == categoria.Id)
            .OrderBy(g => g.Nome).Select(g => g.Nome).ToListAsync();

        var padrao = CruzamentoDoMataMata.Padrao(grupos, categoria.ClassificadosPorGrupo ?? 2);
        Assert.NotNull(padrao);

        // Inverte os lados do primeiro confronto: mesmo conjunto de vagas, cruzamento diferente.
        var invertido = new CruzamentoDoMataMata.Mapa(
            padrao!.Confrontos
                .Select((c, i) => i == 0 ? new CruzamentoDoMataMata.Confronto(c.Lado2, c.Lado1) : c)
                .ToList(),
            padrao.Byes);

        await controller.SalvarCruzamento(torneio.Id, categoria.Id, invertido.Escrever());

        await ctx.Entry(categoria).ReloadAsync();
        Assert.Equal(invertido.Escrever(), categoria.CruzamentoDoMataMata);
        Assert.NotEqual(padrao.Escrever(), categoria.CruzamentoDoMataMata);
    }

    // Desenho que deixa alguém de fora é recusado COM MOTIVO, e o que estava guardado não muda —
    // meio desenho salvo é pior que nenhum.
    [Fact]
    public async Task Desenho_invalido_e_recusado_com_motivo()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx);

        await controller.SalvarCruzamento(torneio.Id, categoria.Id, "1A×2A");

        await ctx.Entry(categoria).ReloadAsync();
        Assert.Null(categoria.CruzamentoDoMataMata);
        Assert.Contains("de fora", string.Join(" ", controller.TempData.Values.Select(v => v?.ToString())));
    }

    // Limpar devolve a categoria pro motor — o mesmo estado de quem nunca desenhou.
    [Fact]
    public async Task Limpar_devolve_a_chave_pro_motor()
    {
        using var ctx = TestInfra.NovoContexto();
        var (torneio, categoria, _, controller) = await SortearAsync(ctx);
        categoria.CruzamentoDoMataMata = "1A×2B";
        await ctx.SaveChangesAsync();

        await controller.SalvarCruzamento(torneio.Id, categoria.Id, null);

        await ctx.Entry(categoria).ReloadAsync();
        Assert.Null(categoria.CruzamentoDoMataMata);
    }

    private static async Task<(Torneio, Categoria, Jogador, Padelizou.Controllers.TorneiosController)>
        SortearAsync(DbPadelContext ctx)
    {
        var (torneio, categoria, org) = TestInfra.MontarTorneio(ctx, qtdDuplas: 8);
        var controller = TestInfra.NovoTorneiosController(ctx, org.Id);
        await controller.GerarChaves(torneio.Id);
        await ctx.Entry(torneio).ReloadAsync();
        return (torneio, categoria, org, controller);
    }

    // ── A TELA ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tela_oferece_o_desenho_so_enquanto_a_chave_espera_aprovacao()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));
        int form = fonte.IndexOf("asp-action=\"SalvarCruzamento\"", StringComparison.Ordinal);

        Assert.True(form > 0, "O formulário do chaveamento à mão sumiu da aba de grupos.");

        // ⚠️ Esconder o botão NÃO é autorização (a ação recusa de novo do lado de lá) — mas
        // oferecer o desenho numa chave já pública seria prometer o que o servidor vai negar.
        var guardaAcima = fonte[Math.Max(0, form - 2500)..form];
        Assert.Contains("AprovacaoDeChaves.Pendente", guardaAcima);
        Assert.Contains("PodeAprovarChaves", guardaAcima);
    }

    // O campo abre com o desenho do MOTOR, não vazio: abrir e salvar sem mexer não muda a chave.
    [Fact]
    public void A_tela_abre_com_o_desenho_que_o_motor_faria()
    {
        var fonte = File.ReadAllText(Path.Combine(PastaDoProjeto(), "Views", "Torneios", "Details.cshtml"));

        Assert.Contains("CruzamentoDoMataMata.Padrao", fonte);
    }

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Não achei a pasta do projeto Padelizou.");
    }
}
