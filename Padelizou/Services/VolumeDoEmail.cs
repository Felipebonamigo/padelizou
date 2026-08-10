namespace Padelizou.Services;

// QUANTO cabe no canal de e-mail, e a partir de quando o Felipe precisa saber.
//
// ⚠️ Existe por causa de 09/08/2026: o disparo de "Novo torneio aberto" (87 e-mails) estourou a
// cota do Gmail e **130 e-mails morreram calados** no resto do dia — duas recuperações de senha
// entre eles. O `EmailService` engole a falha de propósito (não pode quebrar a inscrição de quem
// clicou), então e-mail que falha simplesmente SOME. A descoberta veio horas depois, por um
// jogador dizendo que não conseguia entrar.
//
// Os números são os do plano grátis do Resend (conferidos na página deles em 09/08/2026):
// 100/dia e 3.000/mês.
public static class TetoDoEmail
{
    public const int PorDia = 100;

    // Fica aqui por completude, mas ⚠️ NÃO é ele que aperta: depois dos cortes de 09/08 o
    // volume mensal ficou em ~300-500, ou seja, 6× de folga. Quem encosta é o teto DIÁRIO,
    // porque o disparo de um torneio acontece todo de uma vez. Não há vigia mensal justamente
    // por isso, e porque contagem em memória não sobrevive a um mês de deploys.
    public const int PorMes = 3000;

    // Avisar aos 80% e não aos 100% é o ponto inteiro: aos 100% o aviso não sairia, porque o
    // canal que ele usaria é o que acabou de acabar.
    public const int PorcentoParaAvisar = 80;

    public static int LimiarDeAviso => PorDia * PorcentoParaAvisar / 100;

    public static bool PertoDoTeto(int noDia) => noDia >= LimiarDeAviso;
}

// A contagem do que saiu e do que falhou.
//
// ⚠️ DIFERENÇA DELIBERADA PRO `VolumeDoWhatsApp`: lá o volume DECIDE se a mensagem sai
// (`RegistrarSePuder` devolve false e descarta). Aqui não existe essa porta, e não é esquecimento
// — barrar e-mail no teto seria nós mesmos jogando fora uma recuperação de senha, que é
// exatamente o estrago que este arquivo existe pra evitar. Quem recusa é o provedor; nosso papel
// é só saber que está perto e contar quem já se perdeu.
//
// Singleton, em memória, mesma escolha do WhatsApp: **um restart zera a contagem**. Escrever no
// banco a cada e-mail sairia caro pra medir um teto, e restart é gesto deliberado (deploy). O
// preço é que um deploy no meio de uma rajada esconde o aviso daquele dia.
public class VolumeDoEmail
{
    private readonly object _tranca = new();
    private readonly Queue<DateTime> _saidas = new();
    private readonly Queue<DateTime> _falhas = new();

    public void RegistrarEnvio(DateTime agora)
    {
        lock (_tranca) { Esquecer(agora); _saidas.Enqueue(agora); }
    }

    // Falha aqui quase nunca é uma só: quando a cota estoura, todo o resto do dia falha junto.
    // É a contagem disto que transforma "sumiu um e-mail" em "o canal está fora desde as 14h".
    public void RegistrarFalha(DateTime agora)
    {
        lock (_tranca) { Esquecer(agora); _falhas.Enqueue(agora); }
    }

    public int NoDia(DateTime agora)
    {
        lock (_tranca) { Esquecer(agora); return _saidas.Count(q => q.Date == agora.Date); }
    }

    public int FalhasNoDia(DateTime agora)
    {
        lock (_tranca) { Esquecer(agora); return _falhas.Count(q => q.Date == agora.Date); }
    }

    public bool PertoDoTeto(DateTime agora) => TetoDoEmail.PertoDoTeto(NoDia(agora));

    // O dia é o CIVIL, de meia-noite a meia-noite em Brasília — não as últimas 24h. É assim que
    // se lê "o sistema mandou 90 e-mails hoje". ⚠️ O Resend conta o dia dele em UTC, então perto
    // da meia-noite os dois números divergem um pouco; pra um aviso de 80% isso não muda nada, e
    // fingir precisão aqui só esconderia o que o número quer dizer.
    private void Esquecer(DateTime agora)
    {
        var limite = agora.Date.AddDays(-1);
        while (_saidas.Count > 0 && _saidas.Peek() < limite) _saidas.Dequeue();
        while (_falhas.Count > 0 && _falhas.Peek() < limite) _falhas.Dequeue();
    }
}
