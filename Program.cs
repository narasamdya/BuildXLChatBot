
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using BuildXLChatBot;
using BuildXLChatBot.Plugins;
using Kernel = Microsoft.SemanticKernel.Kernel;

#pragma warning disable SKEXP0003, SKEXP0011, SKEXP0052, SKEXP0055 // Experimental

namespace BuildXLChatBot;

public class Program
{
    private const string AssistantContext = 
@"You are an AI assistant that can help with BuildXL tasks.
  If you don't know how to carry out the task, simply say 'I don't know'.";

    private const string SystemBackground =
@"Microsoft Build Accelerator (or, BuildXL) is an open source, cross platform, distributed build engine created by the 1ES team. It utilizes an observation-based sandbox to ensure correctness of caching and distribution. It runs in CloudBuild, Azure DevOps, and on dev machines. It supports running on Windows and Linux.
BuildXL has also been proven to scale to large codebases (e.g., Windows/Office repositories) where builds can consist of millions of processes with terabytes of outputs. BuildXL relies on runtime file-access monitoring for correct caching and distribution.

Documentation: https://github.com/microsoft/BuildXL/blob/main/Documentation/INDEX.md

The BuildXL tool has many arguments or command-line options that can be used to perform or control builds.

The following is a list of BuildXL command-line options or arguments. Each option is followed by a brief description of the option separated by '=>':

- /qualifier:<qualifier_value> => Qualifiers for controlling the build flavor (short form: /q).
    EXAMPLE: /qualifier:debug => Builds the debug flavor.
- /property:<key>=<value> => Specifies a build property, in key=value format, that overrides an allowed environment variable (short form: /p).
    EXAMPLE: /property:FOO=BAR => Overrides the environment variable FOO with the value BAR.
- /logProcesses => Log all processes, including nested child processes, launched during the build for each executed process pip.
- /logProcessData => Log process execution times and IO counts and transfers. This option requires /logProcesses to be enabled.
- /logObservedFileAccesses => Log observed file accessess for each executed process pip.
- /logProcessDetouringStatus => Log the detouring status for each executed process pip.
- /cacheMiss => Enable on-the-fly cache miss analysis during the execute phase.
    EXAMPLE: 
    * /cacheMiss => Use the local FingerprintStore for comparison.
    * /cacheMiss:[changeset1:changeset2:changeset3] => Compare the build to a remote FingerprintStore identified by changeset1, changeset2, and changeset3.
- /maxProc:<number> => Specifies the maximum number of processes that BuildXL will launch at one time.
    EXAMPLE: /maxProc:10 => Specifies the maximum number of processes to be 10.
- /maxProcMultiplier:<number> => Specifies the multiplier for /maxProc.
    EXAMPLE: /maxProcMultiplier:2 => Specifies the multiplier for /maxProc to be 2.
- /enableLinuxPTraceSandbox => Enables the ptrace sandbox on Linux when a statically linked binary is detected.
- /pipProperty:[PipId:[PropertyAndValue]] => Sets execution behaviors for a pip identified by PipId. PropertyAndValue can be a name or a <key>=<value> pair. Supported properties are 'PipFingerprintSalt' and 'EnableVerboseProcessLogging'.
    EXAMPLES: 
    * /pipProperty:Pip123[PipFingerprintingSalt=Foo] => set 'Foo' fingerprint salt to pip Pip123
    * /pipProperty:Pip123[PipFingerprintingSalt=*] => set random fingerprint salt to pip Pip123
    * /pipProperty:Pip123[EnableVerboseProcessLogging] => set verbose process logging to pip Pip123

- /filter:<filter_expression> => Filter build according to filter expression <filter_expression>  (short form: /f). Filter expressions consists of filter types that can be combined with the negation '~', 'and', and 'or' operators.
    Types of filters:
        * id => Filters by a pip's id or pip's semi-stable hash.
        * tag => Filters by a tag.
        * output => Filters pips by the output files they create. The value may be: 'path' to match a file, 'path\'. to match files within a directory, 'path\*' to match files in a directory and recursive directories, or '*\fileName' to match files with a specific name no matter where they are. 'path' may be an absolute or relative path. 'fileName' must be a fileName and may not contain any directory separator characters.
        * input => Filters by an input to a pip. The value may be: 'path' to match a file, 'path\'. to match files within a directory, 'path\*' to match files in a directory and recursive directories, or '*\fileName' to match files with a specific name no matter where they are. 'path' may be an absolute or relative path. 'fileName' must be a fileName and may not contain any directory separator characters.
        * value => Filters by value name.

    EXAMPLES:
        * /f:id='123456' => Selects the pip with the id or semi-stable hash '123456' and its dependencies.
        * /f:tag='test' => Selects all pips marked with the 'test' tag, including their dependencies.
        * /f:output='out\bin\release\foo.exe' => Selects all pips who will produce the output 'out\bin\release\foo.exe' and their dependencies.
        * /f:output='out\bin\release\*' => Selects all pips who will produce outputs in 'out\bin\release' folder, including their dependencies.
        * /f:input='src\utilities\foo.cpp' => Selects all pips who will consume the file 'src\utilities\foo.cpp', including their dependencies.
        * /f:input='src\utilities\*' => Selects all pips who will consume inputs from 'src\utilities' folder, including their dependencies.
        * /f:value='Foo' => Selects all pips with a value named 'Foo', including their dependencies.

    EXAMPLES of filter expressions with negation and combination are as follows:
        * /f:~(tag='test') => Selects all pips not marked with the 'test' tag, including their dependencies
        * /f:(tag='foo'and~(tag='test')) => Selects all pips marked with tag 'foo' and not marked with tag 'test', including their dependencies
        * /f:(tag='foo'or(tag='test')) => Selects all pips marked with tag 'foo' or marked with tag 'test', including their dependencies
        * /f:(output='out\bin\release\*'and(input='src\utilities\foo.cpp')) => Selects all pips that produce outputs in 'out\bin\release' folder and consume the file 'src\utilities\foo.cpp', including their dependencies
";

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.Unicode;  

        if (!Settings.TryReadFromEnvironment(out Settings? settings, out string? error))
        {
            Console.WriteLine($"Error reading settings: {error}");
            return;
        }

        Console.WriteLine($"UseOpenAI: {settings.UseOpenAI}");
        Console.WriteLine($"Model: {settings.Model}");

        Kernel kernel = CreateKernel(settings);
        IChatCompletionService ai = kernel.GetRequiredService<IChatCompletionService>();

        var chat = new ChatHistory(AssistantContext + Environment.NewLine + SystemBackground);
        var builder = new StringBuilder();
        var openAIPromptExecutionSettings = new OpenAIPromptExecutionSettings()
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        Console.WriteLine("🤖 : Your command is my wish, sir! (or type 'exit' to quit)");

        // Q&A loop
        while (true)
        {
            Console.Write("😎 : ");
            string userMessage = Console.ReadLine()!;

            if (string.Equals("exit", userMessage.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("🤖 : Goodbye!");
                break;
            }

            chat.AddUserMessage(userMessage);
            builder.Clear();

            bool first = true;
            await foreach (var message in ai.GetStreamingChatMessageContentsAsync(
                chat,
                executionSettings: openAIPromptExecutionSettings,
                kernel: kernel))
            {
                if (first)
                {
                    Console.WriteLine("🤖 :");
                    first = false;
                }

                Console.Write(message);
                builder.Append(message.Content);
            }

            Console.WriteLine();
            chat.AddAssistantMessage(builder.ToString());
        }
    }

    private static Kernel CreateKernel(Settings settings)
    {
        IKernelBuilder builder = Kernel.CreateBuilder();
        if (settings.UseOpenAI)
        {
            builder.AddOpenAIChatCompletion(settings.Model, settings.ApiKey, settings.OrgId);
        }
        else
        {
            builder.AddAzureOpenAIChatCompletion(settings.Model, settings.EndPoint, settings.ApiKey);
        }

        builder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Trace));
        builder.Plugins.AddFromType<CBPlugin>();

        return builder.Build();
    }
}