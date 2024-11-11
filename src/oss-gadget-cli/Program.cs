namespace oss_gadget_cli;

using CommandLine;
using Microsoft.CST.OpenSource;
using Microsoft.CST.OpenSource.OssGadget.Options;
using Microsoft.CST.OpenSource.OssGadget.CLI.Tools;
using Microsoft.CST.OpenSource.OssGadget.Tools;
using Microsoft.CST.OpenSource.OssGadget.Tools.HealthTool;
using System.Reflection;

class OssGadgetCli : OSSGadget
{
    static async Task<int> Main(string[] args)
    {
        var cli = new OssGadgetCli();
        await cli.ExecuteAsync(args);
        return (int)cli._returnCode;
    }

    private ErrorCode _returnCode = ErrorCode.Ok;
    
    //load all types using Reflection
    private	static Type[] LoadVerbs()
    {
        return Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();		 
    }
    
    private async Task Run(object obj)
    {
        _returnCode = obj switch
        {
            DownloadToolOptions d => await new DownloadTool(ProjectManagerFactory).RunAsync(d),
            HealthToolOptions healthToolOptions => await new HealthTool(ProjectManagerFactory).RunAsync(healthToolOptions),
            RiskCalculatorToolOptions riskCalculatorToolOptions => await new RiskCalculatorTool(ProjectManagerFactory).RunAsync(riskCalculatorToolOptions),
            CharacteristicToolOptions characteristicToolOptions => await new CharacteristicTool(ProjectManagerFactory).RunAsync(characteristicToolOptions),
            _ => ErrorCode.Ok
        };
    }
    
    async Task ExecuteAsync(string[] args)
    {
        var verbs = LoadVerbs();

        var res = (await Parser.Default.ParseArguments(args, verbs).WithParsedAsync(Run)).WithNotParsed(HandleErrors);
    }

    private void HandleErrors(IEnumerable<Error> errs)
    {
        _returnCode = errs.Any(x =>
            x.Tag is not ErrorType.VersionRequestedError
                and not ErrorType.HelpVerbRequestedError
                and not ErrorType.HelpRequestedError)
            ? ErrorCode.ErrorParsingOptions : ErrorCode.Ok;
    }
}