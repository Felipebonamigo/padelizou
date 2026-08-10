using Padelizou.Models;

namespace Padelizou.Services;

// QUEM PODE VER O TELEFONE DE QUEM — uma régua, um lugar.
//
// A régua já existia, mas morava dentro do perfil (ver JogadoresController.Perfil e
// PerfilPrivadoEContatoTests): "perfil privado" esconde Instagram e WhatsApp e mais nada, e
// conta EXCLUÍDA (LGPD) esconde o contato pra sempre. Ela saiu de lá em 10/08/2026, quando o
// aviso de jogo ganhou o botão "Chamar no WhatsApp": a segunda tela a mostrar telefone seria
// a segunda cópia da regra, e é exatamente assim que o número de quem pediu privacidade
// aparece na tela sem ninguém ter decidido isso.
//
// Vale pro contato HUMANO (o link wa.me que a pessoa clica). Não confundir com
// `NotificarWhatsApp`, que é consentimento pro aviso AUTOMÁTICO — ver ConsentimentoDoWhatsApp.
public static class ContatoDoJogador
{
    // O dono SEMPRE vê o próprio contato: a chave é sobre quem VISITA, e esconder do dono
    // seria esconder dele o que ele mesmo cadastrou.
    public static bool PodeVerContato(Jogador dono, int? quemOlhaId) =>
        quemOlhaId == dono.Id || (!dono.Excluido && !dono.PerfilPrivado);

    // Além da permissão, o botão precisa de um número: celular é campo opcional no cadastro, e
    // um `wa.me/55` sem nada atrás abre o WhatsApp num contato que não existe.
    public static bool PodeChamarNoWhatsApp(Jogador dono, int? quemOlhaId) =>
        PodeVerContato(dono, quemOlhaId) && !string.IsNullOrWhiteSpace(dono.Celular);
}
