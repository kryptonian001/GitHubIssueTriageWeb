using GithubIssueTriageShared.Models.Github;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using Azure.Messaging.ServiceBus;

namespace GithubIssueTriageWeb
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Configuration.AddUserSecrets<Program>()
                                 .AddEnvironmentVariables();

            builder.Services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var connectionString = config["ServiceBus:ConnectionString"]
                    ?? throw new InvalidOperationException("Missing ServiceBus connection string.");

                return new ServiceBusClient(connectionString);
            });


            builder.Services.AddSwaggerGen(C =>
            {
                C.SwaggerDoc("v1", new OpenApiInfo
                {
                    Version = "v1",
                    Title = "Gihub Issue Triage",
                    Description = "An ASP.NET Core Web API for triaging github issues for support."

                });
            });

            builder.Services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var queueName = config["ServiceBus:QueueName"]
                    ?? throw new InvalidOperationException("Missing ServiceBus queue name.");

                var client = sp.GetRequiredService<ServiceBusClient>();
                return client.CreateSender(queueName);
            });

            builder.Services.AddOpenApi();
            //builder.Services.AddAuthorization();


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gihub Issue Triage API v1"));

            app.MapPost("/issue", async ([FromBody] GithubIssue payload, HttpRequest request, ServiceBusSender sender, CancellationToken cancellationToken) =>
            {
                var jsonPaylod = JsonConvert.SerializeObject(payload);
                var message = new ServiceBusMessage(jsonPaylod)
                {
                    ContentType = "application/json",
                    Subject = "github-webhook"
                };


                if (request.Headers.TryGetValue("X-GetiHub-Event", out var githubEvent))
                {
                    message.ApplicationProperties["GitHubEvent"] = githubEvent.ToString();
                }

                if (request.Headers.TryGetValue("X-GitHub-Delivery", out var deliveryId))
                {
                    message.MessageId = deliveryId.ToString();
                    message.ApplicationProperties["GitHubDeliveryId"] = deliveryId.ToString();
                }


                await sender.SendMessageAsync(message, cancellationToken);

                return Results.Accepted();
            });

            //app.UseAuthorization();

            app.Run();
        }
    }
}

