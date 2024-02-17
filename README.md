# BuildXL Chat Bot

An AI assistant that can help with [BuildXL](https://github.com/microsoft/BuildXL) tasks.

Currently, it is capable of
- answering questions about BuildXL,
- constructing BuildXL command-line arguments (that even BuildXL developers do not know by heart),
- creating a CloudBuild build request, and
- submitting the build request to the CloudBuild service.

This AI assistant is far from being production quality. One can use it as a platform to learn about semantic kernel and prompt design/engineering.

Feel free to contribute to this chat bot by adding more functionalities or features (e.g., adding semantic memory).

## Requirements
- .NET >= 8

## How to run
- Set/add environment variable `OPENAI_SETTINGS`, whose value has information regarding the model, end point, and API key. The format of value
  is `UseOpenAI;Model;EndPoint;ApiKey;OrgId`, where
    - `UseOpenAI`: whether to use Open AI or Azure Open AI; `true` for using Open AI, and `false` for using Azure Open AI.
    - `Model`: model to use, e.g., `gpt-3.5-turbo`.
    - `EndPoint`: end point; not applicable when using Open AI, so leave it blank.
    - `ApiKey`: API key.
    - `OrgId`: organization id; leave it blank if not applicable.

  Examples:
    - `true;gpt-3.5-turbo;;sk-12345;` - for OpenAI, with `gpt-3.5-turbo` model and API key `sk-12345`
    - `false;gpt-35-turbo-a;https://1es.openai.azure.com/;5e1ec7ed;` - for Azure OpenAI, with `gpt-35-turbo-a` model, `https://1es.openai.azure.com` endpoint, and API key `5e1ec7ed`

  Note that in both examples the organization id is left blank.

  To set the environment variable:
  - PowerShell: 
    ```powershell
    $env:OPENAI_SETTINGS='false;gpt-35-turbo-a;https://1es.openai.azure.com/;5e1ec7ed;'
    ```
  - CMD: 
    ```cmd
    set OPENAI_SETTINGS=false;gpt-35-turbo-a;https://1es.openai.azure.com/;5e1ec7ed;
    ```
  - Bash: 
    ```bash
    export OPENAI_SETTINGS=false;gpt-35-turbo-a;https://1es.openai.azure.com/;5e1ec7ed;
    ```

- Run AI assistant: 
  ```shell
  dotnet run
  ```

## Screenshots

![Screenshot 1](/Images/sc1.png)

![Screenshot 2](/Images/sc2.png)

## References

- [BuildXL, Microsoft Build Accelerator](https://github.com/microsoft/BuildXL)
- Learn semantic kernel:
  - [Semantic kernel @ Microsoft](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
  - [Semantic kernel @ Microsoft/Github](https://github.com/microsoft/semantic-kernel)
- [Demystifying Retrieval Augmented Generation with .NET](https://devblogs.microsoft.com/dotnet/demystifying-retrieval-augmented-generation-with-dotnet/)
