using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Padelizou.Services;

// A regra ÚNICA de quem enxerga moldura (14/08/2026).
//
// ⚠️ POR QUE ISTO EXISTE: o portão nasceu só no MoldurasController, e fechar a PORTA não
// fecha a VITRINE. Quem já tinha escolhido uma moldura antes do interruptor continuava com
// ela desenhada em toda tela — a consulta em produção achou 1 jogador nessa situação. Ou
// seja: a tela dava 404 e a moldura aparecia mesmo assim.
//
// É a "regra duplicada" de sempre, na forma mais traiçoeira: a segunda cópia da regra era a
// AUSÊNCIA dela em quem desenha. Agora o controller e o _FotoDoJogador perguntam pro mesmo
// lugar, e ligar as molduras continua sendo uma linha no systemd.
public class PortaDasMolduras
{
    private readonly MoldurasSettings _settings;

    public PortaDasMolduras(IOptions<MoldurasSettings> settings) => _settings = settings.Value;

    // Desligado, só admin do Padelizou atravessa — ele precisa ver as molduras ao vivo pra
    // decidir o modelo. Pra todo mundo a tela não existe E a foto sai limpa.
    public bool Fechada(ClaimsPrincipal? usuario) =>
        !_settings.Habilitado && usuario?.FindFirst("IsAdmin")?.Value != "true";

    public bool DivertidasLiberadas => _settings.DivertidasLiberadas;
}
