using MinhaBelem.Core.Entities;
namespace MinhaBelem.Core.Abstractions;
public sealed record TouristAnswer(IReadOnlyList<ContentItem> Content,IReadOnlyList<Place> Places,IReadOnlyList<TransitLine> Lines,bool AiRanked);
public interface ITouristAssistant{Task<TouristAnswer> SearchAsync(string question,string culture,bool useAi,CancellationToken ct);}
