using System.Collections.Generic;
using FluentAssertions;
using MiniPainterHub.Server.Features.Tags;
using Xunit;

namespace MiniPainterHub.Server.Tests.Features.Tags;

public class TagTextUtilitiesTests
{
    [Theory]
    [InlineData("  Wet   Blending  ", "wet blending")]
    [InlineData("Oil-Wash", "oil-wash")]
    public void NormalizeText_CollapsesWhitespaceAndLowercases(string input, string expected)
    {
        TagTextUtilities.NormalizeText(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Wet Blending", "wet-blending")]
    [InlineData("NMM / Gold", "nmm-gold")]
    [InlineData("edge_highlight", "edge-highlight")]
    public void CreateSlug_UsesLettersDigitsAndHyphenSeparators(string input, string expected)
    {
        TagTextUtilities.CreateSlug(input).Should().Be(expected);
    }

    [Fact]
    public void ResolveUniqueSlug_AppendsFirstAvailableNumericSuffix()
    {
        var used = new HashSet<string>(["glazing", "glazing-2"], System.StringComparer.OrdinalIgnoreCase);

        TagTextUtilities.ResolveUniqueSlug("glazing", used).Should().Be("glazing-3");
    }
}
