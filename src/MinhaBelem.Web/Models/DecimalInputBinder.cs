using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
namespace MinhaBelem.Web.Models;
/// <summary>HTML number inputs submit invariant decimals, including under pt-BR. Never treat '.' as a thousands separator.</summary>
public sealed class DecimalInputBinder:IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value=context.ValueProvider.GetValue(context.ModelName);
        if(value==ValueProviderResult.None)return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName,value);
        var raw=value.FirstValue?.Trim();
        if(string.IsNullOrEmpty(raw)){if(Nullable.GetUnderlyingType(context.ModelType)is not null)context.Result=ModelBindingResult.Success(null);else context.ModelState.TryAddModelError(context.ModelName,"Informe um número.");return Task.CompletedTask;}
        if(raw.Contains(',')&&!raw.Contains('.'))raw=raw.Replace(',','.');
        if(decimal.TryParse(raw,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number))context.Result=ModelBindingResult.Success(number);
        else context.ModelState.TryAddModelError(context.ModelName,"Informe um número decimal válido, sem separador de milhares.");
        return Task.CompletedTask;
    }
}
public sealed class DecimalInputBinderProvider:IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)=>context.Metadata.ModelType==typeof(decimal)||context.Metadata.ModelType==typeof(decimal?)?new DecimalInputBinder():null;
}
