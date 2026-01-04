using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Rbac.DataPlane;
using Rbac.DataPlane.Diagnostics;
using Rbac.DataPlane.Repositories;
using Rbac.DataPlane.Services;
using Rbac.Shared.CosmosDb;

namespace Rbac.DataPlane.Tests.Middleware;

public class DiagnosticsMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DiagnosticsMiddlewareTests(WebApplicationFactory<Program> factory)
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

                // Add mocks to avoid startup errors
                services.AddSingleton(Mock.Of<ICosmosDbClient>());
                services.AddScoped(sp => Mock.Of<IRoleAssignmentRepository>());
                services.AddScoped(sp => Mock.Of<IRoleDefinitionRepository>());
                services.AddSingleton(sp => Mock.Of<IRoleDefinitionCache>());
                services.AddScoped(sp => Mock.Of<ICheckAccessService>());
            });
        });
    }

    [Fact]
    public async Task Request_GeneratesRequestId_AndLogsSummary()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<Rbac.DataPlane.Middleware.DiagnosticsMiddleware>>();
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                 services.AddSingleton(mockLogger.Object);
            });
             builder.Configure(app =>
            {
                app.UseMiddleware<Rbac.DataPlane.Middleware.DiagnosticsMiddleware>();
                app.Run(async context =>
                {
                    // Verify context is set
                    DiagnosticsContext.Current.RequestId.Should().NotBeNullOrEmpty();
                    await context.Response.WriteAsync("OK");
                });
            });
        }).CreateClient(); 
        
        // Act
        var response = await client.GetAsync("/");
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify Header
        response.Headers.Contains("X-Request-Id").Should().BeTrue();
        var requestId = response.Headers.GetValues("X-Request-Id").FirstOrDefault();
        requestId.Should().NotBeNullOrEmpty();

        // Verify Log
        // Note: verifying extension methods on Logger is tricky with Moq.
        // We look for a call to Log with LogLevel.Information
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("completed in") && o.ToString()!.Contains("RequestId")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Request_UsesExistingRequestId_IfProvided()
    {
         // Arrange
        var existingRequestId = "existing-req-id-123";
        var client = _factory.WithWebHostBuilder(builder =>
        {
             builder.Configure(app =>
            {
                app.UseMiddleware<Rbac.DataPlane.Middleware.DiagnosticsMiddleware>();
                app.Run(async context =>
                {
                    // Verify context matches header
                    DiagnosticsContext.Current.RequestId.Should().Be(existingRequestId);
                    await context.Response.WriteAsync("OK");
                });
            });
        }).CreateClient();

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Request-Id", existingRequestId);
        var response = await client.SendAsync(request);

        // Assert
        response.Headers.GetValues("X-Request-Id").FirstOrDefault().Should().Be(existingRequestId);
    }
}
