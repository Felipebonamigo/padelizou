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
}

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
}
