using System;

namespace MiniPainterHub.Server.Entities
{
    public class FeedPolicy
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public double WRecency { get; set; }
        public double WLikes { get; set; }
        public double WComments { get; set; }
        public double WReportsPenalty { get; set; }
        public double HalfLifeHours { get; set; }
        public bool DiversityByAuthor { get; set; }
        public int MaxPerAuthorPerPage { get; set; }
        public bool ExcludeNSFW { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
