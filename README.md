# OSS Gadget

OSS Gadget is a collection is tools that can help analyze open source projects. These are intended to make it simple to
perform low-level tasks, like locating the source code of a given package, downloading it, analyzing it, or estimating
the health of a project.

OSS Gadget contains the following tools:

* *oss-characteristics*: wraps [Microsoft Application Inspector](https://github.com/Microsoft/ApplicationInspector),
enabling an analysis of an open source packackage.
* *oss-defog*: identifies obfuscated strings (currently, Base-64) within source code.
* *oss-detect-backdoor*: identifies backdoors within an open source package through pattern matching.
* *oss-download*: downloads open source packages.
* *oss-find-source*: locates the source code repository for a given open source package.
* *oss-health*: calculates the health of an open source package.

### Package URLs

In general, all tools accept one or more [Package URLs](https://github.com/package-url/purl-spec) as a way to uniquely
identify a package. Package URLs look like `pkg:npm/express` or `pkg:gem/azure@0.7.10`. For most tools, leaving the
version number off means, "attempt to find the latest version", and using an asterisk (`pkg:npm/express@*`) means
"perform the action on all available versions".

Certain packages must be encoded when described as a Package URL. For example, if a package namespace or name
contains a `@`, such as in `pkg:npm/@types/azure`, that symbol must be encoded before being passed to OSS Gadget,
e.g. `pkg:npm/%40types/azure`. For more information see
[Character encoding](https://github.com/package-url/purl-spec#character-encoding) in the Package URL specification. 

If you use an alternate package source, see the *Advanced* section below on how to target different root URLs.

### Supported Package Managers

OSS Gadget supports open source packages distributed through the following systems:

* Cargo
* Cocoapods
* Composer
* CPAN
* CRAN
* GitHub
* Hackage
* Maven
* NPM
* NuGet
* RubyGems
* PyPI

We'd like to expand this list to include additional systems, and would be happy to accept pull requests to add these.


## Tools

### OSS Characteristics

OSS Characteristics is a very thin wrapper on top of [Application Inspector](https://github.com/Microsoft/ApplicationInspector).
It analyzes a package and gives details on the major characteristics.

### OSS Defog

OSS Defog examines a package's contents for obfuscated text -- specifically, text that is either Base-64- or Hex-encoded. Most
packages do not contain such content, and even the ones that do are usually safe. However, obfuscation has been used to hide malicious
code in open source projects.

### OSS Detect Backdoor

The *oss-detect-backdoor* tool identifies certain patterns that suggest a backdoor within an open source package. This tool currently
has a higher-than-desired false positive rate, and should not be used to take any action automatically. It uses the 
[Application Inspector](https://github.com/Microsoft/ApplicationInspector) engine with a custom set of rules that can be easily extended.

### OSS Download

The oss-download tool downloads and extracts an open source package, based on a Package URL. For example:

```
$ oss-download pkg:/npm/express
```

### OSS Find Source

The *oss-find-source* tool attempts to locate the source code for a given package. It does this by downloading and examining metadata
associated with a package, looking for GitHub URLs. As such, it may return multiple GitHub URLs, or none at all.

```
$ .\oss-find-source pkg:npm/left-pad
Found: pkg:github/stevemao/left-pad (https://github.com/stevemao/left-pad)
Found: pkg:github/azer/left-pad (https://github.com/azer/left-pad)
Found: pkg:github/camwest/left-pad (https://github.com/camwest/left-pad)

$ .\oss-find-source pkg:pypi/django
Found: pkg:github/django/django (https://github.com/django/django)
```

### OSS Health

The *oss-health* tool calculates the health of certain open source projects by analyzing information available through the public 
GitHub API, including:

* Commit History
* Contributor Graph
* Release / Tag 
* Issue Counts (Security and Non-Security)

This algorithm will be refined over time.

#### Encoding

## Building

To build OSS Gadget, you must have the [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) installed.
Then, simply clone the repository and run `dotnet build` in the `src` directory with the relevant parameters:

```
# Default build parameters
dotnet build

# dotnet build -c Release -f netcoreapp3.1 -r win10-x64
Microsoft (R) Build Engine version 16.4.0+e901037fe for .NET Core
Copyright (C) Microsoft Corporation. All rights reserved.

  Restore completed in 497.61 ms for C:\OSSGadget\src\oss-find-source\oss-find-source.csproj.
  Restore completed in 497.62 ms for C:\OSSGadget\src\oss-download\oss-download.csproj.
  Restore completed in 497.62 ms for C:\OSSGadget\src\Shared\Shared.csproj.
  Restore completed in 497.6 ms for C:\OSSGadget\src\oss-detect-backdoor\oss-detect-backdoor.csproj.
  Restore completed in 497.61 ms for C:\OSSGadget\src\oss-characteristics\oss-characteristic.csproj.
  Restore completed in 497.59 ms for C:\OSSGadget\src\oss-health\oss-health.csproj.
  Restore completed in 1.46 sec for C:\OSSGadget\src\oss-defog\oss-defog.csproj.
  Shared -> C:\OSSGadget\src\Shared\bin\Release\netcoreapp3.1\Shared.dll
  Shared -> C:\OSSGadget\src\Shared\bin\Release\netcoreapp3.1\win10-x64\Shared.dll
  oss-download -> C:\OSSGadget\src\oss-download\bin\Release\netcoreapp3.1\win10-x64\oss-download.dll
  oss-download -> C:\OSSGadget\src\oss-download\bin\Release\netcoreapp3.1\oss-download.dll
  oss-find-source -> C:\OSSGadget\src\oss-find-source\bin\Release\netcoreapp3.1\win10-x64\oss-find-source.dll
  oss-health -> C:\OSSGadget\src\oss-health\bin\Release\netcoreapp3.1\win10-x64\oss-health.dll
  oss-characteristic -> C:\OSSGadget\src\oss-characteristics\bin\Release\netcoreapp3.1\oss-characteristic.dll
  oss-characteristic -> C:\OSSGadget\src\oss-characteristics\bin\Release\netcoreapp3.1\win10-x64\oss-characteristic.dll
  oss-defog -> C:\OSSGadget\src\oss-defog\bin\Release\netcoreapp3.1\win10-x64\oss-defog.dll
  oss-detect-backdoor -> C:\OSSGadget\src\oss-detect-backdoor\bin\Release\netcoreapp3.1\win10-x64\oss-detect-backdoor.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.47
```

In this case, executables will be placed in a location like `oss-download\bin\Release\netcoreapp3.1\win10-x64`,
for each respective project.

## Advanced Usage

### Logging

@TODO

### Alternate Endpoints

If you're using an alternate endpoint, such as a corporate NPM mirror, you can set certain environment variables to
redirect traffic to that location instead of the default one. These environment variables include:

* `GITHUB_ACCESS_TOKEN`: When connecting to GitHub, this environment variable must be defined.
* `CARGO_ENDPOINT`: Connect to Cargo (Rust), default value is https://crates.io.
* `CARGO_ENDPOINT_STATIC`: Connect to Cargo (Rust), default value is https://static.crates.io.
* `COCOAPODS_SPECS_ENDPOINT`: Connect to Cocoapods (MacOS / iOS), default value is https://github.com/Cocoapods/Specs/tree/master.
* `COCOAPODS_SPECS_RAW_ENDPOINT`: Connect to Cocoapods (MacOS / iOS), default value is https://raw.githubusercontent.com/CocoaPods/Specs/master.
* `COCOAPODS_METADATA_ENDPOINT`: Connect to Cocoapods (MacOS / iOS), default value is https://cocoapods.org.
* `COMPOSER_ENDPOINT`: Connect to Composer (PHP), default value is https://repo.packagist.org.
* `CPAN_ENDPOINT`: Connect to CPAN (Perl), default value is https://metacpan.org.
* `CPAN_BINARY_ENDPOINT`: Connect to CPAN (Perl), default value is https://cpan.metacpan.org.
* `CRAN_ENDPOINT`: Connect to CRAN (R), default value is https://cran.r-project.org.
* `RUBYGEMS_ENDPOINT`: Connect to RubyGems (Ruby), default value is https://rubygems.org.
* `RUBYGEMS_ENDPOINT_API`: Connect to RubyGems (Ruby), default value is https://api.rubygems.org.
* `HACKAGE_ENDPOINT`: Connect to Hackage (Haskell), default value is https://hackage.haskell.org.
* `MAVEN_ENDPOINT`: Connect to Maven (Java), default value is https://repo1.maven.org/maven2.
* `NPM_ENDPOINT`: Connect to NPM (JavaScript), default value is https://registry.npmjs.org.
* `NUGET_ENDPOINT_API`: Connect to NuGet (.NET), default value is https://api.nuget.org.
* `PYPI_ENDPOINT`: Connect to PyPI (Python), default value is https://pypi.org.

Please note that for some systems, like Cocoapods, if you change one value, you'll very likely need to change the others.


# Security

If you discover a security issue in this project, please report it to us privately so we can fix the
issue promptly. More details are in [SECURITY.md](SECURITY.md).

# Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit [cla.opensource.microsoft.com](https://cla.opensource.microsoft.com).

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
