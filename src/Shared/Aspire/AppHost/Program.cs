using Projects;

var builder = DistributedApplication.CreateBuilder(args);


var server = builder.AddProject<K7_Server_Web>("K7Server");
builder.AddProject<K7_Clients_Web>("K7WebClient")
    .WithReference(server);


builder.Build().Run();
