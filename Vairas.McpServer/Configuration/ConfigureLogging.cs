namespace Vairas.McpServer.Configuration;

public static class ConfigureLogging
{
    public static ILoggingBuilder AddJsonLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddJsonConsole();
        return logging;
    }
}
