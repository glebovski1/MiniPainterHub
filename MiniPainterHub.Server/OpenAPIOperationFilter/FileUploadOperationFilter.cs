using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;
using System.Linq;

namespace MiniPainterHub.Server.OpenAPIOperationFilter
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasFileParam = context.MethodInfo
                .GetParameters()
                .Any(p => p.ParameterType == typeof(IFormFile));

            if (!hasFileParam) return;

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type       = "object",
                        Properties =
                        {
                            ["Title"]   = new OpenApiSchema { Type = "string" },
                            ["Content"] = new OpenApiSchema { Type = "string" },
                            ["image"]   = new OpenApiSchema { Type = "string", Format = "binary" },
                        },
                        Required = new HashSet<string> { "Title", "Content" }
                    }
                }
            }
            };
        }
    }
}
