using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var pgPassword = builder.AddParameter("pg-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: pgPassword)
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithHostPort(5432)
    .WithPgAdmin(pgAdmin => pgAdmin
        .WithHostPort(5050)
        .WithLifetime(ContainerLifetime.Persistent));

var database = postgres.AddDatabase("k7");

var server = builder.AddProject<K7_Server_Web>("k7-server")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment(context =>
    {
        context.EnvironmentVariables["Database__Provider"] = "Postgres";
        context.EnvironmentVariables["Database__UserID"] = postgres.Resource.UserNameParameter!;
        context.EnvironmentVariables["Database__Password"] = postgres.Resource.PasswordParameter!;
        context.EnvironmentVariables["Database__Server"] = postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Host)!;
        context.EnvironmentVariables["Database__Port"] = postgres.Resource.PrimaryEndpoint.Property(EndpointProperty.Port)!;
        context.EnvironmentVariables["Database__Name"] = database.Resource.DatabaseName!;
        context.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = builder.Environment.EnvironmentName;
    })
    .WithArgs("--init-db");
    
builder.Build().Run();
