using System.Net;
using System.Text;

namespace MinhaBelem.Web.Models;

/// <summary>
/// Gera a marcação de <option> de forma segura para o Razor.
/// O tag helper <c>option</c> do ASP.NET Core proíbe expressões C# na área de
/// atributos, e <c>selected="@(bool)"</c> marca todas as opções como selecionadas
/// (em HTML o atributo é booleano: <c>selected="False"</c> ainda seleciona).
/// </summary>
public static class SelectOptions
{
    /// <summary>
    /// Monta as opções de um <c>select</c> marcando apenas a que corresponde a <paramref name="selectedValue"/>.
    /// </summary>
    public static string Build(IEnumerable<(string Value, string Label)> items, string? selectedValue)
    {
        var builder = new StringBuilder();
        foreach (var (value, label) in items)
        {
            var isSelected = string.Equals(value, selectedValue, StringComparison.Ordinal);
            builder.Append("<option value=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
            if (isSelected) builder.Append(" selected");
            builder.Append('>').Append(WebUtility.HtmlEncode(label)).Append("</option>");
        }
        return builder.ToString();
    }

    /// <summary>
    /// Monta as opções de um <c>select</c> usando uma função de predicação por item.
    /// </summary>
    public static string Build(IEnumerable<(string Value, string Label)> items, Func<string, bool> isSelected)
    {
        var builder = new StringBuilder();
        foreach (var (value, label) in items)
        {
            builder.Append("<option value=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
            if (isSelected(value)) builder.Append(" selected");
            builder.Append('>').Append(WebUtility.HtmlEncode(label)).Append("</option>");
        }
        return builder.ToString();
    }

    /// <summary>
    /// Monta um <c>input type="checkbox"</c> com o atributo <c>checked</c> presente
    /// apenas quando verdadeiro (o atributo é booleano em HTML).
    /// </summary>
    public static string Checkbox(string name, string value, bool isChecked, string? cssClass = null, string? id = null)
    {
        var builder = new StringBuilder();
        builder.Append("<input type=\"checkbox\" name=\"").Append(WebUtility.HtmlEncode(name))
               .Append("\" value=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
        if (isChecked) builder.Append(" checked");
        if (!string.IsNullOrWhiteSpace(cssClass)) builder.Append(" class=\"").Append(WebUtility.HtmlEncode(cssClass)).Append('"');
        if (!string.IsNullOrWhiteSpace(id)) builder.Append(" id=\"").Append(WebUtility.HtmlEncode(id)).Append('"');
        builder.Append(" />");
        return builder.ToString();
    }
}
