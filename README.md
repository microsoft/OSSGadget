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

This will download left-pad into a newly-created directory named `npm-left-pad@1.3.0`. (Because, at the time of this writing, 1.3.0 was the latest version of [left-pad](https://www.npmjs.com/package/left-pad)).

Each of the programs self-documents information on command line options (`--help`).

### Detailed Tool Information

#### OSS Characteristics

OSS Characteristics is a thin wrapper on top of [Application Inspector](https://github.com/Microsoft/ApplicationInspector).
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
OSS Gadget builds with standard `dotnet build` commands.

See https://github.com/microsoft/OSSGadget/wiki/Building-from-Source for information on building from source.

### Docker Image
See https://github.com/microsoft/OSSGadget/wiki/Docker-Image for information on how to use the included Dockerfile.

### Advanced Usage
See https://github.com/microsoft/OSSGadget/wiki/Advanced-Usage for advanced usage information like changing API endpoints.

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
