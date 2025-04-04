﻿using System.Text;
using CorporateSystem.Auth.Infrastructure;
using CorporateSystem.Auth.Infrastructure.Extensions;
using CorporateSystem.Auth.Infrastructure.Options;
using CorporateSystem.Auth.Services.Extensions;
using CorporateSystem.Auth.Services.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
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
        
        services.Configure<JwtToken>(Configuration.GetSection("JwtToken"));
        services.Configure<RedisOptions>(Configuration.GetSection("RedisOptions"));
        services.Configure<NotificationOptions>(Configuration.GetSection("NotificationOptions"));
        services.Configure<GrpcNotificationOptions>(Configuration.GetSection("GrpcNotificationOptions"));
        
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var redisOptions = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            return ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
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
                    Encoding.UTF8.GetBytes(Configuration.GetSection("JwtToken")["JwtSecret"]!))
            };
        });
        
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
        
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthentication();
        
        app.UseRouting();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}