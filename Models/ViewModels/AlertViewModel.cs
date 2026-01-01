namespace MessManagement.Models.ViewModels
{
    public class AlertViewModel
    {
        public string Id { get; set; } = "alert-" + Guid.NewGuid().ToString("N")[..8];
        public string Type { get; set; } = "info"; // success, error, warning, info
        public string? Title { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool Dismissible { get; set; } = true;
    }
}
