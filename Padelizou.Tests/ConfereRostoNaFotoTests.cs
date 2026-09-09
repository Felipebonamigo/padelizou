namespace Padelizou.Tests;

// A foto de perfil passou a ser RECUSADA quando não tem rosto nenhum — pedido do Felipe em
// 09/09/2026: "não suba, se nao tiver um rosto", pra parar de virar logo de time e paisagem.
//
// A conferência roda INTEIRA no navegador (face-api.js + TinyFaceDetector), como o aviso no
// celular: nenhum controller participa, então nada disso aparece num teste de request. O que
// estes testes guardam são as decisões cujo defeito seria MUDO — o site continua abrindo, o
// build passa, e o estrago só existe no celular de outra pessoa.
//
// ⚠️ POR QUE A VARREDURA EM PEDAÇOS É OBRIGATÓRIA, e não otimização: o detector ENCOLHE a foto
// inteira pra 416px antes de olhar, então o que decide não é o tamanho do rosto em pixels, é a
// FRAÇÃO da foto que ele ocupa. Medido no Chromium em 09/09/2026, com foto real, olhando a foto
// inteira:
//
//     rosto com 8,8% da altura da foto -> acha
//     rosto com 8,3% da altura da foto -> NÃO acha
//
// 8,8% é a pessoa do peito pra cima. Foto de corpo inteiro na quadra tem o rosto em ~4% — seria
// recusada, e é foto legítima. Olhando a foto tambem em 5 pedaços ampliados, o piso cai pra
// ~3,9% (a pessoa ocupando 1/3 da altura). Tirar a varredura não quebra teste nenhum de
// renderização: só passa a recusar foto de gente de verdade, calado.
public class ConfereRostoNaFotoTests
{
    private static string RaizDoRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Padelizou", "wwwroot", "css", "site.css")))
            dir = dir.Parent;
        Assert.True(dir != null, "Não achei a raiz do repo.");
        return dir!.FullName;
    }

    private static string Arquivo(params string[] partes) =>
        File.ReadAllText(Path.Combine(new[] { RaizDoRepo(), "Padelizou" }.Concat(partes).ToArray()));

    private static string CaminhoDaLib(string nome) =>
        Path.Combine(RaizDoRepo(), "Padelizou", "wwwroot", "lib", "face-api", nome);

    private static string Roteiro() => Arquivo("wwwroot", "js", "confere-rosto.js");

    // ===================== OS ARQUIVOS =====================

    // Mesmo perigo da Poppins do cartão: a biblioteca é servida como arquivo estático e nada
    // no build sabe que ela existe. Faltando um dos três, a conferência não roda — e como ela
    // é fail-open de propósito (ver abaixo), o sintoma é a trava simplesmente NÃO EXISTIR, com
    // o site funcionando normalmente e ninguém percebendo até a próxima logo de time no perfil.
    [Fact]
    public void A_biblioteca_e_o_modelo_estao_versionados()
    {
        foreach (var nome in new[]
                 {
                     "face-api.min.js",
                     "tiny_face_detector_model-weights_manifest.json",
                     "tiny_face_detector_model-shard1",
                 })
        {
            Assert.True(File.Exists(CaminhoDaLib(nome)),
                $"Falta wwwroot/lib/face-api/{nome}. Sem ele a conferência de rosto não roda, "
                + "e como ela libera a foto quando falha, a trava some sem avisar ninguém.");
        }

        // MIT: a licença anda junto com o código, como a da Poppins.
        Assert.True(File.Exists(CaminhoDaLib("LICENSE")),
            "A licença do face-api.js (MIT) precisa estar versionada ao lado da biblioteca.");
    }

    // O manifest cita o arquivo de pesos pelo NOME, e é o navegador que vai buscar. Renomear o
    // shard quebra a conferência à distância, sem erro de build — mesmo fio do PwaArquivosTests.
    [Fact]
    public void O_manifest_do_modelo_aponta_pro_peso_que_existe()
    {
        var manifest = File.ReadAllText(CaminhoDaLib("tiny_face_detector_model-weights_manifest.json"));

        Assert.Contains("tiny_face_detector_model-shard1", manifest);
        Assert.True(File.Exists(CaminhoDaLib("tiny_face_detector_model-shard1")),
            "O manifest do modelo aponta pra um arquivo de pesos que não está no disco.");
    }

    // ===================== O QUE A CONFERÊNCIA FAZ =====================

    // ⚠️ O teste que não pode ser apagado. Ver o cabeçalho: sem a segunda passada, o piso sobe
    // de ~3,9% pra 8,8% da altura da foto, e foto de jogador na quadra passa a ser recusada.
    [Fact]
    public void A_conferencia_varre_a_foto_em_pedacos_quando_a_inteira_falha()
    {
        var js = Roteiro();

        Assert.True(js.Contains("function varrer"),
            "Sumiu a varredura em pedaços do confere-rosto.js. Olhando só a foto inteira, o "
            + "detector precisa que o rosto ocupe 8,8% da altura — foto de corpo inteiro na "
            + "quadra tem ~4% e seria recusada.");

        Assert.True(js.Contains("recorte"),
            "A varredura precisa RECORTAR e ampliar pedaços: é a ampliação que faz o rosto "
            + "pequeno ser visto, não o número de passadas.");
    }

    // A foto é opcional e o formulário é longo (nome, telefone, time, sedes). Recusar a foto
    // derrubando o POST inteiro faria a pessoa perder tudo que digitou por causa do campo que
    // menos importa — é a mesma escolha que a carência de nome já faz no EditarPerfil.
    [Fact]
    public void Foto_recusada_limpa_so_a_foto_e_nao_derruba_o_formulario()
    {
        var js = Roteiro();

        Assert.True(js.Contains("campo.value = \"\""),
            "Ao recusar, o confere-rosto.js precisa limpar o <input type=file> — assim o resto "
            + "do formulário salva normalmente e só a foto fica pra trás.");

        Assert.False(js.Contains("form.onsubmit = () => false"),
            "Recusar a foto não pode travar o formulário inteiro: quem só queria arrumar o "
            + "telefone perderia o que digitou.");
    }

    // ⚠️ FAIL-OPEN, e é decisão de produto: a conferência é uma CONVENIÊNCIA contra o engano
    // (subir a logo do time sem querer), não uma trava de segurança — ela roda no navegador da
    // pessoa e quem quiser burlar, burla. Então navegador antigo, WASM bloqueado, modelo que
    // não baixou no 4G ruim: a foto SOBE. O contrário deixaria gente sem conseguir trocar de
    // foto por um motivo que ela não tem como entender nem resolver.
    [Fact]
    public void Falha_tecnica_na_conferencia_libera_a_foto()
    {
        var js = Roteiro();

        Assert.True(js.Contains("catch"),
            "O confere-rosto.js precisa de try/catch em volta da detecção: erro técnico libera "
            + "a foto, nunca recusa.");

        Assert.True(js.Contains("// fail-open"),
            "A decisão de liberar quando a conferência não roda precisa estar escrita no "
            + "arquivo — senão a próxima sessão 'conserta' o catch achando que é buraco.");
    }

    // A conferência leva de 0,4s a 3,9s (medido com CPU 6x mais lenta, o pior caso é justamente
    // a foto SEM rosto, que varre os 5 pedaços). Nesse tempo dá pra escolher a foto e apertar
    // Salvar — e aí a foto recusada subiria assim mesmo.
    [Fact]
    public void O_envio_espera_a_conferencia_terminar()
    {
        var js = Roteiro();

        Assert.True(js.Contains("preventDefault"),
            "Sem segurar o submit, quem apertar Salvar durante a conferência sobe a foto sem "
            + "ela ter sido conferida — a trava vira sorte de cronometragem.");
    }

    // ===================== ONDE ELA VALE =====================

    // São as duas únicas portas por onde foto de perfil entra (AuthController.SalvarFotoPerfilAsync
    // é chamado do Cadastro e do EditarPerfil). Ficar em uma só deixa a outra livre, e ninguém
    // descobre até aparecer uma logo no perfil de quem acabou de se cadastrar.
    [Theory]
    [InlineData("EditarPerfil.cshtml")]
    [InlineData("Cadastro.cshtml")]
    public void As_duas_telas_que_recebem_foto_carregam_a_conferencia(string tela)
    {
        var html = Arquivo("Views", "Auth", tela);

        Assert.True(html.Contains("confere-rosto.js"),
            $"{tela} tem <input type=\"file\" name=\"foto\"> mas não carrega o confere-rosto.js — "
            + "a foto entra por ali sem passar por conferência nenhuma.");
    }

    // A biblioteca e o modelo somam ~850 KB. Baixar isso no carregamento de toda página de
    // cadastro (a primeira que a pessoa vê no site) pra uma conferência que só acontece se ela
    // escolher uma foto seria pagar adiantado por quem nem vai usar.
    [Fact]
    public void A_biblioteca_so_e_baixada_quando_a_pessoa_mexe_no_campo_da_foto()
    {
        var js = Roteiro();

        Assert.True(js.Contains("createElement(\"script\")"),
            "O face-api.js precisa ser carregado sob demanda, e não no <script> da página.");

        foreach (var tela in new[] { "EditarPerfil.cshtml", "Cadastro.cshtml" })
        {
            Assert.False(Arquivo("Views", "Auth", tela).Contains("face-api.min.js"),
                $"{tela} está carregando o face-api.min.js direto no <script>: são ~650 KB no "
                + "carregamento da página, pra quem talvez nem troque a foto.");
        }
    }

    // ⚠️ TODO O CUSTO ESTÁ NA PRIMEIRA VEZ, e ele não precisa ser esperado parado. Medido com
    // CPU 6x mais lenta: a 1ª foto leva 4,5s (baixar a biblioteca + compilar os shaders do
    // WebGL), a 2ª leva 0,8s. Entre TOCAR no campo e escolher a foto na galeria passam vários
    // segundos — é lá que esses 3,7s cabem, de graça. Sem isto a pessoa fica olhando
    // "Conferindo a foto…" por 4 segundos na única vez que ela vai usar a tela.
    [Fact]
    public void O_preparo_comeca_quando_a_pessoa_toca_no_campo_e_nao_quando_ela_ja_escolheu()
    {
        var js = Roteiro();

        Assert.True(js.Contains("\"focus\"") && js.Contains("\"click\""),
            "O confere-rosto.js precisa começar a baixar a biblioteca no toque do campo (focus e "
            + "click — no celular nem sempre vem os dois), e não só no change.");

        Assert.True(js.Contains("function aquecer"),
            "Baixar a biblioteca não basta: a primeira detecção compila os shaders do WebGL e "
            + "sozinha custa ~3s num celular modesto. O aquecimento tem que acontecer junto.");
    }
}
