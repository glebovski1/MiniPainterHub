using MiniPainterHub.Server.Exceptions;
using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Services
{
    internal static class ViewerMarkValidation
    {
        public const int CoordinateScale = 6;
        private const int MaxAuthorTagLength = 64;
        private const int MaxAuthorMessageLength = 1000;

        public static (decimal NormalizedX, decimal NormalizedY) NormalizeCoordinates(
            decimal normalizedX,
            decimal normalizedY,
            string xFieldName = "NormalizedX",
            string yFieldName = "NormalizedY")
        {
            var errors = new Dictionary<string, string[]>();

            if (normalizedX < 0m || normalizedX > 1m)
            {
                errors[xFieldName] = new[] { "X coordinate must be between 0 and 1." };
            }

            if (normalizedY < 0m || normalizedY > 1m)
            {
                errors[yFieldName] = new[] { "Y coordinate must be between 0 and 1." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Viewer mark data is invalid.", errors);
            }

            return (
                Math.Round(normalizedX, CoordinateScale, MidpointRounding.AwayFromZero),
                Math.Round(normalizedY, CoordinateScale, MidpointRounding.AwayFromZero));
        }

        public static (string? Tag, string? Message) NormalizeAuthorContent(string? tag, string? message)
        {
            var normalizedTag = string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();
            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
            var errors = new Dictionary<string, string[]>();

            if (normalizedTag is null && normalizedMessage is null)
            {
                errors["Mark"] = new[] { "Author marks require a tag and/or message." };
            }

            if (!string.IsNullOrEmpty(normalizedTag) && normalizedTag.Length > MaxAuthorTagLength)
            {
                errors["Tag"] = new[] { $"Tag must be {MaxAuthorTagLength} characters or fewer." };
            }

            if (!string.IsNullOrEmpty(normalizedMessage) && normalizedMessage.Length > MaxAuthorMessageLength)
            {
                errors["Message"] = new[] { $"Message must be {MaxAuthorMessageLength} characters or fewer." };
            }

            if (errors.Count > 0)
            {
                throw new DomainValidationException("Viewer mark data is invalid.", errors);
            }

            return (normalizedTag, normalizedMessage);
        }
    }
}
