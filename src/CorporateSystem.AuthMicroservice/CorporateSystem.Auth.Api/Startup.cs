﻿using System.Security.Claims;
using System.Text;
using CorporateSystem.Auth.Api.Background.Jobs;
using CorporateSystem.Auth.Api.Background.Services;
using CorporateSystem.Auth.Api.Middlewares;
using CorporateSystem.Auth.Infrastructure;
using CorporateSystem.Auth.Infrastructure.Extensions;
using CorporateSystem.Auth.Infrastructure.Options;
using CorporateSystem.Auth.Kafka.Extensions;
using CorporateSystem.Auth.Kafka.Models;
using CorporateSystem.Auth.Kafka.Options;
using CorporateSystem.Auth.Services.Extensions;
using CorporateSystem.Auth.Services.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Quartz;
using StackExchange.Redis;

namespace CorporateSystem.Auth.Api;

public class Startup
{
    public IConfiguration Configuration { get; }

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }
    
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        
        services.AddAuthInfrastructure();
        services.AddAuthServices();
        services.AddKeyedProduceHandler();
        
        services.Configure<JwtToken>(Configuration.GetSection("JwtToken"));
        services.Configure<RedisOptions>(Configuration.GetSection("RedisOptions"));
        services.Configure<NotificationOptions>(Configuration.GetSection("NotificationOptions"));
        services.Configure<GrpcNotificationOptions>(Configuration.GetSection("GrpcNotificationOptions"));
        services.Configure<ProducerOptions>($"{nameof(UserDeleteEvent)}", Configuration.GetSection("ProducerOptions"));
        
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = Configuration["RedisOptions:ConnectionString"];
        });
        
        var connectionString = Configuration.GetConnectionString("DefaultConnection");
        services.AddDbContext<DataContext>(options =>
            options.UseNpgsql(connectionString));
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(Configuration.GetSection("JwtToken")["JwtSecret"]!)),
                RoleClaimType = ClaimTypes.Role,
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddHostedService<OutboxEventBackgroundService>();
        services.AddQuartz(config =>
        {
            var jobKey = new JobKey("ClearExpiredRefreshTokensJob");
            config.AddJob<ClearExpiredRefreshTokensJob>(opts => opts.WithIdentity(jobKey));
            
            config.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("ClearExpiredRefreshTokensTrigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(12)
                    .RepeatForever()));
        });
        
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
    }
    
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Program> logger)
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<DataContext>();
                context.Database.Migrate();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка возникла при попытке запуска транзакций");
            }
        }

        app.UseMiddleware<ExceptionMiddleware>();
        
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}