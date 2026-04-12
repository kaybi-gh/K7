using System.CommandLine;
using K7.Import.Commands;

var rootCommand = ImportCommand.CreateRoot();

var config = new CommandLineConfiguration(rootCommand);
return await config.InvokeAsync(args);
