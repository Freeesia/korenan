var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.korenan_ApiService>("apiservice");

builder.AddProject<Projects.korenan_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

var weatherApi = builder.AddProject<Projects.korenan_react_Server>("korenan-react-server");

builder.AddNpmApp("react", "../korenan.react/korenan.react.client", "dev")
    .WithReference(weatherApi)
    .WaitFor(weatherApi)
    .WithEnvironment("BROWSER", "none") // Disable opening browser on npm start
    .WithHttpsEndpoint(env: "VITE_PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
