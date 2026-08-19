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

    // Grava UM acesso. Sem tentar deduplicar por pessoa — reload da mesma página conta de
    // novo, porque a pergunta é tráfego, não visitante único.
    public static async Task RegistrarAsync(DbPadelContext context)
    {
        context.AcessosAoSite.Add(new AcessoAoSite { Quando = DateTime.Now });
        await context.SaveChangesAsync();
    }
}
