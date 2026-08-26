using K7.Import.Commands;

var rootCommand = ImportCommand.CreateRoot();
return await rootCommand.Parse(args).InvokeAsync();
