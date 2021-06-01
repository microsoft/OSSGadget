![CodeQL](https://github.com/microsoft/OSSGadget/workflows/CodeQL/badge.svg)

## OSS Gadget

> **Note:** OSS Gadget is currently in **public preview** and is not ready for production use.

OSS Gadget is a collection of tools that can help analyze open source projects. These are intended to make it simple to
perform low-level tasks, like locating the source code of a given package, downloading it, performing basic analyses on
it, or estimating its health. The tools included in OSS Gadget will grow over time; currently, they include:

* *oss-characteristic*: Identify a package's notable characteristics and features. Uses
  [Application Inspector](https://github.com/Microsoft/ApplicationInspector).
* *oss-defog*: Searches a package for obfuscated strings (Base-64).
* *oss-detect-backdoor*: Identifies *potential* backdoors and malicious code within a package. Currently has a high
  false-positive rate.
* *oss-detect-cryptography*: Identifies cryptographic implementations within a package.
* *oss-diff*: Compares two packages using a standard diff/patch view.
* *oss-download*: Downloads a package and extracts it locally.
* *oss-find-domain-squats*: Identifies potential typo-squatting for a given domain name.
* *oss-find-source*: Attempts to locate the source code (on GitHub, currently) of a given package.
* *oss-find-squats*: Identifies potential typo-squatting for a given package.
* *oss-health*: Calculates health metrics for a given package.
* *oss-metadata*: Normalizes metadata about a package into a common schema.

All OSS Gadget tools accept one or more [Package URLs](https://github.com/package-url/purl-spec) as a way to uniquely
identify a package. Package URLs look like `pkg:npm/express` or `pkg:gem/azure@0.7.10`. If you leave the
version number off, it implicitly means, "attempt to find the latest version". Using an asterisk
(`pkg:npm/express@*`) means "perform the action on all available versions".

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

We will continue expanding this list to cover additional package management systems and would be happy to accept
contributions from the community.

### Basic Usage

All OSS Gadget tools are command line programs. When installed globally, they can be accessed from your path. For
example, to download the NPM left-pad module, type:

```
$ oss-download pkg:npm/left-pad
```

This will download left-pad into a newly-created directory named `npm-left-pad@1.3.0`. (Because, at the time of this writing, 1.3.0
was the latest version of [left-pad](https://www.npmjs.com/package/left-pad)).

Each of the programs contains information on command line options (`--help`).

### Detailed Tool Information

#### OSS Characteristics

OSS Characteristics is a very thin wrapper on top of [Application Inspector](https://github.com/Microsoft/ApplicationInspector).
It analyzes a package and gives details on the major characteristics.

#### OSS Defog

OSS Defog examines a package's contents for obfuscated text -- specifically, text that is either Base-64- or Hex-encoded. Most
packages do not contain such content, and even the ones that do are usually safe. However, obfuscation has been used to hide malicious
code in open source projects.

#### OSS Download

OSS Download provides a shortcut to download a package. This can be useful in a few different scenarios:

* You are operating across multiple package management ecosystems.
* You don't have (or want) native tools installed, like NPM or Python/PIP.
* You need to download many packages, and therefore cannot use a web-browser.

OSS Download takes a Package URL and calls a module specific to the package manager. For example, if an NPM package is requested,
code will run to query the NPM registry for the project, searching for the correct binary. Once found, it downloads it, and
by default, extracts it into a new directory.

Note that for GitHub projects, the `git ls-remote` command is currently needed to enumerate tags, which means that you
need to have git installed and available on the path.

#### OSS Find Source

It's often useful to locate the source code to a given package. OSS Find Source works by searching through package metadata
(obtained by querying an API or scraping relevant web pages) for GitHub URLs. It then outputs that list of URLs.

Currently, OSS Find Source is only aware of GitHub. Support for Bitbucket, GitLab, and other sources may be added in
the future.

#### OSS Health

As the name suggests, OSS Health estimates the health of an open source package. It does this by collecting various metrics
from a project (currently only supported for GitHub), combining them through an algorithm that we think is reasonable, and displaying
the output.

In this context, we mean "health" to mean, roughly, the likelihood that a package will continue to meet stakeholder
expectations in the future. We can divide this into different areas:

* Will the project continue to address bugs?
* Will there be new/improved features?
* How vibrant is the community?
* What is the so-called "[bus factor](https://en.wikipedia.org/wiki/Bus_factor)"?
* Are security issues addressed promptly?

We recognize that the algorithm implemented isn't perfect, and welcome dialogue and contributions on how to improve it.

#### OSS Risk Calculator

OSS Risk Calculator combines two other tools, OSS Health and OSS Characteristics, to calculate a risk score for a project. 
You can ignore the health aspect by passing in the `--no-health` command line option, and the output will be a risk level
in a range from 0 (no risk) to 1 (very high risk).

The algorithm we use could definitely be improved ([#150](https://github.com/microsoft/OSSGadget/issues/150)).

### Building from Source

OSS Gadget was built and tested using .NET Core 5.0, and will generally target the latest version of .NET Core.
To build OSS Gadget, simply clone the project and run `dotnet build` from the `src` directory.

```
PS C:\OSSGadget\src> dotnet build
Microsoft (R) Build Engine version 16.9.0+57a23d249 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  Restored C:\OSSGadget\src\oss-diff\oss-diff.csproj (in 1.37 sec).
  Restored C:\OSSGadget\src\oss-metadata\oss-metadata.csproj (in 1.37 sec).
  Restored C:\OSSGadget\src\Shared\Shared.csproj (in 1.37 sec).
  Restored C:\OSSGadget\src\oss-defog\oss-defog.csproj (in 1.37 sec).
  Restored C:\OSSGadget\src\oss-detect-cryptography\oss-detect-cryptography.csproj (in 1.42 sec).
  Restored C:\OSSGadget\src\oss-find-source\oss-find-source.csproj (in 1.37 sec).
  Restored C:\OSSGadget\src\oss-risk-calculator\oss-risk-calculator.csproj (in 1.51 sec).
  Restored C:\OSSGadget\src\oss-tests\oss-tests.csproj (in 1.61 sec).
  Restored C:\OSSGadget\src\oss-find-domain-squats\oss-find-domain-squats.csproj (in 764 ms).
  Restored C:\OSSGadget\src\oss-download\oss-download.csproj (in 777 ms).
  Restored C:\OSSGadget\src\oss-health\oss-health.csproj (in 778 ms).
  Restored C:\OSSGadget\src\oss-find-squats\oss-find-squats.csproj (in 765 ms).
  Restored C:\OSSGadget\src\oss-characteristics\oss-characteristic.csproj (in 836 ms).
  Restored C:\OSSGadget\src\oss-detect-backdoor\oss-detect-backdoor.csproj (in 853 ms).
  Shared -> C:\OSSGadget\src\Shared\bin\Debug\net5.0\Shared.dll
  oss-find-source -> C:\OSSGadget\src\oss-find-source\bin\Debug\net5.0\oss-find-source.dll
  oss-download -> C:\OSSGadget\src\oss-download\bin\Debug\net5.0\oss-download.dll
  oss-metadata -> C:\OSSGadget\src\oss-metadata\bin\Debug\net5.0\oss-metadata.dll
  oss-find-squats -> C:\OSSGadget\src\oss-find-squats\bin\Debug\net5.0\oss-find-squats.dll
  oss-diff -> C:\OSSGadget\src\oss-diff\bin\Debug\net5.0\oss-diff.dll
  oss-find-domain-squats -> C:\OSSGadget\src\oss-find-domain-squats\bin\Debug\net5.0\oss-find-domain-squats.dll
  oss-defog -> C:\OSSGadget\src\oss-defog\bin\Debug\net5.0\oss-defog.dll
  oss-health -> C:\OSSGadget\src\oss-health\bin\Debug\net5.0\oss-health.dll
  oss-characteristic -> C:\OSSGadget\src\oss-characteristics\bin\Debug\net5.0\oss-characteristic.dll
  oss-detect-cryptography -> C:\OSSGadget\src\oss-detect-cryptography\bin\Debug\net5.0\oss-detect-cryptography.dll
  oss-detect-backdoor -> C:\OSSGadget\src\oss-detect-backdoor\bin\Debug\net5.0\oss-detect-backdoor.dll
  oss-risk-calculator -> C:\OSSGadget\src\oss-risk-calculator\bin\Debug\net5.0\oss-risk-calculator.dll

Build succeeded.

    0 Warning(s)
    0 Error(s)
```

You can also use any of the normal `dotnet` parameters to target a specific framework, configuration, and runtime.

### Docker Image

If you don't have the development environment configured or you want to run OSSGadget without additional overhead, you can use Docker. This repository contains a "Dockerfile" which allows us to build an image and use that to run a container with the latest code.

```
# Clone repository
$> git clone https://github.com/microsoft/OSSGadget.git
$> cd OSSGadget

# Build OSSGadget and create a docker image
$> docker build -t ossgadget:latest .

# Run container
$> docker run -it ossgadget:latest /bin/bash

# Inside container - run oss-download binary
root@container:/app/src# ./oss-download/bin/Debug/net5.0/oss-download 
```

For certain tools, like OSS Health, you'll also need to set the `GITHUB_ACCESS_TOKEN` environment variable when you
create the container.

### Advanced Usage

#### Encoding

Certain packages must be encoded when described as a Package URL. For more information see
[Character encoding](https://github.com/package-url/purl-spec#character-encoding) in the Package URL specification.

#### Alternate Endpoints

If you're using an alternate endpoint, such as a corporate NPM mirror, you can set certain environment variables to
redirect traffic to that location instead of the default one. These environment variables include:

* `GITHUB_ACCESS_TOKEN`: When connecting to GitHub, this environment variable must be defined (only used for OSS Health).
* `CARGO_ENDPOINT`: Connect to Cargo (Rust), default value is https://crates.io.
* `CARGO_ENDPOINT_STATIC`: Connect to Cargo (Rust), default value is https://static.crates.io.
* `COCOAPODS_SPECS_ENDPOINT`: Connect to Cocoapods (MacOS / iOS), default value is https://github.com/Cocoapods/Specs/tree/master.
* `COCOAPODS_SPECS_RAW_ENDPOINT`: Connect to Cocoapods (MacOS / iOS), default value is https://raw.githubusercontent.com/CocoaPods/Specs/master.
* `COCOAPODS_METADATA_ENDPOINT`: Connect to Cocoapods (MacOS / iOS), default value is https://cocoapods.org.
* `COMPOSER_ENDPOINT`: Connect to Composer (PHP), default value is https://repo.packagist.org.
* `CPAN_ENDPOINT`: Connect to CPAN (Perl), default value is https://metacpan.org.
* `CPAN_BINARY_ENDPOINT`: Connect to CPAN (Perl), default value is https://cpan.metacpan.org.
* `CRAN_ENDPOINT`: Connect to CRAN (R), default value is https://cran.r-project.org.
* `GO_PROXY_ENDPOINT`: Connect to a Go Proxy, default value is https://proxy.golang.org.
* `GO_PKG_ENDPOINT`: Connect to the Go Package Repository, default value is https://pkg.go.dev.
* `HACKAGE_ENDPOINT`: Connect to Hackage (Haskell), default value is https://hackage.haskell.org.

* `MAVEN_ENDPOINT`: Connect to Maven (Java), default value is https://repo1.maven.org/maven2.
* `NPM_ENDPOINT`: Connect to NPM (JavaScript), default value is https://registry.npmjs.org.
* `NUGET_ENDPOINT_API`: Connect to NuGet (.NET), default value is https://api.nuget.org.
* `PYPI_ENDPOINT`: Connect to PyPI (Python), default value is https://pypi.org.
* `RUBYGEMS_ENDPOINT`: Connect to RubyGems (Ruby), default value is https://rubygems.org.
* `RUBYGEMS_ENDPOINT_API`: Connect to RubyGems (Ruby), default value is https://api.rubygems.org.
* `UBUNTU_ARCHIVE_MIRROR`: Ubuntu archive mirror, default value is https://mirror.math.princeton.edu/pub.
* `UBUNTU_ENDPOINT`: Ubuntu package repository, default value is https://packages.ubuntu.com.
* `UBUTNU_POOL_NAMES`: Ubuntu pools to query, default value is main,universe,multiverse,restricted.
* `VS_MARKETPLACE_ENDPOINT`: Connect to Visual Studio Marketplace, default value is https://marketplace.visualstudio.com.

Please note that for some systems, like Cocoapods, if you change one value, you'll very likely need to change the others.

## Reporting Security Vulnerabilities

To report a security vulnerability, please see [SECURITY.md](SECURITY.md).

## Contributing to OSS Gadget

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
