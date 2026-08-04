namespace Padelizou.Services;

// A decisão do vigia, pura e testável: o que fazer com o canal no estado em que ele está.
// Separada do serviço de fundo porque é ELA que precisa estar certa — o resto é relógio.
public enum AcaoDoVigia
{
    NadaAFazer,
    TentarReligar,
    ChamarOFelipe,
}

public static class VigiaDoWhatsApp
{
    // Quanto tempo o canal pode ficar fora sem que ninguém seja incomodado. A queda de 03/08
    // durou 17h; com o vigia, a mesma queda se resolve na primeira passada.
    public static readonly TimeSpan IntervaloEntreChecagens = TimeSpan.FromMinutes(5);

    // O e-mail não se repete antes disso. Alerta que chega de 5 em 5 minutos vira ruído, e
    // ruído a pessoa aprende a ignorar — justo o alerta que ela precisava ler.
    public static readonly TimeSpan IntervaloEntreAvisos = TimeSpan.FromHours(6);

    // Primeira passada logo depois de subir: vigia que só se manifesta daqui a horas é vigia
    // que ninguém consegue testar, e no que não se testa não se confia.
    public static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(2);

    // O que fazer, dado o estado e se o religar já foi tentado nesta rodada.
    public static AcaoDoVigia Decidir(EstadoDoCanal estado, bool jaTentouReligar)
    {
        if (!DiagnosticoDoCanal.EhProblema(estado)) return AcaoDoVigia.NadaAFazer;

        // Ainda não tentamos e o restart tem chance? Tenta — foi o que resolveu em 03/08,
        // sem QR e sem ninguém acordar.
        if (!jaTentouReligar && DiagnosticoDoCanal.AdiantaReligar(estado))
            return AcaoDoVigia.TentarReligar;

        // Sobrou o que só o Felipe resolve: aparelho removido no celular (exige QR) ou o
        // serviço de mensagem fora do ar.
        return AcaoDoVigia.ChamarOFelipe;
    }

    // O corpo do e-mail. Diz o que aconteceu, o que já foi tentado sozinho e qual é o gesto
    // que falta — um alerta que não diz o que fazer só transfere a angústia.
    public static string CorpoDoEmail(EstadoDoCanal estado, bool tentouReligar)
    {
        var tentativa = tentouReligar
            ? "<p>Já tentei religar sozinho e <strong>não resolveu</strong>.</p>"
            : "";

        var caminho = estado == EstadoDoCanal.SemResposta
            ? @"<p>O serviço de mensagem não está respondendo. No servidor:<br/>
                <code>docker ps</code> e <code>docker logs --tail 50 evolution-api</code></p>
                <p>Se o container caiu, <code>docker compose -f /opt/evolution/docker-compose.yml up -d</code>.</p>"
            : @"<p>O mais provável é que o aparelho tenha sido removido no celular — nesse caso a
                credencial morre junto e <strong>só um QR novo resolve</strong>.</p>
                <p>Peça o QR pro Claude, ou abra o painel da Evolution pelo túnel:<br/>
                <code>ssh -L 8081:127.0.0.1:8081 root@179.197.233.184</code></p>";

        return $@"
            <p>O canal de WhatsApp do Padelizou está fora: <strong>{DiagnosticoDoCanal.Frase(estado)}</strong>.</p>
            {tentativa}
            <p><strong>Enquanto isso, os avisos não chegam em quase ninguém.</strong> Hoje a
            grande maioria das contas só é alcançável por WhatsApp — quem tem o app instalado
            é a minoria, e a notificação do app é a única que continua funcionando.</p>
            {caminho}
            <p>Os avisos que falharam nesse meio-tempo <strong>não são reenviados</strong>.</p>";
    }
}
