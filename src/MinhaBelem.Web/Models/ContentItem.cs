namespace MinhaBelem.Web.Models;
public sealed class ContentItem {
 public long Id {get;set;} public string Category {get;set;}=""; public string Title {get;set;}=""; public string Summary {get;set;}="";
 public string? Neighborhood {get;set;} public string SourceName {get;set;}=""; public string SourceUrl {get;set;}="";
 public string? ImageUrl {get;set;} public string? AuthorName {get;set;} public DateTime? EventAt {get;set;}
 public DateTime PublishedAt {get;set;} public DateTime LastVerifiedAt {get;set;}
 public string VerificationStatus {get;set;}="pending";public decimal? TicketPrice {get;set;} public string? Address {get;set;}
}