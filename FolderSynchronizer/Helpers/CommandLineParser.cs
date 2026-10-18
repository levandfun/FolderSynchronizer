
using CommandLine;
using FolderSynchronizer.Models;


namespace FolderSynchronizer.Helpers;


public static class CommandLineParser
{
    public static CommandLineOptions? ParseArguments(string[] args)
    {
        CommandLineOptions? options = null;
        _ = Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(opts => options = opts);
        ValidateArguments(ref options);
        return options;
    }
    private static T GetDefaultValue<T>(string propertyName)
    {
        OptionAttribute? optionAttribute = typeof(CommandLineOptions).GetProperty(propertyName)?.GetCustomAttributes(typeof(OptionAttribute), true).FirstOrDefault() as OptionAttribute;
        if (optionAttribute is null || optionAttribute.Default is null || !typeof(T).IsAssignableFrom(optionAttribute.Default.GetType()))
            throw new Exception($"Property {propertyName} does not have default option attribute");
        return (T)optionAttribute.Default;
    }
    private static void ValidateArguments(ref CommandLineOptions? options)
    {

        if (options == null)
            return;
        CommandLineOptions newOptions = new CommandLineOptions()
        {
            LogFilePath = options.LogFilePath,
            Comparator = options.Comparator,
            ReplicaPath = options.ReplicaPath,
            SourcePath = options.SourcePath,
            MaxParallelThreads = options.MaxParallelThreads <= 0 ? GetDefaultValue<int>(nameof(CommandLineOptions.MaxParallelThreads)) : options.MaxParallelThreads,
            MaxRetryAttempts = options.MaxRetryAttempts <= 0 ? GetDefaultValue<int>(nameof(CommandLineOptions.MaxRetryAttempts)) : options.MaxRetryAttempts,
            RetryDelayMilliseconds = options.RetryDelayMilliseconds <= 0 ? GetDefaultValue<int>(nameof(CommandLineOptions.RetryDelayMilliseconds)) : options.RetryDelayMilliseconds,
            SyncInterval = options.SyncInterval <= 0 ? GetDefaultValue<int>(nameof(CommandLineOptions.SyncInterval)) : options.SyncInterval,
        };
        options = newOptions;
    }
}
