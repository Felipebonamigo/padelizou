using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// AS DUAS PERGUNTAS DE TRÁFEGO DA TELA DE MÉTRICAS: "quanto hoje" e "quão concentrado".
//
// ⚠️ "MÁXIMO DE ACESSOS SIMULTÂNEOS" NÃO É PRESENÇA — é DENSIDADE. Este site não tem conexão
// persistente (nem WebSocket, nem SignalR): não há como saber quantas ABAS estão abertas
// agora, só quantos ACESSOS bateram perto um do outro. A régua aqui é "o minuto mais cheio do
// dia" — quantos acessos caíram dentro do mesmo minuto-relógio, no pior caso. É uma medida
// honesta de PICO DE TRÁFEGO (útil pra saber se o servidor aguentaria mais gente), não uma
// contagem de gente "online agora" — e o texto da tela precisa deixar isso claro, senão a
// pessoa lê como se fosse um contador de presença.
public static class MetricasDeAcesso
{
    public static async Task<int> AcessosHojeAsync(DbPadelContext context, DateTime hoje) =>
        await context.AcessosAoSite.CountAsync(a => a.Quando >= hoje);

    // Agrupa por MINUTO-RELÓGIO e pega o maior grupo. Minuto-relógio, e não "qualquer janela
    // de 60s deslizante": a janela deslizante dá o pico técnico exato, mas custa bem mais pra
    // calcular (cada acesso contra os vizinhos) e a diferença prática é ruído perto do que a
    // métrica já entrega — "o site aguentou X no minuto mais cheio".
    //
    // Agrupado EM MEMÓRIA, não em SQL: mesma escolha da série de Cadastros/Inscrições logo
    // acima na mesma tela ("poucas linhas por fatia, agrupar em memória é suficiente"). Um dia
    // de acesso de uma panelinha de padel são no máximo alguns milhares de linhas — perto de
    // nada pra trazer pra memória, e evita depender de `DateTrunc`, que é extensão do Npgsql
    // (não do EF Core puro) e exigiria mais um using só pra isto.
    public static async Task<int> PicoDeAcessosNoMinutoAsync(DbPadelContext context, DateTime hoje)
    {
        var quandos = await context.AcessosAoSite
            .Where(a => a.Quando >= hoje)
            .Select(a => a.Quando)
            .ToListAsync();

        if (quandos.Count == 0) return 0;

        return quandos
            .GroupBy(q => new DateTime(q.Year, q.Month, q.Day, q.Hour, q.Minute, 0))
            .Max(g => g.Count());
    }

    // O PRIMEIRO acesso já registrado — o começo da medição. Null = nunca registrou nada.
    //
    // Serve pra separar "ninguém entrou" de "ainda não contávamos". A tabela da tela de
    // métricas mostra 14 dias, e o registro de acessos só nasceu em 18/08/2026: sem esta
    // fronteira, os doze dias anteriores apareceriam como zero, que é uma afirmação falsa
    // sobre o passado. Sai da própria tabela em vez de uma data fixa no código porque assim
    // continua certo em dev, em base restaurada de backup e se um dia a tabela for limpa.
    public static async Task<DateTime?> PrimeiroAcessoAsync(DbPadelContext context) =>
        await context.AcessosAoSite.OrderBy(a => a.Quando).Select(a => (DateTime?)a.Quando).FirstOrDefaultAsync();

    // Os acessos da série inteira, crus, pro controller fatiar junto com cadastros e
    // inscrições. Mesma escolha de sempre nesta tela: agrupa em memória.
    public static async Task<List<DateTime>> DesdeAsync(DbPadelContext context, DateTime inicio) =>
        await context.AcessosAoSite.Where(a => a.Quando >= inicio).Select(a => a.Quando).ToListAsync();

    // Grava UM acesso. Sem tentar deduplicar por pessoa — reload da mesma página conta de
    // novo, porque a pergunta é tráfego, não visitante único.
    public static async Task RegistrarAsync(DbPadelContext context)
    {
        context.AcessosAoSite.Add(new AcessoAoSite { Quando = DateTime.Now });
        await context.SaveChangesAsync();
    }
}
