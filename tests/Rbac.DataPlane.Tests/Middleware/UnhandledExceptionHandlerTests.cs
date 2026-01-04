using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Rbac.DataPlane;
using Rbac.DataPlane.Repositories;
using Rbac.DataPlane.Services;
using Rbac.Shared.CosmosDb;

namespace Rbac.DataPlane.Tests.Middleware;

public class UnhandledExceptionHandlerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UnhandledExceptionHandlerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Remove existing registrations
                var descriptors = services.Where(d => 
                    d.ServiceType == typeof(ICosmosDbClient) ||
                    d.ServiceType == typeof(IRoleAssignmentRepository) ||
                    d.ServiceType == typeof(IRoleDefinitionRepository) ||
                    d.ServiceType == typeof(ICheckAccessService) ||
                    d.ServiceType == typeof(IRoleDefinitionCache)).ToList();
                
                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                // Add mocks
                services.AddSingleton(Mock.Of<ICosmosDbClient>());
                services.AddScoped(sp => Mock.Of<IRoleAssignmentRepository>());
                services.AddScoped(sp => Mock.Of<IRoleDefinitionRepository>());
                services.AddSingleton(sp => Mock.Of<IRoleDefinitionCache>());
                services.AddScoped(sp => Mock.Of<ICheckAccessService>());
            });
        });
    }

    [Fact]
    public async Task Request_WithUncaughtException_ReturnsInternalServerError()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.Configure(app =>
            {
                app.UseMiddleware<Rbac.DataPlane.Middleware.UnhandledExceptionHandler>();
                app.Run(context =>
                {
                    if (context.Request.Headers.ContainsKey("X-Throw-Generic"))
                    {
                        throw new Exception("Test Generic Exception");
                    }
                    return Task.CompletedTask;
                });
            });
        }).CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Throw-Generic", "true");
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("InternalServerError");
    }

    [Fact]
    public async Task Request_WithArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.Configure(app =>
            {
               app.UseMiddleware<Rbac.DataPlane.Middleware.UnhandledExceptionHandler>();
               app.Run(context => throw new ArgumentException("Invalid argument"));
            });
        }).CreateClient();

        // Act
        var response = await client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("BadRequest");
        content.Should().Contain("Invalid argument");
    }
}
