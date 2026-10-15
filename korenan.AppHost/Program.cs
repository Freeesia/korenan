var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithDataVolume()
    .WithPersistence();

var apiService = builder.AddProject<Projects.korenan_ApiService>("apiservice")
    .WithReference(cache)
    .WaitFor(cache);

builder.AddViteApp("react", "../korenan.react.client")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithExternalHttpEndpoints();

builder.Build().Run();
