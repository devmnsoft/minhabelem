using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace MinhaBelem.Web.Services;
/// <summary>Gateway editorial. Nunca publica automaticamente. Credenciais são obtidas da configuração do servidor.</summary>
public sealed class AiAssistant(IHttpClientFactory factory,IConfiguration cfg){
 public async Task<string?> SuggestAsync(string provider,string sourceText,CancellationToken ct){
  if(sourceText.Length>12000)sourceText=sourceText[..12000];
  var key=cfg[$"AI:{provider}:ApiKey"];
  if(string.IsNullOrWhiteSpace(key))return null;
  string endpoint,model;
  switch(provider.ToLowerInvariant()){
   case "groq":endpoint="https://api.groq.com/openai/v1/chat/completions";model=cfg["AI:groq:Model"]??"llama-3.3-70b-versatile";break;
   case "deepseek":endpoint="https://api.deepseek.com/chat/completions";model=cfg["AI:deepseek:Model"]??"deepseek-chat";break;
   case "gemini":endpoint="https://generativelanguage.googleapis.com/v1beta/openai/chat/completions";model=cfg["AI:gemini:Model"]??"gemini-2.5-flash";break;
   default:throw new ArgumentOutOfRangeException(nameof(provider));
  }
  using var req=new HttpRequestMessage(HttpMethod.Post,endpoint);
  req.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
  req.Content=new StringContent(JsonSerializer.Serialize(new{model,messages=new[]{new{role="system",content="Você é auxiliar editorial do MinhaBelém. Apenas classifique e resuma o texto recebido em português brasileiro. Não invente fatos, não publique, não adicione informação sem fonte."},new{role="user",content=sourceText}},temperature=0.1}),Encoding.UTF8,"application/json");
  using var res=await factory.CreateClient("ai").SendAsync(req,ct);res.EnsureSuccessStatusCode();
  using var doc=JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
  return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
 }
}