namespace MessManagement.Models.ViewModels
{
    public class StatsCardViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = "-";
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "activity";
        public string IconClass { get; set; } = "primary"; // primary, success, warning, info
        public string DescriptionClass { get; set; } = "text-gray-600";
    }
}
