[![Nuget](https://img.shields.io/nuget/v/Microsoft.CST.OSSGadget.Shared)](https://www.nuget.org/packages/Microsoft.CST.OSSGadget.Shared)
![CodeQL](https://github.com/microsoft/OSSGadget/workflows/CodeQL/badge.svg)

## OSS Gadget

> **Note:** OSS Gadget is currently in **public preview** and is not ready for production use.

OSS Gadget is a collection of tools that can help analyze open source projects. These are intended to make it simple to perform low-level tasks, like locating the source code of a given package, downloading it, performing basic analyses on it, or estimating its health. The tools included in OSS Gadget will grow over time.

### Included Tools
A list of tools included is below.  Click on the name of a tool to go to the wiki for usage information.

* [oss-characteristic](https://github.com/microsoft/OSSGadget/wiki/OSS-Characteristics): Identify a package's notable characteristics and features. Uses
  [Application Inspector](https://github.com/Microsoft/ApplicationInspector).
* [oss-defog](https://github.com/microsoft/OSSGadget/wiki/OSS-Defog): Searches a package for obfuscated strings (Base-64).
* [oss-detect-backdoor](https://github.com/microsoft/OSSGadget/wiki/OSS-Detect-Backdoor): Identifies *potential* backdoors and malicious code within a package. Currently has a high false-positive rate.
* [oss-detect-cryptography](https://github.com/microsoft/OSSGadget/wiki/OSS-Detect-Cryptography): Identifies cryptographic implementations within a package.
* [oss-diff](https://github.com/microsoft/OSSGadget/wiki/OSS-Diff): Compares two packages using a standard diff/patch view.
* [oss-download](https://github.com/microsoft/OSSGadget/wiki/OSS-Download): Downloads a package and extracts it locally.
* [oss-find-domain-squats](https://github.com/microsoft/OSSGadget/wiki/OSS-Find-Domain-Squats): Identifies potential typo-squatting for a given domain name.
* [oss-find-source](https://github.com/microsoft/OSSGadget/wiki/OSS-Find-Source): Attempts to locate the source code (on GitHub, currently) of a given package.
* [oss-find-squats](https://github.com/microsoft/OSSGadget/wiki/OSS-Find-Squats): Identifies potential typo-squatting for a given package.
* [oss-health](https://github.com/microsoft/OSSGadget/wiki/OSS-Health): Calculates health metrics for a given package.
* [oss-metadata](https://github.com/microsoft/OSSGadget/wiki/OSS-Metadata): Retrieves metadata from deps.dev or libraries.io for a given package.
* [oss-risk-calculator](https://github.com/microsoft/OSSGadget/wiki/OSS-Risk-Calculator): Calculates a metric for risk of using a package.

All OSS Gadget tools accept one or more [Package URLs](https://github.com/package-url/purl-spec) as a way to uniquely identify a package. Package URLs look like `pkg:npm/express` or `pkg:gem/azure@0.7.10`. If you leave the version number off, it implicitly means, "attempt to find the latest version". Using an asterisk (`pkg:npm/express@*`) means "perform the action on all available versions".

### Package Sources
OSS Gadget supports packages provided by these sources:

* Cargo - `pkg:cargo/...`
* Cocoapods - `pkg:cocoapods/...`
* Composer - `pkg:composer/...`
* CPAN - `pkg:cpan/...`
* CRAN - `pkg:cran/...`
* GitHub - `pkg:github/...`
* Go - `pkg:golang/...`
* Hackage - `pkg:hackage/...`
* Maven - `pkg:maven/...`
* NPM - `pkg:npm/...`
* NuGet - `pkg:nuget/...`
* RubyGems - `pkg:gem/...`
* PyPI - `pkg:pypi/...`
* Ubuntu - `pkg:ubuntu/...`
* Visual Studio Marketplace - `pkg:vsm/...`
* Generic - `pkg:url/...?url=URL`

We will continue expanding this list to cover additional package management systems and would be happy to accept contributions from the community.

## Basic Usage

All OSS Gadget tools are command line programs. When installed globally, they can be accessed from your path. For example, to download the NPM left-pad module, type:

```
$ oss-download pkg:npm/left-pad
```

This will download left-pad into a newly-created directory named `npm-left-pad@1.3.0`. (Because, at the time of this writing, 1.3.0 was the latest version of [left-pad](https://www.npmjs.com/package/left-pad)).

Each of the programs self-documents information on command line options (`--help`).

### Building from Source
OSS Gadget builds with standard `dotnet build` commands and includes tests via `dotnet test`.

See [Building from Source](https://github.com/microsoft/OSSGadget/wiki/Building-from-Source) in the wiki for information on building from source.

### Docker Image
See [Docker Image](https://github.com/microsoft/OSSGadget/wiki/Docker-Image) in the wiki for information on how to use the included Dockerfile.

### Advanced Usage
See [Advanced Usage](https://github.com/microsoft/OSSGadget/wiki/Advanced-Usage) in the wiki for advanced usage information like changing API endpoints.

### Reporting Security Vulnerabilities

To report a security vulnerability, please see [SECURITY.md](SECURITY.md).

### Contributing to OSS Gadget

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
