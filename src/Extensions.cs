namespace Qs.EventGrid.Emulator;

public static class Extensions
{
    public static IServiceCollection AddOptions<Opt>(this IServiceCollection services, string name = default) where Opt : class
    {
        OptionsServiceCollectionExtensions.AddOptions<Opt>(services)
                                          .BindConfiguration(name ?? typeof(Opt).Name);
        return services;
    }

    public static string EnsureTrailing(this string input, string trailing = "/")
        => input?.EndsWith(trailing) ?? true ? input : input + trailing;

    public static string Prepend(this string source, string prepend)
        => source == null ? null : $"{prepend}{source}";

    public static string Append(this string source, string append)
        => source == null ? null : $"{source}{append}";

    public static ILogger GetLogger<T>(this IServiceProvider serviceProvider)
        => serviceProvider.GetRequiredService<ILogger<T>>();
}
