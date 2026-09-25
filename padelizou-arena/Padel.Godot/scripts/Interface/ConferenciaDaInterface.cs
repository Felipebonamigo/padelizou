using Godot;
using Padel.Core;
using Padel.Core.Torneio;

namespace Padel.Godot.Interface;

/// <summary>
/// Os testes da interface: linha de comando, arquivo de configuração, dados do placar e a entrada do controle — e as
/// cenas de verdade (partida, carreira, menu, Opções) onde elas encontram a carreira e o perfil. Nenhum caso toca no
/// save de quem joga: perfil, carreira e configuração apontam pra uma pasta temporária.
/// Moram no projeto Godot, e não no Padel.Core.Tests, porque o xUnit do Core não enxerga este assembly — e a parte
/// da entrada precisa do InputMap e de um viewport de verdade. Rodam dentro do Godot, sem tela:
/// <code>godot --headless --path Padel.Godot res://cenas/TestePlacar.tscn -- --conferir</code>
/// Cada caso imprime "ok" ou "FALHOU" com o motivo; o processo sai com 0 se tudo passou e 1 se algo falhou.
/// atalho: um executor mínimo em vez de xUnit, porque a lista de arquivos desta tarefa não previa projeto de teste;
/// a saída é um Padel.Godot.Tests que compile <c>Configuracao.cs</c> e <c>DadosDoPlacar.cs</c> por link (a parte
/// sem Godot) e rodar esta conferência no CI pela parte que precisa do Godot.
/// </summary>
public static class ConferenciaDaInterface
{
    private sealed class Falha(string mensagem) : Exception(mensagem);

    private sealed record Caso(string Nome, Func<Node, Task> Rodar);

    private static string _pasta = "";

    /// <summary>Roda tudo, imprime o resultado e fecha o jogo com 0 (tudo passou) ou 1 (algo falhou).</summary>
    public static async void RodarESair(Node no)
    {
        int falhas = 0, total = 0;
        _pasta = Path.Combine(Path.GetTempPath(), $"padelizou-conferencia-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(_pasta);
            // Nenhum caso toca no save de quem joga: o perfil, a carreira e as opções apontam pra pasta temporária.
            PerfilLocal.CaminhoPedido = Arquivo("perfil-do-jogo.json");
            Configuracao.CaminhoPedido = Arquivo("configuracao-do-jogo.json");
            // Um quadro antes: o nó precisa estar na árvore, com viewport, pros casos que empurram entrada.
            await no.ToSignal(no.GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (var caso in Casos())
            {
                total++;
                Configuracao.RestaurarPadroes();
                PerfilLocal.CaminhoPedido = Arquivo("perfil-do-jogo.json");
                EstadoDaCarreira.UsarArquivo(Arquivo("carreira-do-jogo.json"));
                EstadoDaCarreira.Automatico = false;
                try
                {
                    await caso.Rodar(no);
                    GD.Print($"  ok      {caso.Nome}");
                }
                catch (Exception e)   // qualquer exceção é o caso falhando, não o executor
                {
                    falhas++;
                    GD.PrintErr($"  FALHOU  {caso.Nome}: {e.Message}");
                }
            }
        }
        finally
        {
            Configuracao.RestaurarPadroes();
            no.GetTree().Paused = false;
            try { Directory.Delete(_pasta, recursive: true); }
            catch (IOException) { }   // sobra de pasta temporária não reprova a conferência
            GD.Print($"Conferência da interface: {total} casos, {falhas} falha(s).");
            no.GetTree().Quit(falhas == 0 && total > 0 ? 0 : 1);
        }
    }

    private static IEnumerable<Caso> Casos() =>
    [
        // ---- Linha de comando ----
        Sincrono("o que o CI roda (--auto --semente 42 --sair-apos 15) pula o menu", () =>
        {
            Nenhum(Ler("--auto", "--semente", "42", "--sair-apos", "15"));
            Igual(ModoDeJogo.Demonstracao, Configuracao.Modo, "Modo");
            Igual<uint?>(42, Configuracao.Semente, "Semente");
            Igual<double?>(15, Configuracao.SairApos, "SairApos");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("sem argumento o menu espera", () =>
        {
            Nenhum(Ler());
            Igual(ModoDeJogo.Local, Configuracao.Modo, "Modo");
            Exigir(!Configuracao.PularMenu, "PularMenu deveria estar desligado");
        }),
        Sincrono("argumentos das cenas (--tela, --estado, --conferir) passam em silêncio", () =>
        {
            Nenhum(Ler("--tela", "opcoes", "--estado", "3", "--conferir"));
            Exigir(!Configuracao.PularMenu, "PularMenu deveria estar desligado");
        }),
        Sincrono("--host PORTA cria a sala e pula o menu", () =>
        {
            Nenhum(Ler("--host", "9000"));
            Igual(ModoDeJogo.CriarSala, Configuracao.Modo, "Modo");
            Igual(9000, Configuracao.PortaParaCriar, "PortaParaCriar");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("--host sozinho usa a porta salva", () =>
        {
            Configuracao.PortaParaCriar = 8123;
            Nenhum(Ler("--host"));
            Igual(ModoDeJogo.CriarSala, Configuracao.Modo, "Modo");
            Igual(8123, Configuracao.PortaParaCriar, "PortaParaCriar");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("--host com porta inválida avisa, usa a salva e pula o menu", () =>
        {
            Um(Ler("--host", "99999"));
            Igual(ModoDeJogo.CriarSala, Configuracao.Modo, "Modo");
            Igual(Configuracao.PortaPadrao, Configuracao.PortaParaCriar, "PortaParaCriar");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("--conectar IP:PORTA entra na sala e pula o menu", () =>
        {
            Nenhum(Ler("--conectar", "192.168.0.12:9000"));
            Igual(ModoDeJogo.EntrarNaSala, Configuracao.Modo, "Modo");
            Igual("192.168.0.12", Configuracao.EnderecoDaSala, "EnderecoDaSala");
            Igual(9000, Configuracao.PortaDaSala, "PortaDaSala");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("--conectar aceita IPv6 entre colchetes", () =>
        {
            Nenhum(Ler("--conectar", "[::1]:7000"));
            Igual("::1", Configuracao.EnderecoDaSala, "EnderecoDaSala");
            Igual(7000, Configuracao.PortaDaSala, "PortaDaSala");
        }),
        Sincrono("--conectar com endereço inválido avisa, usa a sala salva e pula o menu", () =>
        {
            Configuracao.EnderecoDaSala = "10.0.0.5";
            Configuracao.PortaDaSala = 8000;
            Um(Ler("--conectar", "127.0.0.1:99999"));
            Igual(ModoDeJogo.EntrarNaSala, Configuracao.Modo, "Modo");
            Igual("10.0.0.5", Configuracao.EnderecoDaSala, "EnderecoDaSala");
            Igual(8000, Configuracao.PortaDaSala, "PortaDaSala");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado — sem tela, o jogo ficaria parado no menu");
        }),
        Sincrono("--conectar sem valor avisa e pula o menu", () =>
        {
            Um(Ler("--conectar"));
            Igual(ModoDeJogo.EntrarNaSala, Configuracao.Modo, "Modo");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("--conectar seguido de outro argumento não o engole", () =>
        {
            Um(Ler("--conectar", "--auto-golpe"));
            Igual(ModoDeGolpe.Automatico, Configuracao.ModoDeGolpe, "ModoDeGolpe");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("--sair-apos inválido avisa e ainda pula o menu", () =>
        {
            Um(Ler("--sair-apos", "abc"));
            Igual<double?>(null, Configuracao.SairApos, "SairApos");
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado — sem tela, o jogo ficaria parado no menu");
        }),
        Sincrono("--sair-apos sem valor avisa e pula o menu", () =>
        {
            Um(Ler("--sair-apos"));
            Exigir(Configuracao.PularMenu, "PularMenu deveria estar ligado");
        }),
        Sincrono("--sair-apos lê o número como o PartidaNode lê (\"1,5\" é 15)", () =>
        {
            // A regra do PartidaNode: double.TryParse(texto, CultureInfo.InvariantCulture) — Float + separador de milhar.
            // Menu e partida precisam concordar, senão o menu avisa uma coisa e a partida faz outra. Negativo lá
            // desliga a saída (aqui: null); infinito lá é "sair quando a partida acabar" (aqui: infinito também).
            var casos = new (string Texto, double? Segundos)[]
            {
                ("15", 15), ("2.5", 2.5), (" 7 ", 7), ("1e1", 10), ("1,5", 15), ("1,000", 1000),
                ("-3", null), ("abc", null), ("NaN", null), ("Infinity", double.PositiveInfinity),
            };
            foreach (var (texto, segundos) in casos)
            {
                Configuracao.RestaurarPadroes();
                Ler("--sair-apos", texto);
                Igual(segundos, Configuracao.SairApos, $"SairApos de '{texto}'");
            }
        }),
        Sincrono("--sair-apos com vírgula avisa como foi lido", () =>
        {
            var avisos = Ler("--sair-apos", "1,5");
            Exigir(avisos.Count == 1 && avisos[0].Contains("15", StringComparison.Ordinal), $"esperava um aviso dizendo 15 s, veio [{string.Join(" | ", avisos)}]");
        }),
        Sincrono("--semente lê o número como o PartidaNode lê", () =>
        {
            // A regra do PartidaNode: uint.TryParse(texto) — inteiro com sinal e espaço nas pontas.
            var casos = new (string Texto, uint? Semente)[] { ("42", 42), ("+5", 5), (" 9 ", 9), ("-1", null), ("4.2", null), ("x", null) };
            foreach (var (texto, semente) in casos)
            {
                Configuracao.RestaurarPadroes();
                Ler("--semente", texto);
                Igual(semente, Configuracao.Semente, $"Semente de '{texto}'");
                Exigir(!Configuracao.PularMenu, "--semente sozinha não pula o menu");
            }
        }),
        Sincrono("--coop escolhe o coop local e espera no menu", () =>
        {
            Nenhum(Ler("--coop"));
            Igual(ModoDeJogo.CoopLocal, Configuracao.Modo, "Modo");
            Exigir(!Configuracao.PularMenu, "PularMenu deveria estar desligado");
        }),
        Sincrono("quando dois pedem modos diferentes vale o último", () =>
        {
            Nenhum(Ler("--host", "--conectar", "1.2.3.4:5"));
            Igual(ModoDeJogo.EntrarNaSala, Configuracao.Modo, "Modo");
            Igual("1.2.3.4", Configuracao.EnderecoDaSala, "EnderecoDaSala");
            Igual(5, Configuracao.PortaDaSala, "PortaDaSala");
        }),
        Sincrono("--nome tira os espaços das pontas e corta no tamanho máximo", () =>
        {
            Nenhum(Ler("--nome", "  Ana  "));
            Igual("Ana", Configuracao.NomeDoJogador, "NomeDoJogador");
            Nenhum(Ler("--nome", new string('x', 30)));
            Igual(Configuracao.TamanhoMaximoDoNome, Configuracao.NomeDoJogador.Length, "tamanho do nome");
            Um(Ler("--nome"));
        }),
        Sincrono("--facil, --dificil e --auto-golpe", () =>
        {
            Nenhum(Ler("--dificil", "--auto-golpe"));
            Igual(Dificuldade.Dificil, Configuracao.Dificuldade, "Dificuldade");
            Igual(ModoDeGolpe.Automatico, Configuracao.ModoDeGolpe, "ModoDeGolpe");
            Nenhum(Ler("--facil"));
            Igual(Dificuldade.Facil, Configuracao.Dificuldade, "Dificuldade");
            Exigir(!Configuracao.PularMenu, "PularMenu deveria estar desligado");
        }),
        Sincrono("--screenshot sem arquivo avisa", () =>
        {
            Um(Ler("--screenshot"));
            Igual<string?>(null, Configuracao.Screenshot, "Screenshot");
            Nenhum(Ler("--screenshot", "menu.png"));
            Igual<string?>("menu.png", Configuracao.Screenshot, "Screenshot");
        }),

        // ---- Endereço e porta ----
        Sincrono("TentarLerPorta: 1 a 65535, só dígitos", () =>
        {
            foreach (var (texto, valida) in new (string?, bool)[] { ("1", true), ("65535", true), (" 80 ", true), ("0", false), ("65536", false), ("-1", false), ("+5", false), ("8o", false), ("", false), (null, false) })
                Igual(valida, Configuracao.TentarLerPorta(texto, out _), $"porta '{texto}'");
        }),
        Sincrono("TentarLerEndereco aceita IPv4, nome, IPv6 e porta opcional", () =>
        {
            var casos = new (string Texto, string Host, int Porta)[]
            {
                ("192.168.0.10:7777", "192.168.0.10", 7777), ("meu-pc.local:8000", "meu-pc.local", 8000),
                ("[::1]:7000", "::1", 7000), ("::1", "::1", Configuracao.PortaPadrao), ("10.0.0.1", "10.0.0.1", Configuracao.PortaPadrao),
                ("  10.0.0.2:81 ", "10.0.0.2", 81),
            };
            foreach (var (texto, host, porta) in casos)
            {
                Exigir(Configuracao.TentarLerEndereco(texto, out var h, out var p), $"'{texto}' deveria ser aceito");
                Igual(host, h, $"host de '{texto}'");
                Igual(porta, p, $"porta de '{texto}'");
            }
        }),
        Sincrono("TentarLerEndereco recusa o que não é endereço", () =>
        {
            foreach (var texto in new[] { "", "   ", "1.2.3.4:0", "1.2.3.4:99999", ":7777", "a b:1", "usuario@host:1", "host/caminho", "host:1?x=2", "http://host" })
                Exigir(!Configuracao.TentarLerEndereco(texto, out _, out _), $"'{texto}' deveria ser recusado");
        }),

        // ---- Arquivo ----
        Sincrono("arquivo ausente volta ao padrão", () =>
        {
            Configuracao.Dificuldade = Dificuldade.Dificil;
            Igual(ResultadoDaCarga.ArquivoAusente, Configuracao.Carregar(Arquivo("nao-existe.json")), "resultado");
            Igual(Dificuldade.Medio, Configuracao.Dificuldade, "Dificuldade");
        }),
        Sincrono("salvar e carregar devolve as escolhas", () =>
        {
            string caminho = Arquivo("ida-e-volta.json");
            Configuracao.Dificuldade = Dificuldade.Dificil;
            Configuracao.PontoDeOuro = false;
            Configuracao.SetsParaVencer = 2;
            Configuracao.ModoDeGolpe = ModoDeGolpe.Automatico;
            Configuracao.Destro = false;
            Configuracao.NomeDoJogador = "Marina";
            Configuracao.EnderecoDaSala = "192.168.0.40";
            Configuracao.PortaDaSala = 9100;
            Configuracao.PortaParaCriar = 9200;
            Configuracao.Volume = 0.25f;
            Exigir(Configuracao.Salvar(caminho, out var erro), $"Salvar falhou: {erro}");
            Configuracao.RestaurarPadroes();
            Igual(ResultadoDaCarga.Carregado, Configuracao.Carregar(caminho), "resultado");
            Igual(Dificuldade.Dificil, Configuracao.Dificuldade, "Dificuldade");
            Igual(false, Configuracao.PontoDeOuro, "PontoDeOuro");
            Igual(2, Configuracao.SetsParaVencer, "SetsParaVencer");
            Igual(ModoDeGolpe.Automatico, Configuracao.ModoDeGolpe, "ModoDeGolpe");
            Igual(false, Configuracao.Destro, "Destro");
            Igual("Marina", Configuracao.NomeDoJogador, "NomeDoJogador");
            Igual("192.168.0.40", Configuracao.EnderecoDaSala, "EnderecoDaSala");
            Igual(9100, Configuracao.PortaDaSala, "PortaDaSala");
            Igual(9200, Configuracao.PortaParaCriar, "PortaParaCriar");
            Igual(0.25f, Configuracao.Volume, "Volume");
            Exigir(!File.Exists(caminho + ".tmp"), "o temporário deveria ter virado o arquivo");
        }),
        Sincrono("JSON corrompido volta ao padrão e segue", () =>
        {
            foreach (var conteudo in new[] { "{ isto não é json", "null", "[1, 2]", "{\"Dificuldade\": \"Impossivel\"}", "" })
            {
                Configuracao.Dificuldade = Dificuldade.Dificil;
                string caminho = Arquivo("corrompido.json");
                File.WriteAllText(caminho, conteudo);
                Igual(ResultadoDaCarga.Corrompido, Configuracao.Carregar(caminho), $"resultado de '{conteudo}'");
                Igual(Dificuldade.Medio, Configuracao.Dificuldade, $"Dificuldade depois de '{conteudo}'");
            }
        }),
        Sincrono("campo fora da faixa volta só ele ao padrão", () =>
        {
            string caminho = Arquivo("fora-da-faixa.json");
            File.WriteAllText(caminho, "{\"Dificuldade\": \"Dificil\", \"SetsParaVencer\": 5, \"PortaDaSala\": 0, \"PortaParaCriar\": 70000, \"Volume\": 7, \"EnderecoDaSala\": \"a b\", \"Nome\": \"   \"}");
            Igual(ResultadoDaCarga.Carregado, Configuracao.Carregar(caminho), "resultado");
            Igual(Dificuldade.Dificil, Configuracao.Dificuldade, "Dificuldade");
            Igual(1, Configuracao.SetsParaVencer, "SetsParaVencer");
            Igual(Configuracao.PortaPadrao, Configuracao.PortaDaSala, "PortaDaSala");
            Igual(Configuracao.PortaPadrao, Configuracao.PortaParaCriar, "PortaParaCriar");
            Igual(1f, Configuracao.Volume, "Volume");
            Igual(Configuracao.EnderecoPadrao, Configuracao.EnderecoDaSala, "EnderecoDaSala");
            Igual(Configuracao.NomePadrao, Configuracao.NomeDoJogador, "NomeDoJogador");
        }),
        Sincrono("campo ausente no arquivo fica com o padrão", () =>
        {
            string caminho = Arquivo("vazio.json");
            File.WriteAllText(caminho, "{}");
            Configuracao.PontoDeOuro = false;
            Configuracao.Destro = false;
            Igual(ResultadoDaCarga.Carregado, Configuracao.Carregar(caminho), "resultado");
            Igual(true, Configuracao.PontoDeOuro, "PontoDeOuro");
            Igual(true, Configuracao.Destro, "Destro");
            Igual(Configuracao.VolumePadrao, Configuracao.Volume, "Volume");
        }),
        Sincrono("carregar não mexe no que veio da linha de comando", () =>
        {
            string caminho = Arquivo("com-linha-de-comando.json");
            Exigir(Configuracao.Salvar(caminho, out var erro), $"Salvar falhou: {erro}");
            Ler("--auto", "--sair-apos", "3", "--semente", "7", "--screenshot", "a.png");
            Configuracao.Carregar(caminho);
            Igual(ModoDeJogo.Demonstracao, Configuracao.Modo, "Modo");
            Igual<double?>(3, Configuracao.SairApos, "SairApos");
            Igual<uint?>(7, Configuracao.Semente, "Semente");
            Igual<string?>("a.png", Configuracao.Screenshot, "Screenshot");
            Exigir(Configuracao.PularMenu, "PularMenu deveria continuar ligado");
        }),
        Sincrono("o que vale só pra esta execução não vai pro arquivo", () =>
        {
            Ler("--auto", "--sair-apos", "3", "--semente", "7", "--screenshot", "a.png");
            string json = Configuracao.ParaJson();
            foreach (var campo in new[] { "Modo\"", "SairApos", "Semente", "Screenshot", "PularMenu" })
                Exigir(!json.Contains(campo, StringComparison.Ordinal), $"'{campo}' não deveria ir pro arquivo");
        }),
        Sincrono("salvar num caminho impossível devolve false sem derrubar o jogo", () =>
        {
            string arquivo = Arquivo("sou-um-arquivo");
            File.WriteAllText(arquivo, "");
            Exigir(!Configuracao.Salvar(Path.Combine(arquivo, "configuracao.json"), out var erro), "Salvar deveria falhar");
            Exigir(!string.IsNullOrEmpty(erro), "a falha deveria vir com a mensagem");
        }),

        // ---- Dados do placar ----
        Sincrono("placar 15-30 no 3-2", () =>
        {
            var d = DadosDoPlacar.De(TestePlacarNode.Normal(), "Casa", "Rivais", new Mensagem("oi"), pingMs: 31);
            Igual(3, d.GamesCasa, "GamesCasa");
            Igual(2, d.GamesRivais, "GamesRivais");
            Igual("15", d.PontosCasa, "PontosCasa");
            Igual("30", d.PontosRivais, "PontosRivais");
            Igual(0, d.SetsAnteriores.Count, "sets anteriores");
            Exigir(d.TimeQueSaca is 0 or 1, $"TimeQueSaca deveria ser 0 ou 1, veio {d.TimeQueSaca}");
            Exigir(!d.EmTieBreak && !d.PontoDecisivo && !d.PartidaEncerrada, "nem tie-break, nem ponto decisivo, nem fim");
            Igual<string?>("oi", d.Mensagem, "Mensagem");
            Igual<int?>(31, d.PingMs, "PingMs");
        }),
        Sincrono("40-40 com ponto de ouro marca o ponto decisivo", () =>
        {
            var d = DadosDoPlacar.De(TestePlacarNode.PontoDeOuro(), "Casa", "Rivais", new Mensagem("ouro", Destaque: true));
            Igual("40", d.PontosCasa, "PontosCasa");
            Igual("40", d.PontosRivais, "PontosRivais");
            Exigir(d.PontoDecisivo, "PontoDecisivo deveria estar ligado");
            Exigir(d.MensagemEmDestaque && !d.MensagemSuave, "a mensagem deveria vir em destaque");
        }),
        Sincrono("tie-break: pontos em número e o set anterior com o tie-break", () =>
        {
            var d = DadosDoPlacar.De(TestePlacarNode.TieBreak(), "Casa", "Rivais");
            Exigir(d.EmTieBreak, "EmTieBreak deveria estar ligado");
            Exigir(!d.PontoDecisivo, "tie-break não é ponto de ouro");
            Igual("5", d.PontosCasa, "PontosCasa");
            Igual("4", d.PontosRivais, "PontosRivais");
            Igual(2, d.SetsAnteriores.Count, "sets anteriores");
            Igual(new SetAnterior(6, 4), d.SetsAnteriores[0], "1º set");
            Igual(new SetAnterior(6, 7, 5, 7), d.SetsAnteriores[1], "2º set");
            Igual<int?>(1, d.SetsAnteriores[1].Vencedor, "vencedor do 2º set");
            Igual<int?>(5, d.SetsAnteriores[1].TieBreakDoPerdedor, "tie-break de quem perdeu o 2º set");
            Igual<int?>(null, d.SetsAnteriores[0].TieBreakDoPerdedor, "1º set sem tie-break");
        }),
        Sincrono("partida encerrada esconde a marca de saque", () =>
        {
            var d = DadosDoPlacar.De(TestePlacarNode.Encerrada(), "Casa", "Rivais");
            Exigir(d.PartidaEncerrada, "PartidaEncerrada deveria estar ligado");
            Igual(-1, d.TimeQueSaca, "TimeQueSaca");
            Igual(3, d.SetsAnteriores.Count, "sets anteriores");
            Igual(new SetAnterior(7, 6, 10, 8), d.SetsAnteriores[2], "3º set");
        }),
        Sincrono("MesmoConteudo compara os sets item a item", () =>
        {
            var a = DadosDoPlacar.De(TestePlacarNode.TieBreak(), "Casa", "Rivais");
            var b = DadosDoPlacar.De(TestePlacarNode.TieBreak(), "Casa", "Rivais");
            Exigir(a.MesmoConteudo(b), "mesmo placar deveria ter o mesmo conteúdo");
            Exigir(!a.MesmoConteudo(b with { SetsAnteriores = [new SetAnterior(6, 4)] }), "sets diferentes não são o mesmo conteúdo");
            Exigir(!a.MesmoConteudo(b with { PontosCasa = "6" }), "pontos diferentes não são o mesmo conteúdo");
            Exigir(!a.MesmoConteudo(null), "nulo não é o mesmo conteúdo");
        }),

        // ---- Entrada do controle ----
        Sincrono("A e B de qualquer controle confirmam e voltam", () =>
        {
            TemaPadelizou.ConfigurarEntradaDaInterface();
            foreach (int controle in new[] { 0, 1, 2, 3 })
            {
                Exigir(Botao(JoyButton.A, controle, apertado: true).IsActionPressed("ui_accept"), $"A do controle {controle} deveria ser ui_accept");
                Exigir(Botao(JoyButton.B, controle, apertado: true).IsActionPressed("ui_cancel"), $"B do controle {controle} deveria ser ui_cancel");
            }
        }),
        Sincrono("configurar a entrada duas vezes não duplica o A e o B", () =>
        {
            TemaPadelizou.ConfigurarEntradaDaInterface();
            int aceitar = InputMap.ActionGetEvents("ui_accept").Count, cancelar = InputMap.ActionGetEvents("ui_cancel").Count;
            TemaPadelizou.ConfigurarEntradaDaInterface();
            Igual(aceitar, InputMap.ActionGetEvents("ui_accept").Count, "eventos de ui_accept");
            Igual(cancelar, InputMap.ActionGetEvents("ui_cancel").Count, "eventos de ui_cancel");
        }),
        // ---- Perfil do jogador (PerfilLocal): arquivo, conquistas, gravação ----
        Sincrono("perfil: arquivo ausente começa um perfil novo com o nome e a mão das opções, sem gravar nada", () =>
        {
            var perfil = new PerfilLocal(Arquivo("perfil-ausente.json"));
            Configuracao.NomeDoJogador = "Ana";
            Configuracao.Destro = false;
            Igual("Ana", perfil.Atual.Nome, "nome");
            Exigir(!perfil.Atual.Destro, "a mão deveria vir das opções (canhota)");
            Exigir(!File.Exists(Arquivo("perfil-ausente.json")), "ler não deveria gravar");
        }),
        Sincrono("perfil: registrar uma partida grava, e o arquivo relido tem o mesmo perfil", () =>
        {
            var caminho = Arquivo("perfil-registro.json");
            var perfil = new PerfilLocal(caminho);
            var resumo = ResumoDeUmaPartidaDeIA(pontos: 6);
            var novas = perfil.Registrar(resumo);
            Exigir(File.Exists(caminho), "registrar deveria gravar o perfil");
            var relido = new PerfilLocal(caminho).Atual;
            Igual(perfil.Atual.GolpesPorTipo.Values.Sum(), relido.GolpesPorTipo.Values.Sum(), "golpes relidos");
            Igual(perfil.Atual.Conquistas.Count, relido.Conquistas.Count, "conquistas relidas");
            Exigir(resumo.PontosVencidos == 0 || novas.Any(c => c.Id == "PRIMEIRO_PONTO"), "com ponto vencido, PRIMEIRO_PONTO deveria sair na lista de novas");
            Exigir(!AoLado("perfil-registro.json").Any(), "sobrou arquivo temporário da gravação");
        }),
        Sincrono("perfil: conquista nova sai uma vez só — registrar de novo não a devolve", () =>
        {
            var perfil = new PerfilLocal(Arquivo("perfil-idempotente.json"));
            var resumo = ResumoDeUmaPartidaDeIA(pontos: 6);
            var primeira = perfil.Registrar(resumo);
            var segunda = perfil.Registrar(resumo);
            Exigir(primeira.Count > 0, "a primeira partida deveria desbloquear alguma coisa (PRIMEIRO_PONTO)");
            Exigir(!segunda.Any(c => primeira.Any(p => p.Id == c.Id)), "conquista já desbloqueada voltou como nova");
        }),
        Sincrono("perfil: arquivo corrompido não é apagado — é guardado ao lado e o jogo segue com um perfil novo", () =>
        {
            var caminho = Arquivo("perfil-corrompido.json");
            File.WriteAllText(caminho, "{ isto não é um perfil");
            var perfil = new PerfilLocal(caminho);
            Igual(0, perfil.Atual.Partidas, "o perfil novo começa do zero");
            var guardados = Directory.GetFiles(_pasta, "perfil-corrompido.json.ilegivel-*");
            Igual(1, guardados.Length, "cópia guardada do arquivo ilegível");
            Igual("{ isto não é um perfil", File.ReadAllText(guardados[0]), "conteúdo da cópia guardada");
        }),
        Sincrono("perfil: arquivo de uma versão mais nova do jogo nunca é sobrescrito nem mexido", () =>
        {
            var caminho = Arquivo("perfil-futuro.json");
            const string doFuturo = "{\"Versao\": 999, \"Nome\": \"Ana\"}";
            File.WriteAllText(caminho, doFuturo);
            var perfil = new PerfilLocal(caminho);
            perfil.Registrar(ResumoDeUmaPartidaDeIA(pontos: 2));
            Igual(doFuturo, File.ReadAllText(caminho), "o arquivo da versão mais nova");
            Exigir(!AoLado("perfil-futuro.json").Any(), "nada deveria ter sido criado ao lado do arquivo da versão mais nova");
        }),
        Sincrono("perfil: evento de fora (etapa vencida) desbloqueia CAMPEAO_DE_ETAPA e grava", () =>
        {
            var caminho = Arquivo("perfil-etapa.json");
            var perfil = new PerfilLocal(caminho);
            var novas = perfil.Registrar(new Padel.Core.Perfil.EtapaVencida());
            Exigir(novas.Any(c => c.Id == "CAMPEAO_DE_ETAPA"), "etapa vencida deveria dar CAMPEAO_DE_ETAPA");
            Exigir(new PerfilLocal(caminho).Atual.Conquistas.ContainsKey("CAMPEAO_DE_ETAPA"), "a conquista deveria estar gravada");
        }),

        Sincrono("perfil: registrar uma partida terminada grava, e o perfil relido do arquivo é o mesmo, campo a campo", () =>
        {
            // O caso acima compara só a soma dos golpes e a contagem de conquistas, com uma partida pela metade (0 partidas,
            // 0 vitórias): gravar errado o maior rally, os vencedores, a mão ou o nome passava.
            var caminho = Arquivo("perfil-registro-completo.json");
            var perfil = new PerfilLocal(caminho);
            var resumo = ResumoDeUmaPartidaDeIATerminada();
            perfil.Registrar(resumo);
            var relido = new PerfilLocal(caminho).Atual;
            Exigir(perfil.Atual.Equals(relido), $"o perfil relido difere do gravado: {Diferencas(perfil.Atual, relido)}");
            Igual(1, relido.Partidas, "partidas");
            Igual(resumo.Venceu ? 1 : 0, relido.Vitorias, "vitórias");
            Exigir(relido.MaiorRally > 0 && relido.VencedoresPorTipo.Count > 0, "uma partida inteira deveria deixar maior rally e vencedores no perfil");
        }),
        Sincrono("perfil: duas instâncias do jogo no mesmo arquivo não apagam o progresso uma da outra", () =>
        {
            var caminho = Arquivo("perfil-duas-instancias.json");
            var a = new PerfilLocal(caminho);   // o host e o cliente na mesma máquina, ou o jogo aberto duas vezes
            var b = new PerfilLocal(caminho);
            a.Registrar(ResumoTerminado());
            b.Registrar(ResumoTerminado());
            a.Registrar(ResumoTerminado());   // A ainda tinha 1 partida na memória: gravava 2 por cima da de B
            Igual(3, new PerfilLocal(caminho).Atual.Partidas, "partidas no arquivo");
            Igual(3 * ResumoTerminado().DuracaoEmSegundos, new PerfilLocal(caminho).Atual.SegundosDeJogo, "segundos de jogo no arquivo");
        }),
        Sincrono("perfil: arquivo preso por outro programa não derruba quem registra, e fica intacto", () =>
        {
            var caminho = Arquivo("perfil-preso.json");
            new PerfilLocal(caminho).Registrar(ResumoTerminado());
            string antes = File.ReadAllText(caminho);
            using (new FileStream(caminho, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))   // o antivírus, a sincronização
            {
                var perfil = new PerfilLocal(caminho);
                perfil.Registrar(ResumoTerminado());   // não pode lançar: o MostrarFim ficava sem tela de fim e sem pausa
            }
            Igual(antes, File.ReadAllText(caminho), "o arquivo que não deu pra ler");
        }),
        Sincrono("perfil: arquivo ilegível que não dá pra guardar ao lado não é sobrescrito, e quem registra segue", () =>
        {
            var caminho = Arquivo("perfil-ilegivel-preso.json");
            const string ilegivel = "{ ilegível";
            File.WriteAllText(caminho, ilegivel);
            // O destino da cópia ocupado (uma pasta com o nome que a cópia teria): o File.Move falha, como num disco sem permissão.
            var agora = DateTime.UtcNow;
            for (int s = -2; s <= 60; s++) Directory.CreateDirectory($"{caminho}.ilegivel-{agora.AddSeconds(s):yyyyMMdd-HHmmss}");
            new PerfilLocal(caminho).Registrar(ResumoTerminado());
            Igual(ilegivel, File.ReadAllText(caminho), "o arquivo ilegível que não deu pra guardar");
        }),
        Sincrono("perfil: a situação fica exposta — versão mais nova, sem leitura, gravação que falhou — com o aviso pro jogador", () =>
        {
            var futuro = Arquivo("perfil-situacao-futuro.json");
            File.WriteAllText(futuro, "{\"Versao\": 999, \"Nome\": \"Ana\"}");
            var perfil = new PerfilLocal(futuro);
            perfil.Registrar(ResumoTerminado());
            Igual(SituacaoDoPerfil.DeUmJogoMaisNovo, perfil.Situacao, "situação do perfil de uma versão mais nova");
            Exigir(perfil.Aviso?.Contains("atualize o jogo", StringComparison.Ordinal) == true, $"o aviso deveria pedir pra atualizar o jogo; veio '{perfil.Aviso}'");
            Exigir(!perfil.Gravado, "nada desta sessão foi gravado");

            var preso = Arquivo("perfil-situacao-preso.json");
            new PerfilLocal(preso).Registrar(ResumoTerminado());
            var semLeitura = new PerfilLocal(preso);
            using (new FileStream(preso, FileMode.Open, System.IO.FileAccess.Read, FileShare.None)) semLeitura.Registrar(ResumoTerminado());
            Igual(SituacaoDoPerfil.SemLeitura, semLeitura.Situacao, "situação do perfil que não deu pra ler");
            Exigir(semLeitura.Aviso is not null && !semLeitura.Gravado, "sem leitura: aviso e nada gravado");
            semLeitura.Registrar(ResumoTerminado());   // solto o arquivo, a próxima gravação leva o que ficou pendente
            Igual(SituacaoDoPerfil.Normal, semLeitura.Situacao, "situação depois de conseguir ler e gravar");
            Exigir(semLeitura.Gravado, "a pendência deveria ter sido gravada");
            Igual(3, new PerfilLocal(preso).Atual.Partidas, "partidas no arquivo depois da gravação que ficou pendente");

            string arquivoNoCaminho = Arquivo("perfil-sou-um-arquivo");
            File.WriteAllText(arquivoNoCaminho, "");
            var semDisco = new PerfilLocal(Path.Combine(arquivoNoCaminho, "perfil.json"));   // a pasta não pode existir
            semDisco.Registrar(ResumoTerminado());
            Igual(SituacaoDoPerfil.GravacaoFalhou, semDisco.Situacao, "situação da gravação que falhou");
            Exigir(semDisco.Aviso?.Contains("salvar", StringComparison.Ordinal) == true, $"o aviso deveria dizer que não deu pra salvar; veio '{semDisco.Aviso}'");
        }),
        Sincrono("perfil: reler (a tela Perfil) com progresso que não foi gravado mantém o aviso, e tenta gravar de novo", () =>
        {
            // O Reler passava pelo Carregar e zerava a situação: Normal com Gravado=False, e a tela Perfil sem aviso nenhum
            // pra quem largou a partida (sem tela de fim) com o disco cheio.
            var caminho = Arquivo("perfil-reler-pendente.json");
            new PerfilLocal(caminho).Registrar(ResumoTerminado());   // um perfil.json bom, com 1 partida
            string antes = File.ReadAllText(caminho);
            string temporario = caminho + ".gravando";
            Directory.CreateDirectory(temporario);   // o temporário não pode ser escrito: a gravação falha, como num disco cheio
            var perfil = new PerfilLocal(caminho);
            perfil.Registrar(ResumoTerminado());
            Igual(SituacaoDoPerfil.GravacaoFalhou, perfil.Situacao, "situação depois da gravação que falhou");
            var relido = perfil.Reler();
            Igual(SituacaoDoPerfil.GravacaoFalhou, perfil.Situacao, "situação depois de reler, com o progresso ainda fora do disco");
            Exigir(perfil.Aviso?.Contains("salvar", StringComparison.Ordinal) == true, $"o aviso deveria continuar dizendo que não deu pra salvar; veio '{perfil.Aviso}'");
            Exigir(!perfil.Gravado, "o progresso ainda não está no disco");
            Igual(2, relido.Partidas, "o perfil relido mostra o que ficou pendente por cima do disco");
            Igual(antes, File.ReadAllText(caminho), "o arquivo, depois da gravação que falhou de novo");

            Directory.Delete(temporario);   // o espaço voltou: reler é uma "próxima vez", e leva o que ficou pendente
            perfil.Reler();
            Igual(SituacaoDoPerfil.Normal, perfil.Situacao, "situação depois de reler com o disco de volta");
            Exigir(perfil.Aviso is null && perfil.Gravado, $"tudo gravado: sem aviso; veio '{perfil.Aviso}', Gravado={perfil.Gravado}");
            Igual(2, new PerfilLocal(caminho).Atual.Partidas, "partidas no arquivo depois de reler com o disco de volta");

            // A mesma coisa com a pendência que veio de uma leitura que falhou (arquivo preso): solto, reler grava.
            var preso = Arquivo("perfil-reler-preso.json");
            new PerfilLocal(preso).Registrar(ResumoTerminado());
            var semLeitura = new PerfilLocal(preso);
            using (new FileStream(preso, FileMode.Open, System.IO.FileAccess.Read, FileShare.None)) semLeitura.Registrar(ResumoTerminado());
            Igual(SituacaoDoPerfil.SemLeitura, semLeitura.Situacao, "situação do perfil que não deu pra ler");
            semLeitura.Reler();
            Exigir(semLeitura.Situacao != SituacaoDoPerfil.Normal || semLeitura.Gravado,
                $"situação Normal com progresso fora do disco (Gravado=False) é contraditória; aviso '{semLeitura.Aviso}'");
            Exigir(semLeitura.Gravado, "solto o arquivo, reler deveria levar a pendência pro disco");
            Igual(2, new PerfilLocal(preso).Atual.Partidas, "partidas no arquivo depois de reler o que estava preso");
        }),
        new("tela de fim: com o perfil de uma versão mais nova, avisa e não anuncia conquista como ganha", async no =>
        {
            string caminho = Arquivo("perfil-futuro-fim.json");
            File.WriteAllText(caminho, "{\"Versao\": 999, \"Nome\": \"Ana\"}");
            PerfilLocal.CaminhoPedido = caminho;
            var partida = await AbrirPartida(no);
            try
            {
                if (partida.Sessao is not SessaoLocal local) throw new Falha($"a partida deveria ser local, veio {partida.Sessao.GetType().Name}");
                JogarAteOFim(local.Partida);
                for (int i = 0; i < 120 * 30 && !partida.FimMostrado; i++) await QuadroDeFisica(no);
                Exigir(partida.FimMostrado, "a tela de fim deveria ter entrado");
                var textos = TextosDe(partida.GetNode("Telas/Fim"));
                Exigir(textos.Any(t => t.Contains("versão mais nova", StringComparison.Ordinal)), $"a tela de fim deveria avisar que o perfil não está sendo gravado; mostra [{string.Join(" | ", textos)}]");
                Exigir(!textos.Contains("Conquista!"), $"conquista que não foi gravada não pode sair como ganha; mostra [{string.Join(" | ", textos)}]");
            }
            finally { FecharPartida(partida); }
        }),
        new("tela Perfil do menu: avisa quando o perfil é de uma versão mais nova", async no =>
        {
            string caminho = Arquivo("perfil-futuro-menu.json");
            File.WriteAllText(caminho, "{\"Versao\": 999, \"Nome\": \"Ana\"}");
            PerfilLocal.CaminhoPedido = caminho;
            var menu = GD.Load<PackedScene>(PartidaNode.CenaDoMenu).Instantiate<Node>();
            no.AddChild(menu);
            try
            {
                await Quadro(no);
                var perfil = BotaoComTexto(menu, "Perfil") ?? throw new Falha("o menu deveria ter o botão Perfil");
                perfil.EmitSignal(BaseButton.SignalName.Pressed);
                await Quadro(no);
                var textos = TextosDe(menu);
                Exigir(textos.Any(t => t.Contains("versão mais nova", StringComparison.Ordinal)), $"a tela Perfil deveria avisar; mostra [{string.Join(" | ", textos.Where(t => t.Length > 0).Take(12))}…]");
            }
            finally { Descartar(menu); }
        }),
        new("tela Perfil do menu: avisa do progresso que não foi salvo quando a partida foi largada (sem tela de fim)", async no =>
        {
            string caminho = Arquivo("perfil-largada-menu.json");
            new PerfilLocal(caminho).Registrar(ResumoTerminado());   // um perfil.json bom
            string antes = File.ReadAllText(caminho);
            Directory.CreateDirectory(caminho + ".gravando");   // a gravação falha, como num disco cheio
            PerfilLocal.CaminhoPedido = caminho;
            var partida = await AbrirPartida(no);
            for (int i = 0; i < 30; i++) await QuadroDeFisica(no);
            FecharPartida(partida);   // Pausa → Sair pro menu: a abandonada registra no _ExitTree, e não há tela de fim
            Igual(SituacaoDoPerfil.GravacaoFalhou, PerfilLocal.DoJogo.Situacao, "situação depois de largar a partida com a gravação falhando");
            var menu = GD.Load<PackedScene>(PartidaNode.CenaDoMenu).Instantiate<Node>();
            no.AddChild(menu);
            try
            {
                await Quadro(no);
                var perfil = BotaoComTexto(menu, "Perfil") ?? throw new Falha("o menu deveria ter o botão Perfil");
                perfil.EmitSignal(BaseButton.SignalName.Pressed);
                await Quadro(no);
                var textos = TextosDe(menu);
                Exigir(textos.Any(t => t.Contains("salvar o perfil", StringComparison.Ordinal)),
                    $"a tela Perfil deveria avisar que o progresso não foi salvo (Situacao={PerfilLocal.DoJogo.Situacao}, Gravado={PerfilLocal.DoJogo.Gravado}); mostra [{string.Join(" | ", textos.Where(t => t.Length > 0).Take(12))}…]");
                Igual(antes, File.ReadAllText(caminho), "o arquivo do perfil (nada foi gravado)");
            }
            finally { Descartar(menu); }
        }),
        new("opções: trocar o nome e a mão grava no perfil, com a data da troca", async no =>
        {
            string caminho = Arquivo("perfil-preferencias.json");
            PerfilLocal.CaminhoPedido = caminho;
            Configuracao.NomeDoJogador = "Ana";
            Configuracao.Destro = true;
            PerfilLocal.DoJogo.Registrar(ResumoTerminado());   // o perfil já existe, com o nome e a mão de antes
            var antes = new PerfilLocal(caminho).Atual;
            var painel = new PainelDeOpcoes();
            no.AddChild(painel);
            try
            {
                painel.Abrir();
                await Quadro(no);
                var nome = painel.FindChild("Nome", recursive: true, owned: false) as LineEdit ?? throw new Falha("o painel deveria ter o campo Nome");
                nome.Text = "Bia";
                Configuracao.Destro = false;   // o que o seletor "Mão" faz ao escolher Canhoto
                var voltar = BotaoComTexto(painel, "Voltar") ?? throw new Falha("o painel deveria ter o Voltar");
                voltar.EmitSignal(BaseButton.SignalName.Pressed);   // Voltar salva
            }
            finally { Descartar(painel); }
            var depois = new PerfilLocal(caminho).Atual;
            Igual("Bia", depois.Nome, "nome no perfil");
            Exigir(!depois.Destro, "a mão no perfil deveria ser a canhota");
            Exigir(depois.PreferenciasAlteradasEm > antes.PreferenciasAlteradasEm, $"a data da troca deveria andar ({antes.PreferenciasAlteradasEm:O} → {depois.PreferenciasAlteradasEm:O}): é ela que decide a mesclagem do Cloud");
            Igual(antes.Partidas, depois.Partidas, "trocar o nome não mexe nos números");
        }),

        Sincrono("tela do perfil: as 20 conquistas, secreta escondida até sair, progresso das cumulativas", () =>
        {
            var perfil = Padel.Core.Perfil.PerfilDoJogador.Novo("Ana", true, DateTimeOffset.UnixEpoch) with
            {
                Partidas = 7,
                Vitorias = 3,
                GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Bandeja] = 42 },
                Conquistas = new Dictionary<string, DateTimeOffset> { ["CAMPEAO_DE_ETAPA"] = DateTimeOffset.UnixEpoch },
            };
            var tela = new TelaDoPerfil();
            try
            {
                tela.Preencher(perfil);
                var textos = tela.Textos();
                Igual(Padel.Core.Perfil.CatalogoDeConquistas.Todas.Count, tela.Linhas, "uma linha por conquista do catálogo");
                Exigir(textos.Any(x => x.Contains("7 partidas", StringComparison.Ordinal) && x.Contains("3 vitórias", StringComparison.Ordinal)), "os números do perfil deveriam aparecer");
                Exigir(textos.Any(x => x.Contains("1/20", StringComparison.Ordinal)), "deveria contar 1 de 20 conquistas");
                Exigir(textos.Contains("Campeão de etapa"), "a conquista desbloqueada aparece pelo nome");
                var secreta = Padel.Core.Perfil.CatalogoDeConquistas.Todas.First(c => c.Secreta);
                Exigir(!textos.Contains(secreta.Nome.Portugues), $"a secreta {secreta.Id} não deveria mostrar o nome antes de sair");
                Exigir(textos.Any(x => x.Contains("42/100", StringComparison.Ordinal)), "a cumulativa das bandejas deveria mostrar o progresso 42/100");
            }
            finally { tela.Free(); }
        }),

        // ---- Carreira x partida: o jogo da etapa é da partida que o "Jogar" abriu, e de mais nenhuma ----
        new("carreira: a partida da carreira abandonada leva o jogo com ela — a próxima partida não decide esse jogo", async no =>
        {
            var (jogo, dificuldade, sets) = CarreiraComJogoPronto();
            EstadoDaCarreira.PrepararJogo(jogo, dificuldade, sets);   // o "Jogar" da tela da carreira
            var daCarreira = await AbrirPartida(no);
            try { Exigir(daCarreira.EmCarreira, "a partida aberta pelo Jogar da carreira deveria ser da carreira"); }
            finally { FecharPartida(daCarreira); }   // Pausa → Sair pro menu (ou Opções): sai da árvore sem tela de fim
            Configuracao.Modo = ModoDeJogo.Local;   // no menu, Jogar
            var avulsa = await AbrirPartida(no);
            try
            {
                Exigir(!avulsa.EmCarreira, "a partida avulsa aberta depois da carreira abandonada ainda é da carreira: no fim ela decidiria o jogo da etapa");
                Exigir(avulsa.NomesDasDuplas is null, $"a avulsa ficou com os nomes das duplas da carreira: {string.Join(" x ", avulsa.NomesDasDuplas ?? [])}");
            }
            finally { FecharPartida(avulsa); }
            Exigir(!JogoNoDisco(jogo.Numero).Jogado, "o jogo da carreira não pode ter resultado: ninguém o jogou até o fim");
        }),
        new("carreira: a partida da carreira que acabou e foi largada no replay do último ponto guarda o resultado", async no =>
        {
            var (jogo, dificuldade, sets) = CarreiraComJogoPronto();
            EstadoDaCarreira.PrepararJogo(jogo, dificuldade, sets);
            var partida = await AbrirPartida(no);
            try
            {
                if (partida.Sessao is not SessaoLocal local) throw new Falha($"a partida da carreira deveria ser local, veio {partida.Sessao.GetType().Name}");
                JogarAteOFim(local.Partida);   // o Core até o fim, sem quadro nenhum: a tela de fim ainda não entrou
            }
            finally { FecharPartida(partida); }   // o jogador sai durante o replay do último ponto
            Exigir(JogoNoDisco(jogo.Numero).Jogado, "a partida terminou: o resultado deveria estar guardado na carreira (sair no replay não desfaz uma derrota)");
        }),
        new("carreira: o jogo usa a dificuldade, o formato e a semente dele sem mexer nas preferências do jogador", async no =>
        {
            Configuracao.Dificuldade = Dificuldade.Dificil;
            Configuracao.SetsParaVencer = 2;
            var (jogo, _, _) = CarreiraComJogoPronto();
            EstadoDaCarreira.PrepararJogo(jogo, Dificuldade.Facil, 1);
            var partida = await AbrirPartida(no);
            try
            {
                if (partida.Sessao is not SessaoLocal local) throw new Falha($"a partida da carreira deveria ser local, veio {partida.Sessao.GetType().Name}");
                Igual(Dificuldade.Facil, local.Partida.Opcoes.Dificuldade, "dificuldade da partida (a do rival)");
                Igual(1, local.Partida.Opcoes.SetsParaVencer, "formato da partida (o da etapa)");
                Exigir(local.Partida.Opcoes.Semente is not null, "a partida da carreira deveria ter a semente do jogo");
            }
            finally { FecharPartida(partida); }
            // O menu grava a Configuracao no configuracao.json no próximo Jogar: o que está aqui é o que fica salvo.
            Igual(Dificuldade.Dificil, Configuracao.Dificuldade, "a dificuldade das opções do jogador");
            Igual(2, Configuracao.SetsParaVencer, "o formato das opções do jogador");
            Igual<uint?>(null, Configuracao.Semente, "a semente (a avulsa seguinte sairia sempre igual)");
        }),

        // ---- A partida de verdade ----
        new("pausa: Esc, P e Start com a pausa aberta continuam a partida — e ela não reabre no quadro seguinte", async no =>
        {
            var partida = await AbrirPartida(no);
            try
            {
                var teclas = new (string Nome, Func<bool, InputEvent> Evento)[]
                {
                    ("Esc", apertado => new InputEventKey { PhysicalKeycode = Key.Escape, Keycode = Key.Escape, Pressed = apertado }),
                    ("P", apertado => new InputEventKey { PhysicalKeycode = Key.P, Keycode = Key.P, Pressed = apertado }),
                    ("Start", apertado => new InputEventJoypadButton { ButtonIndex = JoyButton.Start, Device = 0, Pressed = apertado }),
                };
                foreach (var (nome, evento) in teclas)
                {
                    await ApertarDeVerdade(no, evento);
                    Exigir(partida.Pausado && partida.TelaDePausaAberta, $"{nome} deveria abrir a pausa");
                    await ApertarDeVerdade(no, evento);
                    for (int i = 0; i < 5; i++) await QuadroDeFisica(no);
                    Exigir(!partida.Pausado && !partida.TelaDePausaAberta, $"{nome} com a pausa aberta deveria continuar a partida, mas a pausa reabriu (pausado={partida.Pausado}, tela aberta={partida.TelaDePausaAberta})");
                }
            }
            finally
            {
                no.GetTree().Paused = false;
                FecharPartida(partida);
            }
        }),

        new("\"Jogar de novo\" mantém --bot, --esperar, --rede-ruim e --sem-replay da linha de comando", async no =>
        {
            PartidaNode.LinhaDeComando = () => ["--bot", "--esperar", "20", "--rede-ruim", "100", "0.05", "--sem-replay"];
            PartidaNode.EsquecerLinhaDeComando();
            try
            {
                foreach (string qual in new[] { "a primeira partida", "a do \"Jogar de novo\"" })
                {
                    var partida = await AbrirPartida(no);   // a primeira lê a linha de comando; a segunda, não
                    try
                    {
                        Exigir(partida.Bot, $"{qual} deveria ter o humano simulado nos controles (--bot)");
                        Igual(20.0, partida.EsperarNaSala, $"espera na sala d{qual[1..]} (--esperar 20)");
                        Igual(100, partida.LatenciaDeTeste, $"latência de teste d{qual[1..]} (--rede-ruim 100 0.05)");
                        Igual(0.05, partida.PerdaDeTeste, $"perda de teste d{qual[1..]} (--rede-ruim 100 0.05)");
                        Exigir(!partida.ReplayLigado, $"{qual} deveria estar sem replay (--sem-replay)");
                    }
                    finally { FecharPartida(partida); }
                }
            }
            finally
            {
                PartidaNode.LinhaDeComando = OS.GetCmdlineUserArgs;
                PartidaNode.EsquecerLinhaDeComando();
            }
        }),
        new("volume: o das opções vale uma vez só na partida (50% soa 50%, não 25%)", async no =>
        {
            int master = AudioServer.GetBusIndex("Master");
            float antes = AudioServer.GetBusVolumeDb(master);
            try
            {
                Configuracao.Volume = 0.5f;
                Configuracao.AplicarVolume();   // o que o menu faz ao abrir, e as Opções a cada mudança
                var partida = await AbrirPartida(no);
                try
                {
                    var som = partida.GetNodeOrNull<SomNode>("Som") ?? throw new Falha("a partida deveria ter o nó Som");
                    float total = AudioServer.GetBusVolumeDb(master) + som.VolumeGeralDb;
                    float esperado = Mathf.LinearToDb(0.5f);
                    Exigir(Math.Abs(total - esperado) < 0.05f, $"ganho total de {total:F2} dB (barramento {AudioServer.GetBusVolumeDb(master):F2} + som {som.VolumeGeralDb:F2}); esperado {esperado:F2} dB");
                }
                finally { FecharPartida(partida); }
            }
            finally { AudioServer.SetBusVolumeDb(master, antes); }
        }),

        // ---- O arquivo da carreira: o que não dá pra ler nunca é sobrescrito sem cópia ----
        new("carreira de um jogo mais novo: a tela diz, e o arquivo fica intacto byte a byte", async no =>
        {
            string doFuturo = System.Text.RegularExpressions.Regex.Replace(TextoDeUmaCarreira(), "\"Versao\":\\s*1", "\"Versao\": 2");
            string caminho = Arquivo("carreira-do-futuro.json");
            File.WriteAllText(caminho, doFuturo);
            EstadoDaCarreira.UsarArquivo(caminho);
            var tela = await AbrirCarreira(no);
            try
            {
                Exigir(File.ReadAllText(caminho) == doFuturo, "o arquivo da carreira de um jogo mais novo foi reescrito");
                Exigir(!AoLado("carreira-do-futuro.json").Any(), $"nada deveria ser criado ao lado: {string.Join(", ", AoLado("carreira-do-futuro.json").Select(Path.GetFileName))}");
                Exigir(TextosDe(tela).Any(t => t.Contains("mais nova", StringComparison.Ordinal)), $"a tela deveria dizer que a carreira é de uma versão mais nova; mostra [{string.Join(" | ", TextosDe(tela))}]");
            }
            finally { tela.QueueFree(); }
        }),
        new("carreira ilegível (arquivo cortado): guardada ao lado antes de começar outra, e a tela diz onde", async no =>
        {
            string inteiro = TextoDeUmaCarreira();
            string cortado = inteiro[..(inteiro.Length / 2)];
            string caminho = Arquivo("carreira-cortada.json");
            File.WriteAllText(caminho, cortado);
            EstadoDaCarreira.UsarArquivo(caminho);
            var tela = await AbrirCarreira(no);
            try
            {
                var guardados = Directory.GetFiles(_pasta, "carreira-cortada.json.ilegivel-*");
                Igual(1, guardados.Length, "cópia guardada da carreira ilegível");
                Igual(cortado, File.ReadAllText(guardados[0]), "conteúdo da cópia guardada");
                Exigir(TextosDe(tela).Any(t => t.Contains(Path.GetFileName(guardados[0]), StringComparison.Ordinal)), $"a tela deveria dizer onde a carreira ilegível foi guardada; mostra [{string.Join(" | ", TextosDe(tela))}]");
            }
            finally { tela.QueueFree(); }
        }),
        new("carreira que não deu pra ler (arquivo preso por outro programa): não começa outra e oferece tentar de novo", async no =>
        {
            string texto = TextoDeUmaCarreira();
            string caminho = Arquivo("carreira-presa.json");
            File.WriteAllText(caminho, texto);
            EstadoDaCarreira.UsarArquivo(caminho);
            Control tela;
            using (new FileStream(caminho, FileMode.Open, System.IO.FileAccess.Read, FileShare.None))   // o antivírus, a sincronização
            {
                tela = await AbrirCarreira(no);
                Exigir(EstadoDaCarreira.Atual is null, "com o arquivo preso, nenhuma carreira nova deveria começar (ela seria gravada por cima da de verdade)");
            }
            try
            {
                Igual(texto, File.ReadAllText(caminho), "o arquivo da carreira");
                var tentar = BotaoComTexto(tela, "Tentar de novo") ?? throw new Falha($"a tela deveria oferecer 'Tentar de novo'; mostra [{string.Join(" | ", TextosDe(tela))}]");
                tentar.EmitSignal(BaseButton.SignalName.Pressed);
                Exigir(EstadoDaCarreira.Atual is not null, "solto o arquivo, 'Tentar de novo' deveria abrir a carreira");
                Igual(texto, File.ReadAllText(caminho), "o arquivo da carreira depois de abrir");
            }
            finally { tela.QueueFree(); }
        }),

        // ---- Entrada da partida (EntradaLocal): sozinho x coop no sofá ----
        Sincrono("sozinho: qualquer controle joga (A, B, direcional) e o L é lob", () =>
        {
            EntradaLocal.ConfigurarMapa(coop: false);
            foreach (int controle in new[] { 0, 1, 2 })
            {
                Exigir(Botao(JoyButton.A, controle, apertado: true).IsActionPressed(EntradaLocal.Acao), $"A do controle {controle} deveria ser a ação jogando sozinho");
                Exigir(Botao(JoyButton.B, controle, apertado: true).IsActionPressed(EntradaLocal.Lob), $"B do controle {controle} deveria ser o lob jogando sozinho");
                Exigir(Eixo(JoyAxis.LeftX, -1, controle).IsActionPressed(EntradaLocal.Esquerda), $"o analógico do controle {controle} deveria mover jogando sozinho");
            }
            Exigir(TeclaFisica(Key.L).IsActionPressed(EntradaLocal.Lob), "L deveria ser lob jogando sozinho");
        }),
        Sincrono("coop: o controle 1 e o IJKL são do segundo jogador, não do primeiro", () =>
        {
            EntradaLocal.ConfigurarMapa(coop: true);
            Exigir(Botao(JoyButton.A, 0, apertado: true).IsActionPressed(EntradaLocal.Acao), "A do controle 0 deveria ser a ação do primeiro");
            Exigir(!Botao(JoyButton.A, 1, apertado: true).IsActionPressed(EntradaLocal.Acao), "A do controle 1 é do segundo jogador: não pode balançar o primeiro");
            Exigir(!Botao(JoyButton.B, 1, apertado: true).IsActionPressed(EntradaLocal.Lob), "B do controle 1 é do segundo jogador: não pode dar lob pelo primeiro");
            Exigir(!Eixo(JoyAxis.LeftX, -1, 1).IsActionPressed(EntradaLocal.Esquerda), "o analógico do controle 1 não pode mover o primeiro");
            Exigir(!TeclaFisica(Key.L).IsActionPressed(EntradaLocal.Lob), "L é a direita do segundo jogador (IJKL): não pode dar lob pelo primeiro");
            Exigir(TeclaFisica(Key.Shift).IsActionPressed(EntradaLocal.Lob), "Shift continua sendo o lob do primeiro no coop");
            EntradaLocal.ConfigurarMapa(coop: false);
            Exigir(Botao(JoyButton.A, 1, apertado: true).IsActionPressed(EntradaLocal.Acao), "voltando a jogar sozinho, o controle 1 volta a jogar");
            Exigir(TeclaFisica(Key.L).IsActionPressed(EntradaLocal.Lob), "voltando a jogar sozinho, o L volta a ser lob");
        }),
        Sincrono("Start de qualquer controle pausa, sozinho e no coop", () =>
        {
            foreach (bool coop in new[] { false, true })
            {
                EntradaLocal.ConfigurarMapa(coop);
                foreach (int controle in new[] { 0, 1, 2 })
                    Exigir(Botao(JoyButton.Start, controle, apertado: true).IsActionPressed(EntradaLocal.Pausa), $"Start do controle {controle} deveria pausar (coop={coop})");
            }
            EntradaLocal.ConfigurarMapa(coop: false);
        }),
        Sincrono("configurar o mapa da partida de novo não duplica evento", () =>
        {
            EntradaLocal.ConfigurarMapa(coop: false);
            int acao = InputMap.ActionGetEvents(EntradaLocal.Acao).Count, lob = InputMap.ActionGetEvents(EntradaLocal.Lob).Count;
            EntradaLocal.ConfigurarMapa(coop: true);
            EntradaLocal.ConfigurarMapa(coop: false);
            Igual(acao, InputMap.ActionGetEvents(EntradaLocal.Acao).Count, "eventos da ação");
            Igual(lob, InputMap.ActionGetEvents(EntradaLocal.Lob).Count, "eventos do lob");
        }),
        new("tela do perfil: o direcional do controle rola a lista com o foco no Voltar", async no =>
        {
            TemaPadelizou.ConfigurarEntradaDaInterface();
            // Como no menu: dentro de uma coluna de altura fixa, menor que as 20 conquistas — tem o que rolar.
            var coluna = new MarginContainer { Size = new Vector2(900, 480) };
            var tela = new TelaDoPerfil();
            coluna.AddChild(tela);
            no.AddChild(coluna);
            try
            {
                tela.Preencher(Padel.Core.Perfil.PerfilDoJogador.Novo("Ana", true, DateTimeOffset.UnixEpoch));
                tela.Voltar.GrabFocus();
                await Quadro(no);
                await Quadro(no);
                int antes = tela.Rolagem;
                Apertar(no, JoyButton.DpadDown, controle: 1);
                await Quadro(no);
                Exigir(tela.Rolagem > antes, $"o direcional pra baixo deveria rolar a lista (rolagem {antes} → {tela.Rolagem})");
                Exigir(tela.Voltar.HasFocus(), "o foco fica no Voltar");
                int meio = tela.Rolagem;
                Apertar(no, JoyButton.DpadUp, controle: 1);
                await Quadro(no);
                Exigir(tela.Rolagem < meio, $"o direcional pra cima deveria voltar a lista (rolagem {meio} → {tela.Rolagem})");
            }
            finally { coluna.QueueFree(); }
        }),
        new("botão focado responde ao A do segundo controle", async no =>
        {
            TemaPadelizou.ConfigurarEntradaDaInterface();
            var botao = new Button { Text = "Coop local" };
            no.AddChild(botao);
            try
            {
                int apertos = 0;
                botao.Pressed += () => apertos++;
                botao.GrabFocus();
                await Quadro(no);
                Exigir(botao.HasFocus(), "o botão deveria estar com o foco");
                Apertar(no, JoyButton.A, controle: 1);
                await Quadro(no);
                Igual(1, apertos, "vezes que o botão foi apertado");
            }
            finally { botao.QueueFree(); }
        }),
        new("campo de texto edita com o A e termina com o B do segundo controle", async no =>
        {
            TemaPadelizou.ConfigurarEntradaDaInterface();
            var campo = new LineEdit { Text = "192.168.0.12" };
            TemaPadelizou.EditarComControle(campo);
            no.AddChild(campo);
            try
            {
                // GrabFocus() direto já entra editando (como um clique); pela navegação o foco chega sem editar.
                campo.GrabFocus();
                campo.Unedit();
                await Quadro(no);
                Exigir(campo.HasFocus() && !campo.IsEditing(), "o campo deveria estar com o foco, sem editar");
                Apertar(no, JoyButton.A, controle: 1);
                Exigir(campo.IsEditing(), "o A do controle 1 deveria começar a edição");
                Apertar(no, JoyButton.B, controle: 1);
                Exigir(!campo.IsEditing(), "o B do controle 1 deveria terminar a edição");
            }
            finally { campo.QueueFree(); }
        }),
    ];

    // ---- Ajudantes ----

    private static Caso Sincrono(string nome, Action acao) => new(nome, _ =>
    {
        acao();
        return Task.CompletedTask;
    });

    private static List<string> Ler(params string[] args) => Configuracao.LerLinhaDeComando(args);

    private static void Nenhum(List<string> avisos) =>
        Exigir(avisos.Count == 0, $"não esperava aviso, veio [{string.Join(" | ", avisos)}]");

    private static void Um(List<string> avisos) =>
        Exigir(avisos.Count == 1, $"esperava um aviso, veio {avisos.Count}: [{string.Join(" | ", avisos)}]");

    private static string Arquivo(string nome) => Path.Combine(_pasta, nome);

    /// <summary>Arquivos "NOME.algo" ao lado de NOME (o padrão "NOME.*" do .NET também casa com o próprio NOME — herança do DOS).</summary>
    private static IEnumerable<string> AoLado(string nome) =>
        Directory.GetFiles(_pasta).Where(f => Path.GetFileName(f).StartsWith(nome + ".", StringComparison.Ordinal));

    /// <summary>Uma partida de verdade só com IA, até <paramref name="pontos"/> pontos, com o coletor no jogador 0.</summary>
    private static Padel.Core.Perfil.ResumoDaPartida ResumoDeUmaPartidaDeIA(int pontos)
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = 7 });
        using var coletor = new Padel.Core.Perfil.ColetorDaPartida(partida, Padel.Core.Perfil.ModoDaPartida.Local, 0);
        for (int i = 0; i < 120 * 600 && partida.Estatisticas.Pontos < pontos; i++) partida.Avancar(1f / 120f);
        return coletor.Resumo();
    }

    /// <summary>Uma carreira nova num arquivo da pasta temporária, com a primeira etapa começada: o próximo jogo do jogador.</summary>
    private static (JogoDoTorneio Jogo, Dificuldade Dificuldade, int Sets) CarreiraComJogoPronto()
    {
        const string eu = "Ana / Parceiro";
        EstadoDaCarreira.UsarArquivo(Arquivo($"carreira-{Guid.NewGuid():N}.json"));
        EstadoDaCarreira.Nova(eu, 11);
        var carreira = EstadoDaCarreira.Atual ?? throw new Falha("a carreira nova não ficou carregada");
        carreira.IniciarEtapa();
        EstadoDaCarreira.Salvar();
        var jogo = EstadoDaCarreira.ProximoJogoDoJogador() ?? throw new Falha("a etapa começada deveria ter um jogo da dupla do jogador");
        var torneio = carreira.EtapaEmAndamento ?? throw new Falha("a etapa deveria estar em andamento");
        string rival = jogo.DuplaA == eu ? jogo.DuplaB : jogo.DuplaA;
        return (jogo, EstadoDaCarreira.DificuldadePara(torneio.Dupla(rival).Forca), carreira.Etapas[carreira.IndiceDaProximaEtapa].SetsParaVencer);
    }

    /// <summary>O jogo como está no arquivo da carreira (relido do disco, não da memória).</summary>
    private static JogoDoTorneio JogoNoDisco(int numero)
    {
        var carreira = Carreira.Carregar(File.ReadAllText(EstadoDaCarreira.Caminho));
        var torneio = carreira.EtapaEmAndamento ?? throw new Falha("a carreira no disco deveria ter a etapa em andamento");
        return torneio.Jogos.First(j => j.Numero == numero);
    }

    /// <summary>O texto de uma carreira válida, como o jogo grava (feita e descartada numa pasta à parte).</summary>
    private static string TextoDeUmaCarreira()
    {
        string caminho = Arquivo($"carreira-modelo-{Guid.NewGuid():N}.json");
        EstadoDaCarreira.UsarArquivo(caminho);
        EstadoDaCarreira.Nova("Ana / Parceiro", 5);
        string texto = File.ReadAllText(caminho);
        File.Delete(caminho);
        EstadoDaCarreira.UsarArquivo(null);
        return texto;
    }

    /// <summary>A tela da carreira de verdade, filha do nó da conferência, depois do primeiro quadro.</summary>
    private static async Task<Control> AbrirCarreira(Node no)
    {
        var tela = GD.Load<PackedScene>(PartidaNode.CenaDaCarreira).Instantiate<Control>();
        no.AddChild(tela);
        await Quadro(no);
        return tela;
    }

    /// <summary>Todos os textos visíveis de uma tela (rótulos, textos ricos, botões), na ordem da árvore.</summary>
    private static List<string> TextosDe(Node raiz)
    {
        var textos = new List<string>();
        void Juntar(Node no)
        {
            switch (no)
            {
                case Label rotulo: textos.Add(rotulo.Text); break;
                case RichTextLabel rico: textos.Add(rico.Text); break;
                case Button botao when botao.Visible: textos.Add(botao.Text); break;
            }
            foreach (var filho in no.GetChildren()) Juntar(filho);
        }
        Juntar(raiz);
        return textos;
    }

    private static Button? BotaoComTexto(Node raiz, string texto)
    {
        if (raiz is Button { Visible: true } b && b.Text == texto) return b;
        foreach (var filho in raiz.GetChildren())
            if (BotaoComTexto(filho, texto) is Button achado) return achado;
        return null;
    }

    /// <summary>A cena da partida de verdade, filha do nó da conferência, depois do primeiro quadro.</summary>
    private static async Task<PartidaNode> AbrirPartida(Node no)
    {
        var partida = GD.Load<PackedScene>(PartidaNode.CenaDaPartida).Instantiate<PartidaNode>();
        no.AddChild(partida);
        await Quadro(no);
        return partida;
    }

    /// <summary>Uma partida inteira só com IA (terminada), com o coletor no jogador 0.</summary>
    private static Padel.Core.Perfil.ResumoDaPartida ResumoDeUmaPartidaDeIATerminada()
    {
        var partida = new Partida(new OpcoesDaPartida { Humanos = OpcoesDaPartida.NinguemHumano, Semente = 7 });
        using var coletor = new Padel.Core.Perfil.ColetorDaPartida(partida, Padel.Core.Perfil.ModoDaPartida.Local, 0);
        for (int i = 0; i < 120 * 3600 && !partida.Acabou; i++) partida.Avancar(1f / 120f);
        Exigir(partida.Acabou, "a partida de IA deveria acabar em uma hora de jogo");
        return coletor.Resumo();
    }

    /// <summary>Os campos em que dois perfis diferem (pra mensagem de falha).</summary>
    private static string Diferencas(Padel.Core.Perfil.PerfilDoJogador a, Padel.Core.Perfil.PerfilDoJogador b)
    {
        static string Dic<T>(IReadOnlyDictionary<T, int> d) where T : notnull => string.Join(",", d.OrderBy(p => p.Key.ToString()).Select(p => $"{p.Key}={p.Value}"));
        var campos = new (string Nome, string A, string B)[]
        {
            ("Nome", a.Nome, b.Nome), ("Destro", $"{a.Destro}", $"{b.Destro}"), ("PreferenciasAlteradasEm", $"{a.PreferenciasAlteradasEm:O}", $"{b.PreferenciasAlteradasEm:O}"),
            ("Partidas", $"{a.Partidas}", $"{b.Partidas}"), ("Vitorias", $"{a.Vitorias}", $"{b.Vitorias}"), ("MaiorRally", $"{a.MaiorRally}", $"{b.MaiorRally}"),
            ("SegundosDeJogo", $"{a.SegundosDeJogo:R}", $"{b.SegundosDeJogo:R}"), ("GolpesPorTipo", Dic(a.GolpesPorTipo), Dic(b.GolpesPorTipo)),
            ("VencedoresPorTipo", Dic(a.VencedoresPorTipo), Dic(b.VencedoresPorTipo)),
            ("Conquistas", string.Join(",", a.Conquistas.OrderBy(c => c.Key).Select(c => $"{c.Key}@{c.Value:O}")), string.Join(",", b.Conquistas.OrderBy(c => c.Key).Select(c => $"{c.Key}@{c.Value:O}"))),
        };
        return string.Join("; ", campos.Where(c => c.A != c.B).Select(c => $"{c.Nome}: '{c.A}' ≠ '{c.B}'"));
    }

    /// <summary>Uma partida terminada e vencida, montada à mão (o perfil só soma o que ela diz).</summary>
    private static Padel.Core.Perfil.ResumoDaPartida ResumoTerminado() => new()
    {
        Modo = Padel.Core.Perfil.ModoDaPartida.Local,
        Terminada = true,
        Venceu = true,
        PontosVencidos = 24,
        PontosPerdidos = 10,
        DuracaoEmSegundos = 300,
        MaiorRally = 9,
        GolpesPorTipo = new Dictionary<TipoDeGolpe, int> { [TipoDeGolpe.Saque] = 17, [TipoDeGolpe.Normal] = 40 },
        GolpesNaRede = 2,
    };

    /// <summary>Tira da árvore na hora (não recebe mais entrada) e libera no fim do quadro.</summary>
    private static void Descartar(Node no)
    {
        no.GetParent()?.RemoveChild(no);
        no.QueueFree();
    }

    /// <summary>A partida sai da árvore (como numa troca de cena) e é liberada.</summary>
    private static void FecharPartida(PartidaNode partida)
    {
        partida.GetParent()?.RemoveChild(partida);
        partida.Free();
    }

    /// <summary>Joga a partida até o fim direto no Core, com o humano simulado no lugar de quem está nos controles.</summary>
    private static void JogarAteOFim(Partida partida)
    {
        var bots = Enumerable.Range(0, 4).Where(i => partida.Opcoes.Humanos[i])
            .Select(i => (Indice: i, Bot: new HumanoSimulado(i, PerfilDeHumano.Avancado, new Aleatorio(7u + (uint)i), true))).ToList();
        var entradas = new Entrada[4];
        const float passo = 1f / 120f;
        for (int i = 0; i < 120 * 3600 && !partida.Acabou; i++)
        {
            Array.Clear(entradas);
            if (EstadoVisivel.De(partida) is EstadoVisivel estado)
                foreach (var (indice, bot) in bots) entradas[indice] = bot.Decidir(estado, passo);
            partida.Avancar(passo, entradas);
        }
        Exigir(partida.Acabou, "a partida deveria ter acabado em uma hora de jogo");
    }

    private static InputEventJoypadButton Botao(JoyButton botao, int controle, bool apertado) =>
        new() { ButtonIndex = botao, Device = controle, Pressed = apertado };

    private static InputEventJoypadMotion Eixo(JoyAxis eixo, float valor, int controle) =>
        new() { Axis = eixo, AxisValue = valor, Device = controle };

    private static InputEventKey TeclaFisica(Key tecla) => new() { PhysicalKeycode = tecla, Pressed = true };

    /// <summary>Aperta e solta um botão do controle, direto no viewport (o caminho que o Godot usa, sem depender de janela).</summary>
    private static void Apertar(Node no, JoyButton botao, int controle)
    {
        no.GetViewport().PushInput(Botao(botao, controle, apertado: true));
        no.GetViewport().PushInput(Botao(botao, controle, apertado: false));
    }

    private static async Task Quadro(Node no) => await no.ToSignal(no.GetTree(), SceneTree.SignalName.ProcessFrame);

    private static async Task QuadroDeFisica(Node no) => await no.ToSignal(no.GetTree(), SceneTree.SignalName.PhysicsFrame);

    /// <summary>
    /// Aperta e solta pelo Input (o caminho do teclado e do controle de verdade: atualiza o "acabou de apertar" que o
    /// polling lê E entrega o evento aos nós), com quadros de física no meio.
    /// </summary>
    private static async Task ApertarDeVerdade(Node no, Func<bool, InputEvent> evento)
    {
        Input.ParseInputEvent(evento(true));
        for (int i = 0; i < 3; i++) await QuadroDeFisica(no);
        Input.ParseInputEvent(evento(false));
        for (int i = 0; i < 3; i++) await QuadroDeFisica(no);
    }

    private static void Exigir(bool condicao, string mensagem)
    {
        if (!condicao) throw new Falha(mensagem);
    }

    private static void Igual<T>(T esperado, T obtido, string oque)
    {
        if (!EqualityComparer<T>.Default.Equals(esperado, obtido)) throw new Falha($"{oque}: esperado {Mostrar(esperado)}, veio {Mostrar(obtido)}");
    }

    private static string Mostrar<T>(T valor) => valor is null ? "null" : $"'{valor}'";
}
