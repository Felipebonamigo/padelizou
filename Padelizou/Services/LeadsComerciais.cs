using Padelizou.Models;

namespace Padelizou.Services;

// As regras do registro de contato do Programa de Parceiros. Ficam aqui, e não no controller,
// pelo mesmo motivo de AdministracaoTime: a pergunta "esse contato é de quem?" vai ser feita
// de novo quando a tela do próprio parceiro existir, e duas cópias da mesma regra é a causa
// número um dos bugs graves deste sistema.
public static class LeadsComerciais
{
    // 90 dias é o prazo do contrato (PARCEIROS.md). Registrar não reserva pra sempre: quem
    // não fechou em três meses não está trabalhando o contato, e prender o nome nesse caso
    // só impediria outro parceiro de tentar.
    public const int DiasDeValidade = 90;

    public const int TamanhoMaximoContato = 120;
    public const int TamanhoMaximoObservacao = 500;

    public const string Aberto = "Aberto";
    public const string Ganho = "Ganho";
    public const string Perdido = "Perdido";

    // Derivado da data, nunca gravado no banco. Ver o comentário em LeadComercial.Status.
    public const string Vencido = "Vencido";

    public static readonly string[] Tipos = { "Torneio", "Professor", "Clube" };

    // Os clientes que já estavam em negociação quando o programa nasceu (11/08/2026). Não
    // geram comissão pra ninguém, e a lista é congelada de propósito: escrita depois de o
    // primeiro parceiro vender, ela viraria "o Felipe tirou o meu cliente da lista".
    //
    // ⚠️ Isto NÃO é uma trava — é o que a tela mostra pra quem vai registrar. Bloquear por
    // comparação de nome seria pior que inútil: "Golden Point" e "GoldenPoint Padel" passariam
    // igual, e um nome legítimo parecido seria recusado sem motivo.
    public static readonly string[] PipelineCongelado =
    {
        "Loberos", "Corneteiros", "Golden Point", "Nata Padel", "Chakra Padel", "Er Padel",
        "Jonatas Portal", "Gabriel \"Índio\" Reis", "Batata Padel",
    };

    // O telefone chega da tela como a pessoa digitou ("(51) 98456-7890") e é comparado com o
    // que já está gravado. Sem normalizar, o mesmo número registrado por dois parceiros com
    // máscaras diferentes passaria pelas duas vezes — que é exatamente o que esta tela existe
    // pra impedir.
    public static string SoDigitos(string? telefone) =>
        new((telefone ?? "").Where(char.IsDigit).ToArray());

    public static DateTime VenceEm(LeadComercial lead) => lead.RegistradoEm.AddDays(DiasDeValidade);

    public static bool EstaVencido(LeadComercial lead, DateTime agora) =>
        lead.Status == Aberto && VenceEm(lead) < agora;

    // O que a tela mostra. "Vencido" é uma leitura do relógio, não um estado guardado.
    public static string Situacao(LeadComercial lead, DateTime agora) =>
        EstaVencido(lead, agora) ? Vencido : lead.Status;

    public static int DiasQueRestam(LeadComercial lead, DateTime agora) =>
        (int)Math.Ceiling((VenceEm(lead) - agora).TotalDays);

    // Quem segura o contato agora — null quer dizer livre pra registrar.
    //
    // Ganho tranca pra sempre, e é de propósito: virou cliente, tem dono. O prazo de 12 meses
    // do contrato limita quanto tempo aquele cliente RENDE ao parceiro, não a quem ele
    // pertence. Sem isso, um cliente fechado em março voltaria a ser registrável em junho por
    // outra pessoa, que ganharia a comissão de uma venda que não fez.
    public static LeadComercial? QuemSegura(IEnumerable<LeadComercial> mesmoTelefone, DateTime agora)
    {
        var lista = mesmoTelefone.ToList();

        var ganho = lista.FirstOrDefault(l => l.Status == Ganho);
        if (ganho != null) return ganho;

        // O mais antigo entre os abertos: quem registra primeiro, leva.
        return lista
            .Where(l => l.Status == Aberto && !EstaVencido(l, agora))
            .OrderBy(l => l.RegistradoEm)
            .FirstOrDefault();
    }

    // A recusa explica QUEM segura e ATÉ QUANDO. "Já existe" mandaria o Felipe abrir o banco
    // pra responder a única pergunta que o parceiro vai fazer no WhatsApp em seguida.
    public static string? ProblemaParaRegistrar(
        string contato, string telefone, string tipo,
        IEnumerable<LeadComercial> mesmoTelefone, DateTime agora,
        Func<int, string> nomeDoParceiro)
    {
        if (string.IsNullOrWhiteSpace(contato))
            return "Diga o nome do contato.";

        if (contato.Length > TamanhoMaximoContato)
            return $"O nome do contato tem no máximo {TamanhoMaximoContato} caracteres.";

        var digitos = SoDigitos(telefone);
        if (digitos.Length < 10 || digitos.Length > 11)
            return "O telefone precisa ter DDD + número (10 ou 11 dígitos). É por ele que se sabe se dois parceiros trouxeram o mesmo contato.";

        if (!Tipos.Contains(tipo))
            return "Escolha se o contato é de torneio, professor ou clube.";

        var dono = QuemSegura(mesmoTelefone, agora);
        if (dono == null) return null;

        var nome = nomeDoParceiro(dono.ParceiroId);

        if (dono.Status == Ganho)
            return $"Esse telefone já é cliente, trazido por {nome} em {dono.FechadoEm?.ToString("dd/MM/yyyy") ?? dono.RegistradoEm.ToString("dd/MM/yyyy")}. Cliente fechado não volta a ser registrável.";

        return $"{nome} registrou esse telefone em {dono.RegistradoEm:dd/MM/yyyy} e o contato é dele até {VenceEm(dono):dd/MM/yyyy} ({DiasQueRestam(dono, agora)} dias). Quem registra primeiro, leva.";
    }
}
