var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.korenan_ApiService>("apiservice");

builder.AddProject<Projects.korenan_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
