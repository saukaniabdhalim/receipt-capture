namespace ReceiptCapture.Api.ViewModels;

public class HouseholdDashboardViewModel
{
    public int HouseholdId { get; set; }
    public string HouseholdName { get; set; } = string.Empty;
    public List<string> Members { get; set; } = new();

    public decimal TodayTotal { get; set; }
    public int TodayCount { get; set; }

    public decimal MonthTotal { get; set; }
    public int MonthCount { get; set; }

    public List<ReceiptViewModel> RecentReceipts { get; set; } = new();
}

public class ReceiptViewModel
{
    public int ReceiptId { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public decimal Amount { get; set; }
    public DateTime? Date { get; set; }
    public string? Category { get; set; }
}