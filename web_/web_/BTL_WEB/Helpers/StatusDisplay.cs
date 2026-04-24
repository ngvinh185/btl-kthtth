namespace BTL_WEB.Helpers;

public static class StatusDisplay
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Active"] = "Đang hoạt động",
        ["Inactive"] = "Ngừng hoạt động",
        ["Available"] = "Có sẵn",
        ["Pending"] = "Đang chờ",
        ["Adopted"] = "Đã nhận nuôi",
        ["Approved"] = "Đã duyệt",
        ["Rejected"] = "Từ chối",
        ["Confirmed"] = "Đã xác nhận",
        ["Completed"] = "Hoàn tất",
        ["Cancelled"] = "Đã hủy",
        ["Paid"] = "Đã thanh toán",
        ["Failed"] = "Thất bại",
        ["Refunded"] = "Đã hoàn tiền"
    };

    public static string Label(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Chưa cập nhật";
        }

        return Labels.TryGetValue(status.Trim(), out var label) ? label : status.Trim();
    }

    public static string BadgeClass(string? status)
    {
        var key = status?.Trim().ToLowerInvariant();
        return key switch
        {
            "active" or "available" or "approved" or "confirmed" or "completed" or "paid" => "badge-success",
            "pending" => "badge-warning",
            "inactive" or "cancelled" or "rejected" or "failed" => "badge-danger",
            "adopted" or "refunded" => "badge-info",
            _ => "badge-secondary"
        };
    }
}
