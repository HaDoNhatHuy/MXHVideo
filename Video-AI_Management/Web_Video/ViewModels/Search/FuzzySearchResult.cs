using System;

namespace Web_Video.ViewModels.Search
{
    public class FuzzySearchResult
    {
        public Guid VideoId { get; set; }
        public string Title { get; set; }
        public double Score { get; set; } // Điểm số Fuzzy (ví dụ: 0.0 đến 1.0)
    }
}
