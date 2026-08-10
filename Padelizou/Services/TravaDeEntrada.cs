using System.Collections.Concurrent;

namespace Padelizou.Services
{
    // Trava de força-bruta nas portas de entrada (login, portão de Acesso Antecipado,
    // recuperação de senha e cadastro). Sem ela dá pra tentar senhas sem limite nenhum:
    // a única coisa entre um robô e a conta de alguém era a qualidade da senha.
    //
    // São duas travas diferentes porque o dia de torneio quebra a conta ingênua "limite
    // por IP": 30 jogadores no Wi-Fi do clube saem todos pelo MESMO IP, e uma janela por
    // IP no login trancaria gente legítima na hora errada.
    //
    // - LOGIN: janela POR CONTA (o identificador digitado), contando só as FALHAS — quem
    //   acerta a senha nunca gasta janela. Protege a conta de tentativa vinda de qualquer
    //   lugar, inclusive distribuída por vários IPs. O preço conhecido: quem sabe o e-mail
    //   de outra pessoa consegue trancá-la por 5 minutos de cada vez — incômodo pequeno,
    //   aceito de propósito no lugar de deixar a senha dela ser adivinhada.
    // - PORTÃO, RECUPERAÇÃO E CADASTRO: janela POR IP, no rate limiter do próprio ASP.NET
    //   (política "entrada-por-ip", registrada no Program.cs). Essas ações não têm conta
    //   pra particionar (o portão tem UMA senha; o cadastro está criando a conta agora) e
    //   são raras por pessoa — quem estoura 10 em 5 minutos é robô.
    //
    // Em memória de propósito: é uma instância de app só por ambiente, e o objetivo é
    // transformar "milhões de tentativas" em "dez a cada cinco minutos", não ser um WAF.
    public class TravaDeEntrada
    {
        public const string PoliticaPorIp = "entrada-por-ip";

        // O cadastro tem janela PRÓPRIA e mais larga: é um formulário longo, e cada recusa
        // (CPF já usado, login curto, e-mail repetido) gasta uma tentativa. Duas pessoas
        // criando conta no mesmo Wi-Fi do clube, cada uma errando três vezes, encostariam
        // no limite dos outros — e veriam "muitas tentativas" no primeiro contato com o
        // sistema, que é o pior momento possível pra isso acontecer.
        public const string PoliticaCadastro = "cadastro-por-ip";

        // Consulta de CEP: janela própria e larga. Não é porta de entrada — é uma leitura,
        // em cache, que só serve pra preencher campo de formulário. Com o limite do cadastro
        // (20), quem corrige o CEP algumas vezes num Wi-Fi de clube encostaria no teto por
        // causa de digitação. O que a trava evita aqui é robô usando o site pra varrer o
        // ViaCEP, e pra isso 60 já é apertado o bastante.
        public const string PoliticaConsulta = "consulta-por-ip";

        // Consulta que EXIGE login (hoje: achar jogador pelo CPF na inscrição). A janela é
        // POR CONTA, e é esse o ponto: por IP, o Wi-Fi do clube em dia de torneio põe 30
        // pessoas atrás do mesmo endereço, cada uma consultando o CPF do parceiro — a trava
        // prenderia gente legítima exatamente na hora errada, que é o erro que o resto deste
        // arquivo já evita no login. E quem quisesse varrer a base precisaria de uma conta:
        // a janela segue a conta pra onde ela for, inclusive trocando de IP.
        public const string PoliticaConsultaLogada = "consulta-por-conta";

        public const int TentativasPorJanela = 10;
        public const int TentativasDeCadastro = 20;
        public const int ConsultasPorJanela = 60;
        // Inscrever uma dupla gasta duas consultas, mais as correções e a troca de categoria.
        // 60 sobra pra quem inscreve e ainda assim tira "ilimitado" de quem raspa.
        public const int ConsultasLogadasPorJanela = 60;
        public static readonly TimeSpan Janela = TimeSpan.FromMinutes(5);

        private sealed class Contagem
        {
            public DateTime InicioDaJanela;
            public int Falhas;
        }

        private readonly ConcurrentDictionary<string, Contagem> _porConta = new();

        // A mesma normalização da consulta de entrada (BuscaJogador compara tudo em
        // minúsculas): "Bona" e "bona" são a mesma conta, então gastam a mesma janela.
        public static string ChaveDaConta(string? identificador) =>
            (identificador ?? string.Empty).Trim().ToLowerInvariant();

        public bool Bloqueada(string? identificador, DateTime agora)
        {
            if (!_porConta.TryGetValue(ChaveDaConta(identificador), out var contagem)) return false;
            lock (contagem)
            {
                return agora - contagem.InicioDaJanela < Janela
                    && contagem.Falhas >= TentativasPorJanela;
            }
        }

        public void RegistrarFalha(string? identificador, DateTime agora)
        {
            LimparSeCresceuDemais(agora);
            var contagem = _porConta.GetOrAdd(ChaveDaConta(identificador),
                _ => new Contagem { InicioDaJanela = agora });
            lock (contagem)
            {
                if (agora - contagem.InicioDaJanela >= Janela)
                {
                    contagem.InicioDaJanela = agora;
                    contagem.Falhas = 0;
                }
                contagem.Falhas++;
            }
        }

        // Acertou a senha: a janela da conta zera. O dono que errou duas vezes e lembrou a
        // senha não fica carregando as falhas pro resto do dia.
        public void Zerar(string? identificador) =>
            _porConta.TryRemove(ChaveDaConta(identificador), out _);

        // Robô despejando identificadores aleatórios faria o dicionário crescer sem teto.
        // Passado o limite, joga fora as janelas já vencidas — as ativas ficam.
        private void LimparSeCresceuDemais(DateTime agora)
        {
            if (_porConta.Count < 10_000) return;
            foreach (var par in _porConta)
            {
                if (agora - par.Value.InicioDaJanela >= Janela)
                    _porConta.TryRemove(par.Key, out _);
            }
        }
    }
}
