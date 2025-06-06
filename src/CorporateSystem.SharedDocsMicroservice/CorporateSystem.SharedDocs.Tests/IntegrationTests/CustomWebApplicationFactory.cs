﻿using CorporateSystem.SharedDocs.Api.Hubs;
using CorporateSystem.SharedDocs.Services.Handlers;
using CorporateSystem.SharedDocs.Services.Services.Implementations;
using CorporateSystem.SharedDocs.Services.Services.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace CorporateSystem.SharedDocs.Tests.IntegrationTests;

public class CustomWebApplicationFactory<TEntryPoint> 
    : WebApplicationFactory<TEntryPoint>, IAsyncLifetime where TEntryPoint : class
{
    private readonly PostgresContainer _postgresContainer = new();
    
    public Mock<IDocumentService> MockDocumentService { get; } = new();
    public Mock<IAuthApiService> MockAuthApiService { get; } = new();
    public Mock<IDocumentChangeLogService> MockDocumentChangeLogService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "PostgresOptions:ConnectionString", _postgresContainer.ConnectionString }
            });
        });

        builder.ConfigureServices(services =>
        {
            services.Replace(new ServiceDescriptor(typeof(IDocumentService), MockDocumentService.Object));
            services.Replace(new ServiceDescriptor(typeof(IAuthApiService), MockAuthApiService.Object));
            services.Replace(new ServiceDescriptor(typeof(IDocumentChangeLogService), MockDocumentChangeLogService.Object));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.InitializeAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
        await base.DisposeAsync();
    }

    public void ResetMocks()
    {
        MockDocumentService.Invocations.Clear();
        MockAuthApiService.Invocations.Clear();
        MockDocumentChangeLogService.Invocations.Clear();
    }
}