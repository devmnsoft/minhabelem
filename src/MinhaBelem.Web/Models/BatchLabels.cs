namespace MinhaBelem.Web.Models;
public static class BatchLabels
{
    public static string ActionKey(string action) => action switch
    {
        "approve" => "BatchApprove", "rejectCandidate" or "reject" => "BatchReject",
        "reviewCandidate" or "review" => "BatchReview", "retry" => "BatchRetry", "archive" => "BatchArchive",
        "publish" => "BatchPublish", "category" => "BatchCategory", "locality" => "BatchLocality",
        "eventStatus" => "BatchEventStatus", "deduplicate" => "BatchDeduplicate",
        "analyze" => "BatchAnalyze", "translate" => "TranslateWithAi", _ => "BatchAction"
    };
}
