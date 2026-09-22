using Caygnus.ResumableConversation.Api.Data;
using Microsoft.EntityFrameworkCore;
using Caygnus.ResumableConversation.Api.Services;

namespace Caygnus.ResumableConversation.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddControllers();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                builder.Configuration.GetConnectionString("DefaultConnection")));

            // Register the IFakeResponseGenerator services
            builder.Services.AddSingleton<IFakeResponseGenerator, FakeResponseGenerator>();
            // 
            builder.Services.AddSingleton<EventStreamBroker>();

            builder.Services.AddScoped<RunProcessor>();

            // Register the RunRecoveryService as a hosted service
            builder.Services.AddHostedService<RunRecoveryService>();

            // Configure CORS to allow requests from the Angular client
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AngularClient", policy =>
                {
                    policy
                        .WithOrigins("http://localhost:4200")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });


            var app = builder.Build();

            app.UseCors("AngularClient");

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
