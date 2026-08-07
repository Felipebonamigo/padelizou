using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O que acontece quando uma requisição estoura sem tratamento: a linha em ErroDoSistema e,
// se a janela de silêncio deixar (ver VigiaDeErros), o aviso pros administradores raiz.
//
// O aviso sai por EnviarParaJogadorAsync — que só ENFILEIRA (push + e-mail saem por fora,
// no entregador de fundo). Importante aqui mais do que em qualquer outro lugar: este código
// roda dentro de uma requisição que JÁ falhou; segurá-la esperando SMTP só pioraria a queda.
//
// Quem chama é o Middleware/CapturaDeErro. Nada aqui pode deixar exceção escapar — erro no
// registro do erro morre no log (a responsabilidade é do chamador, que embrulha em try).
public class RegistroDeErros
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;
    private readonly ILogger<RegistroDeErros> _logger;

    public RegistroDeErros(DbPadelContext context, IPushNotificationService push,
        ILogger<RegistroDeErros> logger)
    {
        _context = context;
        _push = push;
        _logger = logger;
    }

    public async Task RegistrarAsync(Exception ex, string caminho, string metodo, int? jogadorId)
    {
        var agora = DateTime.Now;
        var erro = new ErroDoSistema
        {
            QuandoEm = agora,
            Caminho = VigiaDeErros.Cortar(caminho, 300),
            Metodo = VigiaDeErros.Cortar(metodo, 10),
            Tipo = VigiaDeErros.Cortar(ex.GetType().Name, 200),
            Mensagem = VigiaDeErros.Cortar(ex.Message, 1000),
            Detalhe = VigiaDeErros.Detalhar(ex),
            JogadorId = jogadorId,
        };

        var ultimoAviso = await _context.ErrosDoSistema
            .Where(e => e.Tipo == erro.Tipo && e.Caminho == erro.Caminho && e.AvisoEnviado)
            .MaxAsync(e => (DateTime?)e.QuandoEm);

        erro.AvisoEnviado = VigiaDeErros.DeveAvisar(ultimoAviso, agora);
        _context.ErrosDoSistema.Add(erro);

        if (erro.AvisoEnviado)
        {
            // A limpeza dos antigos pega carona no aviso (no máximo uma vez por janela de
            // silêncio): frequente o bastante pra tabela não crescer, raro o bastante pra
            // não custar nada no caminho normal. RemoveRange em vez de ExecuteDelete porque
            // os testes rodam em InMemory, que não traduz DELETE em massa.
            var limite = agora - VigiaDeErros.Retencao;
            var velhos = await _context.ErrosDoSistema.Where(e => e.QuandoEm < limite).ToListAsync();
            if (velhos.Count > 0) _context.ErrosDoSistema.RemoveRange(velhos);

            var admins = await _context.Jogadores
                .Where(j => j.IsAdminRaiz)
                .Select(j => j.Id)
                .ToListAsync();

            foreach (var adminId in admins)
            {
                // SoApp = push + e-mail. WhatsApp fica de fora: alerta técnico repetido é o
                // uso que queima chip, e o canal é reservado pro que é pessoal e urgente.
                await _push.EnviarParaJogadorAsync(adminId,
                    "Padelizou: erro em produção",
                    $"{erro.Tipo} em {erro.Metodo} {erro.Caminho} — {erro.Mensagem}",
                    "https://admin.padelizou.com.br/Admin/Erros",
                    AlcanceDoAviso.SoApp);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogError(ex, "Erro não tratado em {Metodo} {Caminho} (registro #{Id}, aviso: {Avisou}).",
            erro.Metodo, erro.Caminho, erro.Id, erro.AvisoEnviado);
    }
}
