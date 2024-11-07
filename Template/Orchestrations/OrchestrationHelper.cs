namespace Orchestrations;

public static class OrchestrationHelper
{
    public static string IdentifyServer()
    {
        string test = Environment.GetEnvironmentVariable("AZURITE_ACCOUNTS");
        if (!string.IsNullOrEmpty(test))
        {
            return "local-azurite";
        }
        test = Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID");
        if (!string.IsNullOrEmpty(test))
        {
            return $"azure-{test}";
        }
        test = Environment.GetEnvironmentVariable("HOSTNAME");
        if (!string.IsNullOrEmpty(test))
        {
            return $"docker-{test}";
        }
        test = Environment.GetEnvironmentVariable("COMPUTERNAME");
        if (!string.IsNullOrEmpty(test))
        {
            return $"machine-{test}";
        }
        return "unknown";
    }
}
