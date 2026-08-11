using System.Globalization;

namespace Padelizou.Services;

// QUAL VERSÃO dos Termos de Uso e da Política de Privacidade está valendo — num lugar só.
//
// Nasceu em 10/08/2026, na varredura de conformidade: o cadastro não pedia aceite nenhum e não
// guardava nada, então **não existia prova de consentimento**. O art. 8º §2 da LGPD põe o ônus
// dessa prova em quem controla os dados — ou seja, em nós. Guardar só a data resolveria metade:
// "aceitou em 10/08" não diz nada se ninguém sabe o que a página dizia naquele dia.
//
// ⚠️ A DATA NÃO PODE SER ESCRITA À MÃO NAS DUAS PÁGINAS. É a mesma lição do preço nos Termos
// (ver TermosDeUsoTests): valor repetido em dois lugares divergentes é promessa pública
// diferente do que o sistema faz. Aqui é pior — a página diria uma versão e o banco guardaria
// outra, e a prova viraria contradição. As duas telas leem daqui.
//
// COMO PUBLICAR UMA VERSÃO NOVA: mude `Atual` e `EmVigorDesde` juntos, no mesmo commit em que
// o texto muda. Quem já aceitou continua com a versão que aceitou gravada na própria linha —
// é isso que permite dizer, depois, o que cada pessoa leu.
public static class VersaoDosDocumentos
{
    // O nome da versão é a data em que ela entrou em vigor, em ISO: ordena sozinho, cabe na
    // coluna e é legível por gente. Versão "1.0" exigiria uma tabela à parte só pra dizer
    // quando cada número valeu.
    public const string Atual = "2026-08-10";

    public static readonly DateTime EmVigorDesde = new(2026, 8, 10);

    // "10 de agosto de 2026" — como as duas páginas escrevem. Fixado em pt-BR de propósito:
    // é documento jurídico brasileiro, não acompanha a cultura de quem visita.
    public static string EmVigorDesdeNaTela =>
        EmVigorDesde.ToString("d 'de' MMMM 'de' yyyy", new CultureInfo("pt-BR"));
}
