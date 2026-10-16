namespace MinhaBelem.Web.Models;
public static class SourceStatusLabels
{
    public static string Label(string? status) => status switch
    {
        "Active" => "Consulta concluída",
        "Error" => "Erro",
        "AwaitingConfiguration" => "Aguardando configuração",
        "QuotaReached" => "Limite diário atingido",
        "Unauthorized" => "Não autorizada",
        _ => "Aguardando agendamento"
    };
}
