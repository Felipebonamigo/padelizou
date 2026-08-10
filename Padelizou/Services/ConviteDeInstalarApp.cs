namespace Padelizou.Services;

// QUANDO convidar a instalar o app, e com que conversa.
//
// O convite genérico aparecia no primeiro carregamento de página de quem está logado no
// celular — que pode ser em cima de uma inscrição, no meio de uma consulta de horário, ou na
// tela que a pessoa abriu com pressa no estacionamento do clube. Convite que interrompe é
// convite recusado, e até 10/08/2026 recusa era definitiva (ver a régua em
// wwwroot/js/instalar-app.js).
//
// O momento que funciona é o contrário disso: LOGO DEPOIS de a pessoa ganhar alguma coisa que
// vai gerar um aviso. Quem acabou de se inscrever num torneio quer saber quando as chaves
// saem; quem acabou de marcar aula quer o lembrete. Aí o app não é um favor que a gente pede
// — é a resposta pra pergunta que ela acabou de fazer.
//
// Os textos moram aqui, e não espalhados pelos controllers, porque são a mesma promessa dita
// de jeitos diferentes: quando ela mudar (e vai), muda num lugar só.
public static class ConviteDeInstalarApp
{
    // O sinal viaja no TempData: nasce no controller que fez a coisa boa acontecer e morre no
    // primeiro carregamento que o lê. Não toca banco e não sobrevive a um F5 — o convite é
    // daquele instante, não um lembrete pendurado na conta.
    public const string ChaveTempData = "ConviteInstalarApp";

    public const string ContaCriada = "conta";
    public const string InscricaoConfirmada = "inscricao";
    public const string DuplaFechada = "dupla";
    public const string AulaMarcada = "aula";

    public record Conversa(string Selo, string Titulo, string Texto);

    // Devolve null quando não há convite a fazer — inclusive pro motivo desconhecido, que é o
    // que chega de um TempData gravado pela versão anterior do site durante um deploy.
    public static Conversa? Da(string? motivo) => motivo switch
    {
        ContaCriada => new Conversa(
            "CONTA CRIADA",
            "Falta 1 toque pra virar app",
            "Instala o Padelizou na tela inicial pra receber os avisos que importam: seu jogo "
            + "chegando, as chaves saindo, aula marcada."),

        InscricaoConfirmada => new Conversa(
            "INSCRIÇÃO CONFIRMADA",
            "Receba o aviso quando as chaves saírem",
            "Com o Padelizou na tela inicial, o horário do seu primeiro jogo chega no celular "
            + "— sem precisar ficar abrindo o site pra conferir."),

        DuplaFechada => new Conversa(
            "DUPLA FECHADA",
            "Receba o aviso quando as chaves saírem",
            "Com o Padelizou na tela inicial, você fica sabendo do sorteio, do horário e de "
            + "qualquer mudança no torneio antes de sair de casa."),

        AulaMarcada => new Conversa(
            "AULA MARCADA",
            "Receba o lembrete da sua aula",
            "Com o Padelizou na tela inicial, o lembrete chega no celular — e você fica sabendo "
            + "na hora se o professor remarcar ou desmarcar."),

        _ => null
    };
}
