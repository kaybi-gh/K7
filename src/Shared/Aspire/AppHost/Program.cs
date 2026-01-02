using Projects;

var builder = DistributedApplication.CreateBuilder(args);


var server = builder.AddProject<K7_Server_Web>("k7-server")
    .WithArgs("--init-db");
    
builder.Build().Run();
