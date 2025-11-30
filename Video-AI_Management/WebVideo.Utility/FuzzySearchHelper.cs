using FuzzySharp;

namespace WebVideo.Utility
{
    public static class FuzzySearchHelper
    {
        // Hàm tính điểm Fuzzy Search dựa trên so sánh chuỗi
        //public static double CalculateFuzzyScore(string source, string target)
        //{
        //    if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
        //        return 0;

        //    // Chuyển về chữ thường để so sánh không phân biệt chữ hoa/thường
        //    string s1 = source.ToLowerInvariant();
        //    string s2 = target.ToLowerInvariant();

        //    // 1. Dùng TokenSetRatio (Tốt cho đảo thứ tự từ khóa)
        //    double scoreTokenSet = Fuzz.TokenSetRatio(s1, s2);

        //    // 2. Dùng Ratio (Tốt cho lỗi chính tả/typos nhỏ)
        //    // Hoặc Fuzz.PartialRatio nếu bạn muốn tìm kiếm cụm từ con bị gõ sai trong chuỗi dài
        //    double scoreRatio = Fuzz.Ratio(s1, s2);

        //    // Lấy điểm số cao nhất trong hai trường hợp
        //    return Math.Max(scoreTokenSet, scoreRatio);
        //}
        public static double CalculateFuzzyScore(string source, string target)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0;

            // WeightedRatio cân bằng giữa các thuật toán khác nhau
            // Nó xử lý tốt cả viết tắt, đảo từ và sai chính tả
            return Fuzz.WeightedRatio(source.ToLowerInvariant(), target.ToLowerInvariant());
        }
    }
}



//================================================
//LOGIC KHÁC
//================================================
//using FuzzySharp;
//using System;
//using System.Linq;
//using System.Text.RegularExpressions;

//namespace WebVideo.Utility
//{
//    public static class FuzzySearchHelper
//    {
//        /// <summary>
//        /// Tính điểm Fuzzy Search nâng cao với nhiều thuật toán
//        /// Xử lý tốt: lỗi chính tả, đảo từ, từ viết tắt, từ đồng nghĩa
//        /// </summary>
//        public static double CalculateFuzzyScore(string source, string target)
//        {
//            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
//                return 0;

//            // Chuẩn hóa chuỗi
//            string s1 = NormalizeString(source);
//            string s2 = NormalizeString(target);

//            // Nếu match chính xác -> 100 điểm
//            if (s1 == s2) return 100;

//            // Nếu s1 chứa s2 hoặc ngược lại -> điểm cao
//            if (s1.Contains(s2) || s2.Contains(s1))
//                return 95;

//            // 1. WeightedRatio - Kết hợp nhiều thuật toán
//            double scoreWeighted = Fuzz.WeightedRatio(s1, s2);

//            // 2. PartialRatio - Tìm cụm từ con khớp
//            double scorePartial = Fuzz.PartialRatio(s1, s2);

//            // 3. TokenSetRatio - Bỏ qua thứ tự từ
//            double scoreTokenSet = Fuzz.TokenSetRatio(s1, s2);

//            // 4. TokenSortRatio - Sắp xếp từ rồi so sánh
//            double scoreTokenSort = Fuzz.TokenSortRatio(s1, s2);

//            // Xử lý đặc biệt cho từ viết tắt / acronyms
//            double scoreAcronym = CheckAcronymMatch(s1, s2);

//            // Trọng số kết hợp các thuật toán
//            double finalScore = Math.Max(
//                scoreWeighted * 0.4,
//                Math.Max(
//                    scoreTokenSet * 0.3,
//                    Math.Max(
//                        scorePartial * 0.2,
//                        Math.Max(scoreTokenSort * 0.1, scoreAcronym)
//                    )
//                )
//            );

//            return finalScore;
//        }

//        /// <summary>
//        /// Chuẩn hóa chuỗi: loại bỏ ký tự đặc biệt, chuyển về lowercase
//        /// </summary>
//        private static string NormalizeString(string input)
//        {
//            if (string.IsNullOrEmpty(input))
//                return string.Empty;

//            // Chuyển về lowercase
//            string normalized = input.ToLowerInvariant();

//            // Loại bỏ dấu tiếng Việt (nếu cần)
//            normalized = RemoveVietnameseTones(normalized);

//            // Loại bỏ ký tự đặc biệt, chỉ giữ chữ cái và số
//            normalized = Regex.Replace(normalized, @"[^a-z0-9\s]", " ");

//            // Loại bỏ khoảng trắng thừa
//            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

//            return normalized;
//        }

//        /// <summary>
//        /// Loại bỏ dấu tiếng Việt
//        /// </summary>
//        private static string RemoveVietnameseTones(string text)
//        {
//            if (string.IsNullOrEmpty(text))
//                return text;

//            string[] vietnameseSigns = new string[]
//            {
//                "aAeEoOuUiIdDyY",
//                "áàạảãâấầậẩẫăắằặẳẵ",
//                "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
//                "éèẹẻẽêếềệểễ",
//                "ÉÈẸẺẼÊẾỀỆỂỄ",
//                "óòọỏõôốồộổỗơớờợởỡ",
//                "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
//                "úùụủũưứừựửữ",
//                "ÚÙỤỦŨƯỨỪỰỬỮ",
//                "íìịỉĩ",
//                "ÍÌỊỈĨ",
//                "đ",
//                "Đ",
//                "ýỳỵỷỹ",
//                "ÝỲỴỶỸ"
//            };

//            for (int i = 1; i < vietnameseSigns.Length; i++)
//            {
//                for (int j = 0; j < vietnameseSigns[i].Length; j++)
//                {
//                    text = text.Replace(vietnameseSigns[i][j], vietnameseSigns[0][i - 1]);
//                }
//            }

//            return text;
//        }

//        /// <summary>
//        /// Kiểm tra match theo từ viết tắt / acronym
//        /// Ví dụ: "HSG" match với "Học Sinh Giỏi"
//        /// </summary>
//        private static double CheckAcronymMatch(string s1, string s2)
//        {
//            try
//            {
//                // Lấy từ viết tắt từ chuỗi dài hơn
//                string longer = s1.Length > s2.Length ? s1 : s2;
//                string shorter = s1.Length > s2.Length ? s2 : s1;

//                // Nếu shorter quá dài thì không phải acronym
//                if (shorter.Length > 10 || shorter.Contains(" "))
//                    return 0;

//                // Lấy chữ cái đầu của mỗi từ trong chuỗi dài
//                var words = longer.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
//                if (words.Length < 2)
//                    return 0;

//                string acronym = string.Concat(words.Select(w => w[0]));

//                // So sánh acronym với shorter
//                if (acronym.Equals(shorter, StringComparison.OrdinalIgnoreCase))
//                    return 90;

//                // Fuzzy match acronym
//                double acronymScore = Fuzz.Ratio(acronym, shorter);
//                return acronymScore > 70 ? acronymScore : 0;
//            }
//            catch
//            {
//                return 0;
//            }
//        }

//        /// <summary>
//        /// Tính điểm tương đồng giữa hai danh sách tags/keywords
//        /// </summary>
//        public static double CalculateTagSimilarity(string[] tags1, string[] tags2)
//        {
//            if (tags1 == null || tags2 == null || tags1.Length == 0 || tags2.Length == 0)
//                return 0;

//            int matchCount = 0;
//            foreach (var tag1 in tags1)
//            {
//                foreach (var tag2 in tags2)
//                {
//                    double score = CalculateFuzzyScore(tag1, tag2);
//                    if (score >= 80)
//                    {
//                        matchCount++;
//                        break;
//                    }
//                }
//            }

//            return (matchCount * 100.0) / Math.Max(tags1.Length, tags2.Length);
//        }
//    }
//}
