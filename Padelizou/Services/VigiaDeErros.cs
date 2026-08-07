namespace Padelizou.Services;

// A regra de QUANDO um erro de produção vira aviso pros admins. Pura de propósito: quem grava
// e avisa é o RegistroDeErros; aqui só se decide.
//
// O princípio é o mesmo dos outros vigias (backup, WhatsApp): alerta que se repete vira ruído,
// e ruído ensina a ignorar — que é pior do que não ter alerta. O PRIMEIRO estouro de um erro
// avisa na hora; as repetições do mesmo erro ficam só no registro, e o mesmo erro só reavisa
// se continuar acontecendo depois da janela de silêncio.
public static class VigiaDeErros
{
    // 6 horas: um deploy quebrado de manhã reavisa à tarde se ninguém corrigiu, mas um loop
    // de erro num torneio não transforma o celular do admin num vibracall.
    public static readonly TimeSpan JanelaDeSilencio = TimeSpan.FromHours(6);

    // Guardar erro pra sempre só engorda o banco — quem investiga olha os últimos dias.
    public static readonly TimeSpan Retencao = TimeSpan.FromDays(90);

    public static bool DeveAvisar(DateTime? ultimoAvisoDoMesmoErro, DateTime agora) =>
        ultimoAvisoDoMesmoErro == null || agora - ultimoAvisoDoMesmoErro >= JanelaDeSilencio;

    // ex.ToString() traz tipo + mensagem + stack + InnerExceptions — é o que se lê pra
    // investigar. O teto existe porque exceção com laço de referência ou mensagem gerada
    // pode vir gigante, e uma coluna sem teto viraria o maior campo do banco.
    public const int TamanhoMaximoDoDetalhe = 8000;

    public static string Detalhar(Exception ex)
    {
        var texto = ex.ToString();
        return texto.Length <= TamanhoMaximoDoDetalhe ? texto : texto[..TamanhoMaximoDoDetalhe];
    }

    // Corta respeitando o teto da coluna. O banco recusaria a linha inteira por causa de uma
    // mensagem longa — e perder o REGISTRO do erro por causa do tamanho da mensagem seria o
    // vigia falhando exatamente na hora em que foi chamado.
    public static string Cortar(string? texto, int maximo) =>
        string.IsNullOrEmpty(texto) ? "" : texto.Length <= maximo ? texto : texto[..maximo];
}
