namespace Padelizou.Services;

// Patrocinadores do SITE — a marca que paga pra aparecer no Padelizou inteiro. Não confundir
// com o patrocinador de TORNEIO, que é assunto entre organizador e a marca dele (ver o
// relatório de fechamento em Views/Torneios/Relatorio.cshtml).
//
// O lugar deles é UMA faixa discreta no rodapé, em toda página (ver .pdz-patrocinio no
// _Layout). Rodapé de propósito: patrocínio vive de estar presente, não de interromper — e
// qualquer coisa acima do conteúdo (banner, faixa no topo) viraria a primeira coisa que o
// sistema diz em toda tela.
//
// Nasce VAZIA: produção sem patrocinador configurado não mostra faixa nenhuma. Enquanto o
// patrocínio está em teste, a lista existe só no dev — no local pelo
// appsettings.Development.json, no dev.padelizou.com.br pelo systemd
// (Patrocinadores__Lista__0__Nome=Paralelo, e assim por diante).
public class PatrocinadoresSettings
{
    public List<Patrocinador> Lista { get; set; } = new();

    // O que o rodapé exibe de fato. Configuração preenchida manda sempre; sem configuração,
    // só o AMBIENTE DE TESTE (Beta__AmbienteDeTeste=true, o dev.padelizou.com.br) mostra o
    // Paralelo embutido abaixo.
    //
    // O embutido existe porque o dev não tem como ser configurado daqui: ele roda como
    // Production (o appsettings.Development.json não vale lá) e o systemd dele só se alcança
    // por SSH. Enquanto o patrocínio está em avaliação, o combinado é aparecer no dev e em
    // NENHUM outro lugar — e produção sem configuração continua sem faixa, porque o fallback
    // exige a chave que só o dev tem. Fechado o patrocínio, produção liga pelo systemd
    // (Patrocinadores__Lista__0__...) sem esperar deploy.
    public List<Patrocinador> ParaExibir(bool ambienteDeTeste)
    {
        var configurados = Lista.FindAll(p => !string.IsNullOrWhiteSpace(p.Imagem));
        if (configurados.Count > 0)
            return configurados;

        if (!ambienteDeTeste)
            return new();

        return new()
        {
            new Patrocinador
            {
                Nome = "Paralelo",
                Imagem = "/image/patrocinadores/paralelo.webp",
                Link = "https://www.paraleloesporte.com.br/",
                LogoEscuro = true,
            },
            // Arte oficial que o Grand Padel mandou (kit da marca, variantes RGB Pos-Neg-Mon):
            // a -01 colorida no tema claro, a -03 negativa branca no escuro.
            new Patrocinador
            {
                Nome = "Grand Padel",
                Imagem = "/image/patrocinadores/grand-padel.webp",
                ImagemEscura = "/image/patrocinadores/grand-padel-branco.webp",
                Link = "https://www.grandpadel.com.br/",
            },
        };
    }
}

// ⚠️ A ARTE É SEMPRE A QUE O PATROCINADOR MANDOU. Nada de redesenhar "parecido" pra adiantar
// o teste: logo é identidade registrada, e o dono repara na hora — aconteceu em 20/08/2026,
// com um logo recriado à mão que não chegou a ser publicado. Sem o arquivo oficial em mãos, o
// patrocinador simplesmente não entra na lista.
public class Patrocinador
{
    // Vai no alt e no title do logo — é o nome que o leitor de tela fala.
    public string Nome { get; set; } = "";

    // Caminho em wwwroot (ex.: /image/patrocinadores/paralelo.webp). Sem imagem o
    // patrocinador NÃO aparece: no rodapé o logo é o anúncio, e nome escrito em texto
    // cinza não é o que ninguém pagou pra ter.
    public string Imagem { get; set; } = "";

    // Site do patrocinador. Vazio = logo sem link, que é um patrocínio válido também.
    public string Link { get; set; } = "";

    // O desenho do logo é escuro (caso do Paralelo, que é preto puro): no tema escuro ele
    // sumiria contra o fundo, então o CSS inverte as cores dele (.pdz-logo-escuro no
    // site.css). Logo COLORIDO fica false — inverter cor de marca alheia é pior que o
    // contraste imperfeito.
    public bool LogoEscuro { get; set; }

    // Versão do logo pra FUNDO ESCURO: no tema escuro entra esta imagem no lugar da de cima.
    // É o caminho certo pra logo COLORIDO, que o LogoEscuro não atende — inverter azul dá
    // laranja, e marca alheia não se recolore. Quase toda marca tem a versão branca no
    // manual dela; é essa que se pede. Preenchida, ganha do LogoEscuro. Vazia = a mesma
    // imagem serve nos dois temas.
    public string ImagemEscura { get; set; } = "";
}
