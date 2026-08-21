using System.Security.Claims;
using Padelizou.Models;

namespace Padelizou.Services;

// DERRUBAR OS LOGINS JÁ ABERTOS de uma conta — a peça que faltava pra "trocar a senha" querer
// dizer alguma coisa contra quem já está dentro.
//
// O problema, em uma frase: o cookie de autenticação deste site é AUTO-SUFICIENTE. Ele carrega
// quem a pessoa é, assinado pelo servidor, e não existe lista de sessões em lugar nenhum — o
// servidor não sabe quantos aparelhos estão logados numa conta, e não teria como apagar um.
// Some-se a isso o `ExpireTimeSpan` de 90 dias deslizante do Program.cs e o resultado é que um
// cookie copiado continuava valendo por três meses, renovando-se sozinho a cada visita, MESMO
// depois de a pessoa trocar a senha. Trocar a senha fechava a porta da frente e não tirava
// ninguém de dentro.
//
// A saída é a mesma do `SecurityStamp` do ASP.NET Identity, e cabe em duas peças:
//
//   1. um valor opaco (o CARIMBO) guardado na conta e copiado pra dentro do cookie, como claim;
//   2. uma conferência a cada request (Program.cs, OnValidatePrincipal).
//
// Trocar o carimbo faz todo cookie emitido ANTES deixar de casar — e eles morrem no ato, sem
// nenhuma lista de sessão precisar existir. É o custo de uma leitura por chave primária por
// request, em troca de conseguir expulsar alguém.
public static class SessoesAbertas
{
    // O nome da claim. Fica aqui, e não solto em string, porque quem escreve (ClaimsDe) e quem
    // lê (a conferência do Program.cs) são dois arquivos distantes: um erro de digitação entre
    // eles não quebraria nada visivelmente — só faria a conferência passar sempre.
    public const string TipoDaClaim = "CarimboDeSessao";

    // Opaco e sem significado: ele não é segredo (vai dentro do cookie da própria pessoa) e não
    // precisa ser imprevisível — precisa ser DIFERENTE do anterior. Guid resolve as duas coisas.
    public static string NovoCarimbo() => Guid.NewGuid().ToString("N");

    // Derruba TODOS os logins abertos desta conta, inclusive o de quem está pedindo. Quem quiser
    // sobreviver à própria derrubada reemite o cookie logo depois (é o que a troca de senha do
    // perfil faz: derruba os outros aparelhos e continua nesta aba).
    //
    // ⚠️ Não salva: quem chama já está no meio de uma transação e chama SaveChanges uma vez só.
    public static void Derrubar(Jogador jogador) => jogador.CarimboDeSessao = NovoCarimbo();

    // Este cookie ainda vale? Só isto é conferido a cada request — por isso é uma função pura,
    // sem banco e sem HTTP: o que decide se alguém entra ou não tem que ser legível de uma vez.
    //
    // ⚠️ `carimboDaConta` NULO é "esta conta nunca foi derrubada", e passa. É o que evita
    // deslogar a base inteira no deploy: os cookies que já estavam na rua não têm a claim.
    // Depois do primeiro carimbo gravado, cookie sem claim é cookie antigo — e aí é recusado.
    public static bool CookieAindaVale(string? carimboDaConta, ClaimsPrincipal? usuario)
    {
        if (string.IsNullOrEmpty(carimboDaConta)) return true;

        return usuario?.FindFirst(TipoDaClaim)?.Value == carimboDaConta;
    }
}
