using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

            // Sử dụng Token Set Ratio, là phương pháp mạnh mẽ hơn cho tìm kiếm (Tối ưu cho việc đảo thứ tự từ)
            // Nếu bạn muốn so sánh chính tả đơn giản hơn, dùng Fuzz.Ratio(s1, s2)
            // Ví dụ này sử dụng Token Set Ratio (FuzzySharp)
            // Nếu bạn không dùng FuzzySharp, phải tự viết thuật toán Levenshtein ở đây.

            // Giả định bạn đã cài FuzzySharp:
            return Fuzz.TokenSetRatio(s1, s2);
        }
    }
}
