using MinhaBelem.Core.Abstractions;
namespace MinhaBelem.Core.Policies;
public static class VisitRecommendationService
{
    public static readonly TimeZoneInfo Zone=TimeZoneInfo.FindSystemTimeZoneById("America/Belem");
    public static VisitAssessment Evaluate(VisitSchedule schedule,DateTime whenUtc,DateTime nowUtc,string sector="principal")
    {
        var info=schedule.Info;
        if(info is null||info.ValidUntil<=nowUtc||info.ValidUntil<=whenUtc||info.ConfirmedAt>nowUtc||string.IsNullOrWhiteSpace(info.SourceUrl))return new("unknown","unavailable","Horário não confirmado para a data da visita.");
        var local=TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(whenUtc,DateTimeKind.Utc),Zone);
        if(info.ClosedUntil?.Date>=local.Date)return new("closed","confirmed","Fechamento temporário informado na fonte.");
        VisitHours[] For(DateTime date){var special=schedule.Hours.Where(h=>h.Sector==sector&&h.SpecialDate?.Date==date.Date).ToArray();return special.Length>0?special:schedule.Hours.Where(h=>h.Sector==sector&&h.SpecialDate is null&&h.Weekday==(int)date.DayOfWeek).ToArray();}
        var today=For(local.Date);
        if(today.Length==0)return new("unknown","unavailable","Horário do setor não confirmado para este dia.");
        if(today.Any(h=>h.SpecialDate is not null&&h.IsClosed))return new("closed","confirmed","Fechado nesta data por exceção de calendário.");
        if(info.LastEntryMinutes is {} last&&local.TimeOfDay.TotalMinutes>=last)return new("closed","confirmed","Horário de última entrada ultrapassado. Confirme as condições na fonte.");
        var windows=new List<(DateTime Start,DateTime End)>();
        for(var offset=-1;offset<=7;offset++)foreach(var h in For(local.Date.AddDays(offset)))
        {
            if(h.IsClosed)continue;var date=local.Date.AddDays(offset);
            if(h.Is24h)windows.Add((date,date.AddDays(1)));
            else if(h.OpenMinute is {} start&&h.CloseMinute is {} end)windows.Add((date.AddMinutes(start),date.AddMinutes(end>start?end:end+1440)));
        }
        var active=windows.Where(w=>w.Start<=local&&w.End>local).OrderByDescending(w=>w.End).FirstOrDefault();
        if(active!=default)return new("open","confirmed",info.ReservationOnly?"Aberto conforme a fonte, somente com reserva.":"Aberto conforme o horário confirmado.",TimeZoneInfo.ConvertTimeToUtc(active.End,Zone));
        var next=windows.Where(w=>w.Start>local).OrderBy(w=>w.Start).FirstOrDefault();
        return new("closed","confirmed","Fechado no horário escolhido. A recomendação considera apenas os horários confirmados; não há previsão autorizada de lotação.",next==default?null:TimeZoneInfo.ConvertTimeToUtc(next.Start,Zone));
    }
    public static (decimal KnownSubtotal,decimal? Total) Cost(decimal? outbound,decimal? entry,decimal? inbound,decimal optional)
    {
        if(outbound<0||entry<0||inbound<0||optional<0)throw new ArgumentException("Custos não podem ser negativos.");
        var known=(outbound??0)+(entry??0)+(inbound??0)+optional;
        return (known,outbound.HasValue&&entry.HasValue&&inbound.HasValue?known:null);
    }
}
