using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;

namespace Padelizou.Services;

// Como o sistema lê um valor em dinheiro que veio de um formulário.
//
// POR QUE ISTO EXISTE: a cultura do app é pt-BR (Program.cs), onde "." é separador de MILHAR.
// Só que todo campo de dinheiro da tela é <input type="number">, e o navegador manda o valor
// sempre no formato da máquina — "79.90" — mesmo quando mostra "79,90" pro usuário. O binder
// padrão do ASP.NET Core lê os campos de formulário na cultura da requisição, então
// decimal.Parse("79.90", pt-BR) devolvia **7990**: uma inscrição de R$ 79,90 virava
// R$ 7.990,00 sem erro nenhum. Passou despercebido porque os preços usados até agora eram
// redondos (120, 150) — sem centavos não há separador, e o bug não aparece.
//
// A REGRA, na ordem:
//   1. vazio continua vazio (campo opcional segue opcional);
//   2. tira "R$" e qualquer tipo de espaço;
//   3. tendo VÍRGULA e PONTO, quem vier por último é o decimal — o outro é milhar e sai fora
//      ("1.234,56" e "1,234.56" caem os dois em 1234.56);
//   4. tendo só um deles, ele é o decimal. É por isso que "1.500" vira 1,5 e não 1500: o
//      <input type="number"> NUNCA manda separador de milhar, então um ponto sozinho só pode
//      ser vírgula decimal.
//
// Vale pra decimal e decimal? — dinheiro. Os outros números do sistema são inteiros e não têm
// separador nenhum, então não passam por aqui.
public class DinheiroModelBinder : IModelBinder
{
    private readonly IModelBinder _padrao;

    public DinheiroModelBinder(IModelBinder padrao) => _padrao = padrao;

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valor = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valor == ValueProviderResult.None) return _padrao.BindModelAsync(bindingContext);

        var texto = valor.FirstValue;
        if (string.IsNullOrWhiteSpace(texto))
        {
            // Campo em branco: nulável vira null; não-nulável fica com o erro do binder padrão.
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
            {
                bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valor);
                bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }
            return _padrao.BindModelAsync(bindingContext);
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valor);

        if (Ler(texto) is decimal lido)
        {
            bindingContext.Result = ModelBindingResult.Success(lido);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.TryAddModelError(
            bindingContext.ModelName, "Informe um valor válido, como 79,90.");
        return Task.CompletedTask;
    }

    // Pública porque é a regra em si: os testes falam com ela direto, sem subir o pipeline de
    // model binding inteiro pra conferir uma conta de vírgula.
    public static decimal? Ler(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;

        // Fora "R$", tira TODO tipo de espaço: teclado de celular manda espaço não-separável
        // e espaço fino no lugar do comum, e nenhum dos dois some com Trim().
        var semMoeda = texto.Replace("R$", "", StringComparison.OrdinalIgnoreCase);
        var limpo = new string(semMoeda.Where(c => !char.IsWhiteSpace(c)).ToArray());

        if (limpo.Length == 0) return null;

        var ultimaVirgula = limpo.LastIndexOf(',');
        var ultimoPonto = limpo.LastIndexOf('.');

        if (ultimaVirgula >= 0 && ultimoPonto >= 0)
        {
            // Os dois presentes: o último manda, o outro era milhar.
            limpo = ultimaVirgula > ultimoPonto
                ? limpo.Replace(".", "").Replace(',', '.')
                : limpo.Replace(",", "");
        }
        else if (ultimaVirgula >= 0)
        {
            limpo = limpo.Replace(',', '.');
        }

        return decimal.TryParse(limpo, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }
}

// Entra na frente do binder padrão só pra decimal/decimal?; todo o resto segue igual.
public class DinheiroModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tipo = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        if (tipo != typeof(decimal)) return null;

        // O binder padrão segue como plano B (campo ausente, campo vazio obrigatório):
        // reimplementar as mensagens de erro dele aqui só criaria duas versões da mesma coisa.
        return new DinheiroModelBinder(new SimpleTypeModelBinder(
            context.Metadata.ModelType,
            context.Services.GetRequiredService<ILoggerFactory>()));
    }
}
