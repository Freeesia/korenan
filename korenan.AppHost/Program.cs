var builder = DistributedApplication.CreateBuilder(args);


var cache = builder.AddRedis("cache")
    .WithDataVolume()
    .WithPersistence();

var apiService = builder.AddProject<Projects.korenan_ApiService>("apiservice")
    .WithReference(cache);

builder.AddNpmApp("react", "../korenan.react.client", "dev")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithEnvironment("BROWSER", "none") // Disable opening browser on npm start
    .WithHttpsEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
