namespace Padelizou.ViewModels;

// A TELA DA DECISÃO de quem foi inscrito por outra pessoa: "Maickel inscreveu você na Copa de
// Verão · 5ª Masculina. Está certo?".
//
// Ela existe por um motivo escrito no próprio código do torneio (o gancho dos chamados do
// mural, 17/08/2026): *"aviso é lembrete, não deve ser a única porta"*. Quem apagou a
// notificação, ou não usa o app, chega aqui pela faixa da tela do torneio.
//
// `QuemFicaSozinho` é o nome de quem CONTINUA inscrito se esta pessoa recusar — nulo quando
// não sobra ninguém e a inscrição inteira acaba. A tela precisa dos dois textos: dizer "seu
// parceiro continua inscrito" pra quem está sozinho na inscrição seria descrever um desfecho
// que não vai acontecer.
public record RecusarInscricaoVM(
    int DuplaId,
    int TorneioId,
    string Torneio,
    string Categoria,
    string QuemInscreveu,
    string? QuemFicaSozinho,
    bool EstaPaga,
    bool JaConfirmei);
