using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Database_Video.Pagination
{
    public class HomeParameters : BaseParameters
    {
        private string _searchBy;
        public string SearchBy
        {
            get => _searchBy;
            set => _searchBy = string.IsNullOrEmpty(value) ? "" : value.ToLower();
        }
        public Guid CategoryId { get; set; }
    }
}
