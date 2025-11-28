using FuzzySharp;

namespace WebVideo.Utility
{
    public static class FuzzySearchHelper
    {
        // Hàm tính điểm Fuzzy Search dựa trên so sánh chuỗi
        public static double CalculateFuzzyScore(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
                return 0;

            // Chuyển về chữ thường để so sánh không phân biệt chữ hoa/thường
            string s1 = source.ToLowerInvariant();
            string s2 = target.ToLowerInvariant();

            // 1. Dùng TokenSetRatio (Tốt cho đảo thứ tự từ khóa)
            double scoreTokenSet = Fuzz.TokenSetRatio(s1, s2);

            // 2. Dùng Ratio (Tốt cho lỗi chính tả/typos nhỏ)
            // Hoặc Fuzz.PartialRatio nếu bạn muốn tìm kiếm cụm từ con bị gõ sai trong chuỗi dài
            double scoreRatio = Fuzz.Ratio(s1, s2);

            // Lấy điểm số cao nhất trong hai trường hợp
            return Math.Max(scoreTokenSet, scoreRatio);
        }
    }
}
