using Padelizou.Services;

namespace Padelizou.ViewModels;

// Painel "Métricas de uso" do admin — os números que dizem se o sistema está crescendo
// e quanto a plataforma já faturou no ano (pro controle do teto do MEI).
public class MetricasAdminVM
{
    // Cadastros
    public int TotalJogadores { get; set; }
    public int JogadoresNovos7 { get; set; }
    public int JogadoresNovos30 { get; set; }

    // Inscrições em torneio (Dupla + Americano)
    public int InscricoesNovas7 { get; set; }
    public int InscricoesNovas30 { get; set; }

    // Pagamentos confirmados
    public int PagamentosConfirmados30 { get; set; }
    public decimal ValorConfirmado30 { get; set; }

    // Engajamento
    public int JogadoresComApp { get; set; }
    public int TorneiosAtivos { get; set; }
    public int TorneiosTotal { get; set; }

    // Tráfego (Services/MetricasDeAcesso) — só site público, dev e admin ficam de fora.
    public int AcessosHoje { get; set; }
    // ⚠️ NÃO é presença ("gente online agora"): é o minuto mais cheio do dia, sem WebSocket
    // pra saber quantas abas estão abertas. Ver o porquê em MetricasDeAcesso.
    public int PicoDeAcessosNoMinuto { get; set; }
    // Quando o registro de acessos começou. Null = nunca registrou nada. A tabela usa isto pra
    // avisar por que as linhas mais antigas não têm número — elas são anteriores à medição.
    public DateTime? AcessosDesde { get; set; }

    // MEI — comissão da plataforma no ano corrente contra o teto anual
    public decimal ComissaoAno { get; set; }
    public decimal TetoMei { get; set; }
    public int PercentualMei => TetoMei > 0 ? (int)Math.Round(ComissaoAno / TetoMei * 100) : 0;

    // ---- Backup fora do servidor ----
    // Quando a última cópia pro Drive terminou. Nulo = nunca houve (ou o carimbo sumiu).
    //
    // Fica no painel, SEMPRE visível, porque o aviso por e-mail sozinho não bastou: ele sai
    // uma vez por semana e depende de um canal que pode estar fora do ar — em 07/08/2026 a
    // cota diária do Gmail estourou, e um alerta que não chega é igual a não ter alerta.
    // Aqui o estado está sempre à mão de quem abre o painel, sem depender de entrega nenhuma.
    public DateTime? UltimoBackupFora { get; set; }

    // Nulo quando este ambiente não faz backup (dev não tem, e não precisa ter).
    public bool VigiaDeBackupLigado { get; set; }

    public bool BackupAtrasado =>
        VigiaDeBackupLigado && VigiaDoBackup.PrecisaAvisar(UltimoBackupFora, DateTime.Now);

    public string BackupComoTexto => VigiaDoBackup.DescreverAtraso(UltimoBackupFora, DateTime.Now);

    // Como a série está fatiada: "dia", "semana" ou "mes" (ver Services/FaixasDeMetricas).
    // Semana é o padrão — foi o único agrupamento que existiu até 03/08/2026.
    public string Agrupamento { get; set; } = FaixasDeMetricas.Semana;

    // A série em si, da fatia mais ANTIGA pra mais recente (a tela inverte pra desenhar).
    public List<FaixaMetricaVM> Faixas { get; set; } = new();
}

// Uma linha da série: um dia, uma semana ou um mês, conforme o Agrupamento.
public class FaixaMetricaVM
{
    public DateTime Inicio { get; set; }
    public string Rotulo { get; set; } = "";
    public int Cadastros { get; set; }
    public int Inscricoes { get; set; }
    public int Pagamentos { get; set; }
    public decimal Valor { get; set; }

    // NULL NÃO É ZERO AQUI. Null = a fatia inteira é anterior ao primeiro acesso registrado,
    // ou seja, ninguém estava contando — a tela mostra "·" e não "—". Zero é uma afirmação
    // sobre o tráfego; null é a ausência de afirmação. (Registro nasceu em 18/08/2026.)
    public int? Acessos { get; set; }

    // A fatia em que a medição COMEÇOU: tem número, mas só da parte do período em que já se
    // contava. Sem isto, o dia que começou às 20h29 seria comparado de igual pra igual com um
    // dia inteiro e leria como queda de tráfego.
    public bool AcessosParciais { get; set; }
}

// Painel "Jogadores por região" — de onde vem quem se cadastra, e em que ritmo cada lugar
// cresce. Usa as mesmas fatias (dia/semana/mês) da tela de métricas, porque é a mesma
// pergunta com um recorte a mais.
public class RegioesAdminVM
{
    // "estado" ou "cidade" (ver Services/JogadoresPorRegiao).
    public string Por { get; set; } = JogadoresPorRegiao.PorEstado;

    // "dia", "semana" ou "mes" (ver Services/FaixasDeMetricas).
    public string Agrupamento { get; set; } = FaixasDeMetricas.Semana;

    // A região aberta embaixo da lista, quando o admin clica numa linha. Nulo = nenhuma.
    public string? ChaveSelecionada { get; set; }

    public int TotalContado { get; set; }
    public int QuantosEstados { get; set; }
    public int QuantasCidades { get; set; }

    // Os dois buracos da medição, contados separado porque são gente diferente: dá pra ter
    // escrito "RS" e não ter escrito a cidade, e o contrário também.
    public int SemEstado { get; set; }
    public int SemCidade { get; set; }

    // Quantas pessoas estão numa UF que elas não escreveram — a cidade delas é que disse.
    // Vai pro rodapé: número deduzido tem que se apresentar como deduzido.
    public int HerdaramAUfDaCidade { get; set; }

    // Os rótulos das fatias, da mais ANTIGA pra mais recente — mesma ordem de `PorFaixa`.
    public List<string> RotulosDasFaixas { get; set; } = new();

    public List<RegiaoNaTelaVM> Regioes { get; set; } = new();

    public RegiaoNaTelaVM? Selecionada =>
        ChaveSelecionada == null ? null : Regioes.FirstOrDefault(r => r.Chave == ChaveSelecionada);

    // Quantos entraram na janela inteira da série — é a soma das linhas, e não o total da
    // base: cadastro anterior a 25/07/2026 não tem data e não entra em fatia nenhuma.
    public int NovosNoPeriodo => Regioes.Sum(r => r.NoPeriodo);

    // A escala das barrinhas: o maior número de cadastros que UMA região fez em UMA fatia.
    // É a mesma pra todas as linhas de propósito — barra por barra, a comparação é entre
    // regiões, e normalizar cada linha pelo próprio máximo faria a cidade de 2 cadastros
    // parecer do tamanho da de 20.
    public int MaiorNaFaixa => Regioes.SelectMany(r => r.PorFaixa).DefaultIfEmpty(0).Max();
}

// Uma linha da tabela de regiões.
public class RegiaoNaTelaVM
{
    public string Rotulo { get; set; } = "";
    public string Chave { get; set; } = "";

    // O "Não informado" — é linha de verdade, mas não é uma região: a tela o marca.
    public bool NaoInformada { get; set; }

    public int Total { get; set; }

    // Cadastros anteriores a 25/07/2026, que não têm data e por isso não entram na série.
    public int SemData { get; set; }

    // Cadastros em cada fatia, na ordem de `RotulosDasFaixas`.
    public List<int> PorFaixa { get; set; } = new();

    public int NoPeriodo => PorFaixa.Sum();
}
