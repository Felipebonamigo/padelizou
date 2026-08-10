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
//
// ⚠️ DUAS TRAVAS ENTRARAM EM 10/08/2026, depois de uma varredura de conformidade achar o
// telefone de jogador real no HTML de um perfil aberto SEM LOGIN, em produção:
//
//  · VISITANTE ANÔNIMO NUNCA VÊ. O perfil é linkado do ranking público e o robots.txt do
//    site diz `Allow: /` (ver Middleware/RobotsMiddleware) — telefone à mostra ali não é
//    "visível pra quem visita", é telefone no índice do Google. E a nossa própria Política
//    de Privacidade promete, com todas as letras, que celular nunca é público.
//  · PRÉ-CADASTRO NÃO EXPÕE CONTATO. Quem foi cadastrado por um TERCEIRO (parceiro de dupla
//    inscrito por CPF) nunca viu esta tela, nunca leu a política e não tem login pra marcar
//    "perfil privado" — o interruptor existe, mas não pra ele. Enquanto a conta não for
//    assumida, o padrão é fechado; depois, quem manda é a escolha do dono.
public static class ContatoDoJogador
{
    // O dono SEMPRE vê o próprio contato: a chave é sobre quem VISITA, e esconder do dono
    // seria esconder dele o que ele mesmo cadastrou.
    public static bool PodeVerContato(Jogador dono, int? quemOlhaId)
    {
        if (quemOlhaId == dono.Id) return true;

        // Deslogado é o caso que virou incidente: é o link que circula em grupo de WhatsApp
        // e é o que o buscador rastreia. Nem é preciso ter conta pra chegar aqui.
        if (quemOlhaId == null) return false;

        return !dono.Excluido && !dono.PerfilPrivado && !dono.EhPreCadastro;
    }

    // Além da permissão, o botão precisa de um número: celular é campo opcional no cadastro, e
    // um `wa.me/55` sem nada atrás abre o WhatsApp num contato que não existe.
    public static bool PodeChamarNoWhatsApp(Jogador dono, int? quemOlhaId) =>
        PodeVerContato(dono, quemOlhaId) && !string.IsNullOrWhiteSpace(dono.Celular);
}
