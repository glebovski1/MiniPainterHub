using System.Collections.Generic;

namespace MiniPainterHub.Common.DTOs;

public class ClientPerformanceBatchDto
{
    public List<ClientPerformanceMetricDto> Metrics { get; set; } = new();
}
