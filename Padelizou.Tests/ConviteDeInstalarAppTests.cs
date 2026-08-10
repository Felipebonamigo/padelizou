using System.Text.RegularExpressions;

namespace Padelizou.Tests;

// O convite de instalar o app é a porta de entrada do único canal de aviso 100% nosso — e em
// 10/08/2026 ele alcançava 58 pessoas instaladas em 154 contas, com 4 avisos ligados. Duas
// coisas explicavam boa parte disso, e as duas eram MUDAS: o site continuava abrindo, nenhum
// log aparecia, e o estrago só existia no celular de outra pessoa.
//
//   1. Fechar o convite gravava "dispensado" PRA SEMPRE. No iPhone os três toques da
//      instalação acontecem FORA do modal ("Compartilhar" → "Adicionar à Tela de Início"),
//      então fechar é exatamente o gesto de quem vai instalar: o convite morria justo em
//      quem estava a caminho. Agora fechar ADIA, e só o "Não quero" encerra.
//   2. Quem chega por link de WhatsApp está numa WebView de dentro do WhatsApp, onde
//      "Adicionar à Tela de Início" NÃO EXISTE no menu — e o modal mandava procurar.
//
// Mesmo espírito do PwaArquivosTests e do AvisoNoCelularTests: isto roda inteiro no navegador,
// não passa por controller nenhum, e o que dá pra amarrar daqui são as decisões cujo defeito
// não apareceria em lugar nenhum.
public class ConviteDeInstalarAppTests
{
    // ⚠️ O teste que não pode ser apagado: é a volta desta linha que apaga o convite de quem
    // estava indo instalar.
    [Fact]
    public void Fechar_o_convite_adia_em_vez_de_encerrar()
    {
        var modal = ModalDeInstalar();
        var corpo = Corpo(modal, "function fecharInstalarPwaModal", "function naoQueroInstalarApp");

        Assert.True(corpo.Contains("pdzAdiarConvite(CHAVE_CONVITE_PWA)"),
            "fecharInstalarPwaModal() voltou a encerrar o convite em vez de adiá-lo. No iPhone "
            + "fechar o modal é o gesto de quem VAI instalar — os três toques acontecem fora "
            + "dele —, então isso desinscreve do convite justamente quem estava a caminho.");
    }

    // Recusar tem que ser uma escolha dita com todas as letras, e ela precisa estar LIGADA a
    // um botão: a função existir sem ninguém chamar seria o mesmo que não existir.
    [Fact]
    public void Recusar_de_vez_e_uma_escolha_propria_e_visivel()
    {
        var modal = ModalDeInstalar();

        Assert.Contains("pdzRecusarConvite(CHAVE_CONVITE_PWA)", modal);
        Assert.True(modal.Contains("onclick=\"naoQueroInstalarApp()\""),
            "Nenhum botão chama naoQueroInstalarApp(). Sem ele, a única saída definitiva some "
            + "e o convite vira insistência — que é o outro jeito de perder a pessoa.");
    }

    // A conversão da marca antiga PRECISA ser gravada. Devolvê-la sem persistir faria `em`
    // valer "agora" a cada carregamento de página: o relógio nunca completaria os 3 dias e o
    // reconvite não chegaria nunca — o mesmo defeito de antes, agora escondido.
    [Fact]
    public void A_marca_antiga_vale_como_adiamento_e_e_regravada_no_aparelho()
    {
        var corpo = Corpo(InstalarAppJs(), "function pdzLerConvite", "function pdzAdiarConvite");

        Assert.Contains("bruto === \"1\"", corpo);
        Assert.True(corpo.Contains("pdzGravarConvite(chave, convertido)"),
            "pdzLerConvite() deixou de gravar a conversão da marca antiga. Sem gravar, a data "
            + "vira 'agora' em todo carregamento, a espera nunca vence, e as pessoas que "
            + "dispensaram o convite antes de 10/08/2026 nunca mais são convidadas.");

        // A migração inteira depende do nome antigo continuar sendo o nome usado.
        Assert.Contains("'pdzPwaInstallDismissed'", ModalDeInstalar());
    }

    [Fact]
    public void Quem_recusou_nunca_mais_e_convidado()
    {
        var corpo = Corpo(InstalarAppJs(), "function pdzPodeConvidar", "async function pdzInstalarApp");

        Assert.Contains("if (estado.recusado) return false;", corpo);
        Assert.True(corpo.Contains("PDZ_ESPERA_DIAS.length"),
            "pdzPodeConvidar() não limita mais o número de convites. Convite que volta pra "
            + "sempre é o que faz a pessoa desinstalar o app pra se livrar dele.");
    }

    // O convite que sai depois de uma inscrição atropela a ESPERA de propósito — o assunto
    // mudou. Atropelar a RECUSA seria outra coisa: quem apertou "não quero" veria o modal
    // voltar na inscrição seguinte, e descobriria que aquele botão não serve pra nada.
    [Fact]
    public void O_convite_com_contexto_respeita_quem_disse_que_nao_quer()
    {
        var corpo = Corpo(ModalDeInstalar(), "function aparecerSozinho", "document.addEventListener('DOMContentLoaded', aparecerSozinho)");

        Assert.True(corpo.Contains("convite && !pdzConviteRecusado(CHAVE_CONVITE_PWA)"),
            "O convite com contexto (inscrição, aula, conta criada) voltou a abrir sem conferir "
            + "a recusa explícita. Quem apertou 'Não quero instalar' é convidado de novo a cada "
            + "inscrição — e o botão de recusar vira enfeite.");
    }

    // A régua mora num lugar só. Um modal que volte a gravar a marca na mão sai de dentro
    // dela sem ninguém perceber — e é assim que "agora não" volta a significar "não".
    [Fact]
    public void Nenhum_dos_dois_convites_grava_dispensa_por_fora_da_regua()
    {
        foreach (var (nome, conteudo) in new[] { ("instalar", ModalDeInstalar()), ("avisos", ModalDeAvisos()) })
        {
            Assert.False(conteudo.Contains("localStorage.setItem(CHAVE"),
                $"O convite de {nome} voltou a gravar a dispensa direto no localStorage, por "
                + "fora de pdzAdiarConvite/pdzRecusarConvite. Duas cópias da régua significam "
                + "uma delas tratando 'agora não' como 'nunca mais'.");
        }
    }

    [Fact]
    public void O_convite_de_avisos_usa_a_mesma_regua_do_convite_de_instalar()
    {
        var avisos = ModalDeAvisos();

        Assert.Contains("pdzPodeConvidar(CHAVE_CONVITE_AVISOS)", avisos);
        Assert.Contains("pdzAdiarConvite(CHAVE_CONVITE_AVISOS)", avisos);
        Assert.Contains("pdzRecusarConvite(CHAVE_CONVITE_AVISOS)", avisos);
    }

    // No iPhone, "Adicionar à Tela de Início" existe só no Safari de verdade. A prova é o
    // `navigator.standalone` — WhatsApp e Instagram não marcam o User-Agent de forma
    // confiável no iOS, então farejar UA ali deixaria passar justamente o caso principal.
    [Fact]
    public void O_navegador_que_nao_instala_e_reconhecido_pelo_que_ele_tem_e_nao_pelo_nome()
    {
        var corpo = Corpo(InstalarAppJs(), "function pdzNavegadorParaTrocar", "var PDZ_ESPERA_DIAS");

        Assert.True(corpo.Contains("navigator.standalone"),
            "pdzNavegadorParaTrocar() parou de usar navigator.standalone pra reconhecer o "
            + "Safari do iPhone. O User-Agent do navegador de dentro do WhatsApp não é "
            + "confiável no iOS — e é ele que traz a maior parte das visitas.");

        Assert.True(corpo.Contains("pdzTemInstalacaoNativa()"),
            "A troca de navegador precisa perder pro caso em que o próprio navegador oferece "
            + "instalar: mandar sair de um Chrome que instala é empurrar a pessoa pra longe.");
    }

    // Onde não dá pra instalar, o passo a passo tem que sair da tela — senão ele manda
    // procurar um item de menu que não existe naquele navegador, e quem procura e não acha
    // conclui que o app não instala.
    [Fact]
    public void O_passo_a_passo_some_no_navegador_que_nao_instala()
    {
        var corpo = Corpo(ModalDeInstalar(), "function montarInstalarPwaModal", "function montarTrocaDeNavegador");

        Assert.Contains("pdzNavegadorParaTrocar()", corpo);
        Assert.Contains("!trocar && plataforma === 'ios'", corpo);
        Assert.Contains("!trocar && plataforma === 'android'", corpo);
    }

    private static string ModalDeInstalar() =>
        SemComentarioRazor(Arquivo(Path.Combine("Views", "Shared", "_InstalarPwaModal.cshtml")));

    private static string ModalDeAvisos() =>
        SemComentarioRazor(Arquivo(Path.Combine("Views", "Shared", "_AtivarAvisosModal.cshtml")));

    private static string InstalarAppJs() =>
        Arquivo(Path.Combine("wwwroot", "js", "instalar-app.js"));

    private static string Corpo(string texto, string inicio, string fim)
    {
        var i = texto.IndexOf(inicio, StringComparison.Ordinal);
        Assert.True(i >= 0, $"Não achei '{inicio}'. Se foi renomeado, atualize este teste — não o apague.");

        var f = texto.IndexOf(fim, i, StringComparison.Ordinal);
        Assert.True(f > i, $"Não achei '{fim}' depois de '{inicio}': o arquivo mudou de formato.");

        return texto[i..f];
    }

    // Comentário Razor não chega ao navegador — e os daqui citam os próprios trechos que os
    // testes procuram. Sem o corte, a explicação passaria por implementação.
    private static string SemComentarioRazor(string texto) =>
        Regex.Replace(texto, @"@\*.*?\*@", "", RegexOptions.Singleline);

    private static string Arquivo(string caminhoRelativo) =>
        File.ReadAllText(Path.Combine(PastaDoProjeto(), caminhoRelativo));

    private static string PastaDoProjeto()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Padelizou", "Views")))
                return Path.Combine(dir.FullName, "Padelizou");
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Não achei a pasta do projeto web subindo a partir de " + AppContext.BaseDirectory);
    }
}
