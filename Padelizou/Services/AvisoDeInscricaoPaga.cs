using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O texto do aviso, puro — longe do banco e da rede, porque texto certo se testa. Mesmo padrão
// do TextoDoApito.
public static class TextoDaInscricaoPaga
{
    public const string Titulo = "Caiu o dinheiro 💰";

    // "Bruna Vargas pagou R$ 250,00. Na categoria 4ª Masculina."
    // "Lucas Almeida pagou R$ 125,00, falta R$ 125,00. Na categoria 4ª Masculina."
    //
    // ⚠️ O QUE FALTA É PARTE DA NOTÍCIA desde que a inscrição pode ser paga em partes
    // (06/10/2026). Dizer só "pagou" faria o organizador riscar a dupla da lista de cobrança —
    // é o mesmo erro que o Lucas relatou, agora por escrito numa notificação.
    //
    // ⚠️ Uma linha só, sem quebra: a notificação do celular corta o que passa de duas linhas,
    // e a caixa de entrada guarda o corpo como texto.
    public static string Corpo(string quem, decimal valorPago, decimal falta, string categoria)
    {
        var frase = falta > 0m
            ? $"{quem} pagou {valorPago:C}, falta {falta:C}."
            : $"{quem} pagou {valorPago:C}.";

        return string.IsNullOrWhiteSpace(categoria) ? frase : $"{frase} Na categoria {categoria}.";
    }
}

// Avisa QUEM RECEBE O DINHEIRO do torneio que uma inscrição foi paga.
//
// 🗣️ Felipe (06/10/2026), com o celular cheio de SMS e e-mail do Asaas: *"quero que essas
// notificações venha apenas 1 email de quando for pago e também no aplicativo q eu tenho do
// sistema"*. Os avisos de "fatura aberta" e "cobrança visualizada" são da conta Asaas e se
// desligam no painel dela; o que faltava deste lado era o aviso no app — até aqui o Padelizou
// não dizia NADA quando um pagamento de inscrição confirmava.
//
// 📧 SEM E-MAIL NOSSO, por decisão dele: o "pagamento confirmado" continua sendo o e-mail do
// Asaas. `AppSemEmail` é o alcance que a régua de 09/08/2026 criou exatamente pra isto —
// mandar o nosso junto trocaria dois e-mails por dois outros.
//
// ⚠️ É SERVIÇO, e não um trecho dentro do webhook, porque DOIS caminhos confirmam dinheiro de
// inscrição: o "pagar depois" (a inscrição já existe) e o pagamento que CRIA a inscrição.
// Regra de torneio duplicada é a causa histórica dos defeitos graves daqui.
public class AvisoDeInscricaoPaga
{
    private readonly DbPadelContext _context;
    private readonly IPushNotificationService _push;

    public AvisoDeInscricaoPaga(DbPadelContext context, IPushNotificationService push)
    {
        _context = context;
        _push = push;
    }

    public async Task NotificarAsync(int torneioId, int quemPagouId, string nomeDeQuemPagou,
        decimal valorPago, decimal falta, string categoria, string? url)
    {
        // ⚠️ SÓ QUEM VÊ O CAIXA. O ajudante faz tudo no torneio menos abrir o dinheiro
        // (Services/AcessoAoDinheiroDoTorneio) — dizer a ele quanto entrou seria furar essa
        // régua por notificação, que é a porta que ninguém audita.
        var destinatarios = await _context.TorneioOrganizadores
            .Where(o => o.TorneioId == torneioId && o.JogadorId != quemPagouId)
            .Select(o => new { o.JogadorId, o.NivelAcesso })
            .ToListAsync();

        var corpo = TextoDaInscricaoPaga.Corpo(nomeDeQuemPagou, valorPago, falta, categoria);

        foreach (var quem in destinatarios)
        {
            // `ehAdminDaPlataforma: false` de propósito: aqui a pergunta é sobre ESTE torneio,
            // e o admin do sistema não deve receber o caixa de todo mundo no celular.
            if (!AcessoAoDinheiroDoTorneio.PodeVer(quem.NivelAcesso, ehAdminDaPlataforma: false)) continue;

            await _push.EnviarParaJogadorAsync(quem.JogadorId, TextoDaInscricaoPaga.Titulo, corpo, url,
                AlcanceDoAviso.AppSemEmail);
        }
    }
}
