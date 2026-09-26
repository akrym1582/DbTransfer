using CommandLine;

return await Parser.Default.ParseArguments<CopyOptions, ExportOptions, ImportOptions, ExecOptions, InspectOptions, ValidateOptions>(args)
    .MapResult(
        (CopyOptions _) => NotImplementedAsync("copy"),
        (ExportOptions _) => NotImplementedAsync("export"),
        (ImportOptions _) => NotImplementedAsync("import"),
        (ExecOptions _) => NotImplementedAsync("exec"),
        (InspectOptions _) => NotImplementedAsync("inspect"),
        (ValidateOptions _) => NotImplementedAsync("validate"),
        _ => Task.FromResult(2));

static Task<int> NotImplementedAsync(string verb)
{
    Console.Error.WriteLine($"The '{verb}' command is registered but no database connector is installed yet.");
    return Task.FromResult(3);
}
