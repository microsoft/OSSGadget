namespace oss_gadget_cli;

using CommandLine;
using Microsoft.CST.OpenSource;
using Microsoft.CST.OpenSource.DomainSquats;
using Microsoft.CST.OpenSource.OssGadget.Options;
using Microsoft.CST.OpenSource.OssGadget.CLI.Tools;
using Microsoft.CST.OpenSource.OssGadget.Tools;
using Microsoft.CST.OpenSource.OssGadget.Tools.HealthTool;
using System.Reflection;

class OssGadgetCli : OSSGadget
{
    static async Task<int> Main(string[] args)
    {
        OssGadgetCli cli = new OssGadgetCli();
        Type[] verbs = LoadVerbs();
        ParserResult<object> parsed = Parser.Default.ParseArguments(args, verbs);
        if (parsed.Errors.Any())
        {
            return (int)cli.HandleErrors(parsed.Errors);
        }
        return (int)await cli.RunAsync(parsed.Value);
    }
    
    //load option types using reflection
    private	static Type[] LoadVerbs()
    {
        return (Assembly.GetAssembly(typeof(BaseToolOptions))?.GetTypes() ?? [])
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null 
                        && t.IsAssignableTo(typeof(BaseToolOptions))).ToArray();		 
    }
    
    private async Task<ErrorCode> RunAsync(object obj)
    {
        return obj switch
        {
            DownloadToolOptions d => await new DownloadTool(ProjectManagerFactory).RunAsync(d),
            HealthToolOptions healthToolOptions => await new HealthTool(ProjectManagerFactory).RunAsync(healthToolOptions),
            RiskCalculatorToolOptions riskCalculatorToolOptions => await new RiskCalculatorTool(ProjectManagerFactory).RunAsync(riskCalculatorToolOptions),
            CharacteristicToolOptions characteristicToolOptions => await new CharacteristicTool(ProjectManagerFactory).RunAsync(characteristicToolOptions),
            FreshToolOptions freshToolOptions => await new FreshTool(ProjectManagerFactory).RunAsync(freshToolOptions),
            DiffToolOptions diffToolOptions => await new DiffTool(ProjectManagerFactory).RunAsync(diffToolOptions),
            ReproducibleToolOptions reproducibleToolOptions => await new ReproducibleTool(ProjectManagerFactory).RunAsync(reproducibleToolOptions),
            FindDomainSquatsToolOptions findDomainSquatsToolOptions => await new FindDomainSquatsTool(new DefaultHttpClientFactory()).RunAsync(findDomainSquatsToolOptions),
            FindSourceToolOptions findSourceToolOptions => await new FindSourceTool(ProjectManagerFactory).RunAsync(findSourceToolOptions),
            _ => ErrorCode.ErrorParsingOptions
        };
    }
    
    private ErrorCode HandleErrors(IEnumerable<Error> errs)
    {
        return errs.Any(x =>
            x.Tag is not ErrorType.VersionRequestedError
                and not ErrorType.HelpVerbRequestedError
                and not ErrorType.HelpRequestedError)
            ? ErrorCode.ErrorParsingOptions : ErrorCode.Ok;
    }
}