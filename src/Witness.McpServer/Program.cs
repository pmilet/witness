using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamJsonRpc;
using Witness.Application.Interfaces;
using Witness.Application.UseCases;
using Witness.Domain.Repositories;
using Witness.Domain.Services;
using Witness.Infrastructure.Http;
using Witness.Infrastructure.Repositories;
using Witness.McpServer;
using Witness.McpServer.Handlers;

var storagePath = Path.Combine(Directory.GetCurrentDirectory(), "witness-store");

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHttpClient("WitnessHttpClient")
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

builder.Services.AddSingleton<IWitnessIdGenerator, WitnessIdGenerator>();
builder.Services.AddSingleton<IInteractionRepository>(sp => new FileSystemInteractionRepository(storagePath));
builder.Services.AddSingleton<ISessionRepository>(sp => new FileSystemSessionRepository(storagePath));
builder.Services.AddSingleton<IHttpExecutor, HttpExecutor>();

builder.Services.AddSingleton<RecordInteractionUseCase>();
builder.Services.AddSingleton<ReplayInteractionUseCase>();
builder.Services.AddSingleton<InspectInteractionUseCase>();
builder.Services.AddSingleton<ListSessionsUseCase>();
builder.Services.AddSingleton<ListInteractionsUseCase>();

builder.Services.AddSingleton<RecordToolHandler>();
builder.Services.AddSingleton<ReplayToolHandler>();
builder.Services.AddSingleton<InspectToolHandler>();
builder.Services.AddSingleton<ListToolHandler>();

builder.Services.AddSingleton<WitnessMcpServer>();

var host = builder.Build();

var mcpServer = host.Services.GetRequiredService<WitnessMcpServer>();

using var stdin = Console.OpenStandardInput();
using var stdout = Console.OpenStandardOutput();

var jsonRpc = new JsonRpc(new HeaderDelimitedMessageHandler(stdout, stdin))
{
    ExceptionStrategy = ExceptionProcessing.ISerializable
};

jsonRpc.AddLocalRpcTarget(mcpServer, new JsonRpcTargetOptions
{
    AllowNonPublicInvocation = false
});

jsonRpc.StartListening();

await jsonRpc.Completion;
