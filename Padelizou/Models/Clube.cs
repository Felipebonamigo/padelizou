using System.Timers;

namespace Padelizou.Models;

public class Clube
{
    public int Id { get; set; }
    // Os três são NOT NULL no banco (IsRequired), então string vazia é o padrão honesto: o EF
    // sobrescreve ao ler, e quem cria um clube sem endereço grava "" em vez de estourar no insert.
    public string Nome { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Contato { get; set; } = string.Empty;

    // Dono do clube — atribuído só por um administrador do sistema (AdminController).
    public int? DonoId { get; set; }
    public virtual Jogador? Dono { get; set; }

    // Cidade do clube — usada pra bater com JogadorCidade nas notificações de "Marcar Jogo".
    public int? CidadeId { get; set; }
    public virtual Cidade? Cidade { get; set; }

    // Aparece nas listas de escolha do site? O clube nasce do que o jogador digitou no
    // cadastro (CatalogoLocais), então a base junta os nomes certos com "Batata", "Rogérinho."
    // e sobra de teste tipo "Clube Teste CSRF 1785325726351". Desligar aqui tira o clube de
    // TODA lista de escolha de uma vez — ver Services/CatalogoLocais.ParaEscolher.
    //
    // ⚠️ Desligar NÃO apaga e NÃO desvincula: quem já marcou aquele clube continua marcado, o
    // torneio que aconteceu lá continua sendo lá. Some só da lista de quem vai escolher agora.
    //
    // ⚠️ Nasce LIGADO, e a migration precisa dizer isso com `defaultValue: true` escrito à mão —
    // coluna bool nova do EF nasce `false` no banco, e o padrão em C# só vale pra objeto NOVO.
    // Sem esse cuidado, TODOS os clubes que já existiam sumiriam das listas no primeiro deploy.
    public bool Selecionavel { get; set; } = true;

    // "Marcar Jogo" — dono/admin ativa pra permitir que o Padelizou administre a agenda de
    // quadras do clube (ver QuadraClube/HorarioMarcacaoDisponivel/MarcacaoJogo).
    public bool MarcacaoHorariosAtiva { get; set; }
    public bool NotificarHorariosDiariamente { get; set; }

    // ---- Política de cancelamento / no-show ----
    // Mesma ideia da política do professor: horas mínimas de aviso e se cobra quem não
    // avisa. 0 = pode desmarcar até a última hora.
    public int HorasMinimasCancelamento { get; set; } = 12;
    public bool CobraNoShow { get; set; }
    public string? PoliticaCancelamentoTexto { get; set; }

    // ---- Dados fiscais (ver Services/DadosFiscaisDoClube e FISCAL.md) ----
    //
    // Quem emite a nota é o CLUBE, não o Padelizou. Estes campos são a identidade dele
    // perante a SEFAZ e a prefeitura — sem eles nenhuma nota sai, com eles errados a nota
    // sai errada. Todos NULÁVEIS: o clube que só usa torneio e ranking nunca preenche nada
    // disso, e obrigar seria cobrar CNPJ de quem só quer marcar um jogo.
    //
    // ⚠️ O Padelizou NÃO guarda certificado digital. O A1 sobe direto pro provedor de
    // emissão, que já o armazena cifrado e é auditado pra isso — guardar chave privada de
    // cliente aqui seria assumir um risco (e uma responsabilidade de LGPD) que não é nosso
    // negócio, em troca de zero vantagem. O que fica aqui é só o retrato de "está
    // configurado lá?" (ver CertificadoConfiguradoEm).

    // Só dígitos, como o CPF do jogador. Validado com dígito verificador na gravação.
    public string? Cnpj { get; set; }

    // O nome de registro ("Bonamigo Padel Ltda"), que é o que vai na nota — diferente do
    // Nome, que é como a quadra é conhecida ("Chakra Padel") e é o que aparece no app.
    public string? RazaoSocial { get; set; }

    // Inscrição estadual: exigida pra NFC-e (venda de mercadoria no bar). "ISENTO" é valor
    // legítimo em alguns casos, então é texto, não número.
    public string? InscricaoEstadual { get; set; }

    // Inscrição municipal: exigida pra NFS-e (aula, reserva de quadra, mensalidade).
    public string? InscricaoMunicipal { get; set; }

    // CRT — 1 Simples Nacional, 2 Simples com excesso de sublimite, 3 Regime Normal.
    // É ele que decide se o item da nota leva CSOSN (Simples) ou CST (Normal); errar aqui
    // erra TODAS as notas de uma vez, e não uma.
    public int? RegimeTributario { get; set; }

    // ---- Endereço fiscal ----
    // O `Endereco` lá em cima é uma linha livre pro jogador achar a quadra ("ao lado do
    // posto"); a nota precisa dos campos separados. São dois endereços porque são duas
    // perguntas diferentes — juntar os dois faria a nota depender de alguém ter digitado
    // bonito o texto que serve pro Waze.
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? NumeroEndereco { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }

    // Código IBGE do município (7 dígitos), obrigatório em NFC-e e NFS-e. Vem preenchido do
    // ViaCEP junto com o resto do endereço (ver Services/ConsultaDeCep) — pedir pro dono do
    // clube descobrir o código IBGE da própria cidade seria o fim do cadastro ali mesmo.
    public string? MunicipioIbge { get; set; }
    public string? Uf { get; set; }

    // Quando o certificado A1 foi enviado ao provedor de emissão. Null = ainda não subiu, e
    // é o que a tela usa pra dizer "falta o certificado". A chave em si nunca passa por aqui.
    public DateTime? CertificadoConfiguradoEm { get; set; }

    // Relacionamentos
    public ICollection<Torneio> Torneios { get; set; } = new List<Torneio>();
    // Times que têm sede aqui. Vira lista de vínculos (e não de times) desde que a sede
    // deixou de ser uma coluna no Time — ver Models/TimeSede.
    public ICollection<TimeSede> TimesComSedeAqui { get; set; } = new List<TimeSede>();
}