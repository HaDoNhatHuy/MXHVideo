using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video
{
    public interface IMeta
    {
        public string? MetaKeywords { get; set; }
        public string? MetaDescription { get; set; }
    }
}
