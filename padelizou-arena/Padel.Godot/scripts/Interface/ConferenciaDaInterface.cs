using Godot;
using Padel.Core;

namespace Padel.Godot.Interface;

/// <summary>
/// Os testes da interface: linha de comando, arquivo de configuração, dados do placar e a entrada do controle.
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
            // Um quadro antes: o nó precisa estar na árvore, com viewport, pros casos que empurram entrada.
            await no.ToSignal(no.GetTree(), SceneTree.SignalName.ProcessFrame);
            foreach (var caso in Casos())
            {
                total++;
                Configuracao.RestaurarPadroes();
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

    private static InputEventJoypadButton Botao(JoyButton botao, int controle, bool apertado) =>
        new() { ButtonIndex = botao, Device = controle, Pressed = apertado };

    /// <summary>Aperta e solta um botão do controle, direto no viewport (o caminho que o Godot usa, sem depender de janela).</summary>
    private static void Apertar(Node no, JoyButton botao, int controle)
    {
        no.GetViewport().PushInput(Botao(botao, controle, apertado: true));
        no.GetViewport().PushInput(Botao(botao, controle, apertado: false));
    }

    private static async Task Quadro(Node no) => await no.ToSignal(no.GetTree(), SceneTree.SignalName.ProcessFrame);

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
