using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MiniPainterHub.Server.OpenAPIOperationFilter
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasFileParam = context.MethodInfo
                .GetParameters()
                .Any(p => IsFileOrContainsFiles(p.ParameterType));

            if (!hasFileParam)
            {
                return;
            }

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties =
                            {
                                ["Title"] = new OpenApiSchema { Type = "string" },
                                ["Content"] = new OpenApiSchema { Type = "string" },
                                ["image"] = new OpenApiSchema { Type = "string", Format = "binary" },
                            },
                            Required = new HashSet<string> { "Title", "Content" }
                        }
                    }
                }
            };
        }

        private static bool IsFileOrContainsFiles(Type type)
        {
            if (type == typeof(IFormFile) || type == typeof(IFormFileCollection))
            {
                return true;
            }

            if (type.GetInterfaces().Any(i =>
                    i.IsGenericType
                    && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    && i.GetGenericArguments()[0] == typeof(IFormFile)))
            {
                return true;
            }

            return type.GetProperties().Any(p => IsFileOrContainsFiles(p.PropertyType));
        }
    }
}
