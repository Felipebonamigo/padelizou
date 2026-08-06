using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// O portão de acesso antecipado ligado e desligado de dentro do app.
//
// Até aqui o portão era só `AcessoAntecipado:Habilitado` no systemd: mexer nele exigia ssh no
// servidor e um restart do serviço. Isso serve pra configuração que muda com deploy e NÃO
// serve pro caso real, que é o lançamento público — o dia em que o portão precisa cair, e
// quem decide a hora é o Felipe, do celular, não uma janela de deploy.
//
// A regra: o systemd continua dando o PADRÃO, e o banco pode dizer o contrário.
//
// ⚠️ Por que o banco e não uma variável em memória: um valor só na memória volta ao padrão no
// primeiro restart, e restart é o que um deploy faz. O portão reapareceria sozinho depois do
// lançamento, com o site já divulgado, e ninguém ligaria uma coisa à outra.
//
// Singleton com o valor em memória porque isto é lido em TODA requisição (o middleware roda
// antes do roteamento): uma consulta ao banco por request pra ler um booleano seria caro à
// toa. O banco é a fonte da verdade, a memória é a cópia — e as duas só divergem se outra
// instância mexer, o que não acontece: o Padelizou roda em um processo por ambiente.
public class PortaoDeAcesso
{
    public const string Chave = "AcessoAntecipado.Habilitado";

    private bool? _doBanco;

    // Null = ninguém mexeu pelo app; vale o que veio da configuração.
    public bool EstaHabilitado(AcessoAntecipadoSettings settings) => _doBanco ?? settings.Habilitado;

    // O admin já decidiu isto alguma vez? Serve pra tela dizer de onde vem o estado atual —
    // "como está configurado no servidor" é diferente de "você desligou".
    public bool DecididoPeloAdmin => _doBanco != null;

    // Lido uma vez no start. Falha aqui não pode impedir o app de subir: sem a leitura, vale
    // o padrão da configuração, que é o comportamento de sempre.
    public async Task CarregarAsync(DbPadelContext context)
    {
        var linha = await context.ConfiguracoesDoSistema
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Chave == Chave);

        _doBanco = linha == null ? null : linha.Valor == "true";
    }

    public async Task DefinirAsync(DbPadelContext context, bool habilitado, int? porJogadorId)
    {
        var linha = await context.ConfiguracoesDoSistema.FirstOrDefaultAsync(c => c.Chave == Chave);

        if (linha == null)
        {
            linha = new ConfiguracaoDoSistema { Chave = Chave };
            context.ConfiguracoesDoSistema.Add(linha);
        }

        linha.Valor = habilitado ? "true" : "false";
        linha.AtualizadoEm = DateTime.Now;
        linha.AtualizadoPorJogadorId = porJogadorId;

        await context.SaveChangesAsync();

        // Só depois de gravar: se o SaveChanges falhar, a memória não pode ficar dizendo uma
        // coisa que o banco não tem — no próximo restart o portão voltaria sozinho.
        _doBanco = habilitado;
    }
}
