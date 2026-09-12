using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O SILÊNCIO GERAL: o sistema inteiro calado por um botão do painel.
//
// Pedido do Felipe (11/09/2026). O caso que ele resolve é o envio em fuga — um job novo
// disparando aviso pra base inteira, um torneio duplicado avisando todo mundo duas vezes — e
// a única saída até aqui era ssh no servidor mais restart do serviço. O momento em que isso é
// preciso é exatamente o momento em que ninguém tem o notebook na mão.
//
// É o mesmo molde do portão de acesso (ver PortaoDeAcesso), e de propósito: chave/valor na
// `ConfiguracaoDoSistema`, singleton com a cópia em memória, banco como fonte da verdade.
//
// ⚠️ Por que o banco e não uma variável em memória: um valor só na memória volta ao padrão no
// primeiro restart, e restart é o que todo deploy faz. O sistema voltaria a falar sozinho
// dias depois, no meio da noite, e ninguém ligaria uma coisa à outra.
//
// ⚠️ E por que aqui NÃO existe padrão vindo do systemd, ao contrário do portão: um sistema
// mudo é um estado de EXCEÇÃO, sempre. Uma chave de configuração pra isso seria uma forma de
// subir um ambiente calado sem ninguém ter decidido calar — e o sintoma disso ("ninguém
// recebe nada") é dos mais caros de diagnosticar, porque cada canal falha por conta própria o
// tempo todo e a suspeita nunca cai no interruptor.
public class SilencioDeAvisos
{
    public const string Chave = "Avisos.Silenciados";

    private bool _ligado;

    // True = o sistema está MUDO: nada de push, e-mail ou WhatsApp sai daqui.
    public bool Ligado => _ligado;

    // O admin já decidiu isto alguma vez? Serve pra tela distinguir "nunca foi mexido" de
    // "alguém religou" — a linha fica no banco depois de religada, e é ela que responde
    // "quem calou o sistema semana passada?".
    public bool DecididoPeloAdmin { get; private set; }

    // Lido uma vez no start. Falha aqui não pode impedir o app de subir: sem a leitura vale o
    // padrão, que é FALAR — e falar demais é um problema menor do que um sistema que sobe
    // mudo por causa de um erro de banco na inicialização.
    public async Task CarregarAsync(DbPadelContext context)
    {
        var linha = await context.ConfiguracoesDoSistema
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Chave == Chave);

        _ligado = linha != null && linha.Valor == "true";
        DecididoPeloAdmin = linha != null;
    }

    public async Task DefinirAsync(DbPadelContext context, bool silenciar, int? porJogadorId)
    {
        var linha = await context.ConfiguracoesDoSistema.FirstOrDefaultAsync(c => c.Chave == Chave);

        if (linha == null)
        {
            linha = new ConfiguracaoDoSistema { Chave = Chave };
            context.ConfiguracoesDoSistema.Add(linha);
        }

        linha.Valor = silenciar ? "true" : "false";
        linha.AtualizadoEm = DateTime.Now;
        linha.AtualizadoPorJogadorId = porJogadorId;

        await context.SaveChangesAsync();

        // Só depois de gravar: se o SaveChanges falhar, a memória não pode ficar dizendo uma
        // coisa que o banco não tem — no próximo restart o sistema voltaria a falar sozinho.
        _ligado = silenciar;
        DecididoPeloAdmin = true;
    }
}
