using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniPainterHub.Common.DTOs
{
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; }  // the items on this page
        public int TotalCount { get; set; }  // total items across all pages
        public int PageNumber { get; set; }  // 1-based index of this page
        public int PageSize { get; set; }  // how many items per page
        public int TotalPages  // computed: how many pages in total
            => (int)Math.Ceiling(TotalCount / (double)PageSize);

        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
