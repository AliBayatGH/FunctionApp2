using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace FunctionApp2;
public class StartStopVMFunction
{
    private readonly ILogger _logger;

    public StartStopVMFunction(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<StartStopVMFunction>();
    }

    [FunctionName("StartStopVMFunction")]
   // public void Run([TimerTrigger("0 0 2 * * ")] TimerInfo myTimer, ILogger log)
    public void Run([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer, ILogger log)
    {
        log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

        if (myTimer.ScheduleStatus is not null)
        {
            log.LogInformation($"Next timer schedule at: {myTimer.ScheduleStatus.Next}");
        }

        // Azure credentials and resource details
        var clientId = Environment.GetEnvironmentVariable("AzureClientId");
        var clientSecret = Environment.GetEnvironmentVariable("AzureClientSecret");
        var tenantId = Environment.GetEnvironmentVariable("AzureTenantId");
        var subscriptionId = Environment.GetEnvironmentVariable("AzureSubscriptionId");
        var resourceGroupName = Environment.GetEnvironmentVariable("AzureResourceGroupName");
        var vmName = Environment.GetEnvironmentVariable("AzureVMName");

        var azure = AuthenticateAzure(clientId, clientSecret, tenantId, subscriptionId);

        // Start VM
        log.LogInformation($"Starting VM {vmName} in resource group {resourceGroupName}");
        azure.VirtualMachines.Start(resourceGroupName, vmName);

        // Run PowerShell script on the VM
        RunPowerShellScriptOnVM(resourceGroupName, vmName, "YourScript.ps1");

        // Stop VM
        log.LogInformation($"Stopping VM {vmName} in resource group {resourceGroupName}");
        azure.VirtualMachines.PowerOff(resourceGroupName, vmName);
    }

    private static IAzure AuthenticateAzure(string clientId, string clientSecret, string tenantId, string subscriptionId)
    {
        var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId, clientSecret, tenantId, AzureEnvironment.AzureGlobalCloud);
        return Azure
            .Configure()
            .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
            .Authenticate(credentials)
            .WithSubscription(subscriptionId);
    }

    private static void RunPowerShellScriptOnVM(string resourceGroupName, string vmName, string scriptPath)
    {
        var connectionInfo = new WSManConnectionInfo(
            new Uri($"http://{vmName}:5985/wsman"),
            "http://schemas.microsoft.com/powershell/Microsoft.PowerShell",
            new PSCredential(null)
        );

        using (var runspace = RunspaceFactory.CreateRunspace(connectionInfo))
        {
            runspace.Open();

            using (var powerShellInstance = PowerShell.Create())
            {
                powerShellInstance.Runspace = runspace;

                // use "AddScript" to add the contents of a script
                powerShellInstance.AddScript(File.ReadAllText(scriptPath));

                // invoke execution on the pipeline (collecting output)
                Collection<PSObject> psOutput = powerShellInstance.Invoke();

                // loop through each output object item
                foreach (PSObject outputItem in psOutput)
                {
                    // TODO: Handle the output as needed
                }
            }
        }
    }
}



