using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MiniPainterHub.Common.DTOs
{
    public class PagedResult<T>
    {
        [Required]
        public IEnumerable<T> Items { get; set; }  // the items on this page

        [Required]
        public int TotalCount { get; set; }  // total items across all pages

        [Required]
        public int PageNumber { get; set; }  // 1-based index of this page

        [Required]
        public int PageSize { get; set; }  // how many items per page
        public int TotalPages  // computed: how many pages in total
            => (int)Math.Ceiling(TotalCount / (double)PageSize);

        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
