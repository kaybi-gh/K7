# Contribruting guide

## Build

Run `dotnet build -tl` to build the solution.

## Run

To run the web application:

```bash
cd .\src\Web\
dotnet watch run
```

Navigate to https://localhost:5001. The application will automatically reload if you change any of the source files.

## Code Styles & Formatting

The template includes [EditorConfig](https://editorconfig.org/) support to help maintain consistent coding styles for multiple developers working on the same project across various editors and IDEs. The **.editorconfig** file defines the coding styles applicable to this solution.

## Test

The solution contains unit, integration, and functional tests.

To run the tests:
```bash
dotnet test
```

## Generate migrations

Run these commands from the root repository folder:

### Postgres
```bash
dotnet ef migrations add InitialCreate --project ./src/Server/Infrastructure/DatabaseProviders/Postgres --startup-project ./src/Server/Web -- --Database:Provider Postgres
```
### Sqlite
```bash
dotnet ef migrations add InitialCreate --project ./src/Server/Infrastructure/DatabaseProviders/Sqlite --startup-project ./src/Server/Web -- --Database:Provider Sqlite

```
