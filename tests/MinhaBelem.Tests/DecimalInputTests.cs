using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using MinhaBelem.Web.Models;
namespace MinhaBelem.Tests;
public sealed class DecimalInputTests
{
    [Theory][InlineData("1.50","1.50")][InlineData("1,50","1.50")][InlineData("1000","1000")][InlineData("0.5","0.5")]
    public async Task HtmlNumberDecimalsAreNotReadAsThousands(string input,string expected)
    {
        var context=new DefaultModelBindingContext{ModelMetadata=new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal)),ModelName="Price",FieldName="Price",ModelState=new(),ValueProvider=new QueryStringValueProvider(BindingSource.Query,new QueryCollection(new Dictionary<string,StringValues>{{"Price",input}}),CultureInfo.GetCultureInfo("pt-BR"))};
        await new DecimalInputBinder().BindModelAsync(context);
        Assert.Empty(context.ModelState.Values.SelectMany(v=>v.Errors));Assert.True(context.Result.IsModelSet);Assert.Equal(decimal.Parse(expected,CultureInfo.InvariantCulture),context.Result.Model);
    }
    [Theory][InlineData("1.000,50")][InlineData("NaN")][InlineData("R$ 5")]
    public async Task AmbiguousOrNonNumericAmountsAreRejected(string input)
    {
        var context=new DefaultModelBindingContext{ModelMetadata=new EmptyModelMetadataProvider().GetMetadataForType(typeof(decimal)),ModelName="Price",FieldName="Price",ModelState=new(),ValueProvider=new QueryStringValueProvider(BindingSource.Query,new QueryCollection(new Dictionary<string,StringValues>{{"Price",input}}),CultureInfo.GetCultureInfo("pt-BR"))};
        await new DecimalInputBinder().BindModelAsync(context);Assert.False(context.ModelState.IsValid);Assert.False(context.Result.IsModelSet);
    }
}
