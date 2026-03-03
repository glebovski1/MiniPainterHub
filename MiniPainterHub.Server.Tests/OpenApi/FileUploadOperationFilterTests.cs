using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi.Models;
using MiniPainterHub.Common.DTOs;
using MiniPainterHub.Server.OpenAPIOperationFilter;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace MiniPainterHub.Server.Tests.OpenApi;

public class FileUploadOperationFilterTests
{
    private readonly FileUploadOperationFilter _filter = new();

    [Fact]
    public void Apply_WhenEndpointHasNoFile_DoesNotSetRequestBody()
    {
        var operation = new OpenApiOperation();
        var context = CreateContext(typeof(FakeController).GetMethod(nameof(FakeController.NoFile))!);

        _filter.Apply(operation, context);

        operation.RequestBody.Should().BeNull();
    }

    [Fact]
    public void Apply_WhenEndpointHasIFormFileParameter_SetsMultipartRequestBody()
    {
        var operation = new OpenApiOperation();
        var context = CreateContext(typeof(FakeController).GetMethod(nameof(FakeController.WithDirectFile))!);

        _filter.Apply(operation, context);

        operation.RequestBody.Should().NotBeNull();
        operation.RequestBody!.Content.Should().ContainKey("multipart/form-data");
        var schema = operation.RequestBody.Content["multipart/form-data"].Schema;
        schema.Required.Should().Contain(new[] { "Title", "Content" });
        schema.Properties.Keys.Should().Contain(new[] { "Title", "Content", "image" });
    }

    [Fact]
    public void Apply_WhenEndpointHasDtoWithFormFiles_SetsMultipartRequestBody()
    {
        var operation = new OpenApiOperation();
        var context = CreateContext(typeof(FakeController).GetMethod(nameof(FakeController.WithDto))!);

        _filter.Apply(operation, context);

        operation.RequestBody.Should().NotBeNull();
        operation.RequestBody!.Content.Keys.Should().Contain("multipart/form-data");
    }

    private static OperationFilterContext CreateContext(MethodInfo method)
    {
        var schemaGenerator = new SchemaGenerator(
            new SchemaGeneratorOptions(),
            new JsonSerializerDataContractResolver(new JsonSerializerOptions()));

        return new OperationFilterContext(
            new ApiDescription(),
            schemaGenerator,
            new SchemaRepository(),
            method);
    }

    private sealed class FakeController
    {
        public void NoFile(string title)
        {
        }

        public void WithDirectFile(IFormFile file)
        {
        }

        public void WithDto(CreateImagePostDto dto)
        {
        }
    }
}
