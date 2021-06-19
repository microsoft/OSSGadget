# OSS Reproducible

The purpose of *oss-reproducible* is to analyze open source packages for reproducibility. We start with
an existing package (for example, the NPM "left-pad" package, version 1.3.0), and we try to answer the
question, **Do the package contents authentically reflect the purported source code?**

## Why Reproducibility?

## How It Works

We start by downloading the package from the respective package repository, using OSS Download. Then
we look for the source (using OSS Find Source) and try to find and download the correct version of it.

OSS Reproducible uses "strategies" to try to correlate source code with the package contents. These
strategies include:

* PackageMatchesSourceStrategy: Do the package contents exactly match the source code repository
  contents?
* PackageContainedInSourceStragegy: Does every file in the package exist in the source code repository?
  Note that this is different from the previous strategy; for example, unit tests in the source repository
  that don't appear in the package would be fine for this strategy.
* AutoBuildProducesSamePackage: Can we re-build the package from the source code repository? Since
  there is a huge variety in build methods, this can very difficult, or in many cases, impossible.
* OryxBuildStrategy: The Microsoft Oryx project has independent logic to attempt to "build" source
  code repositories, so we leverage that as well.

## Ignoring Files

Certain files are uninteresting from a reproducibility perspective, and are excluded from analyses.
These files are defined in Strategies/PackageIgnoreList.txt.

## Contributing to OSS Reproducible

### Fixing a Failing Build

If a project requires a custom build command or setup, you can add this to OSS Reproducible by
creating a file in BuildHelperScripts/(Package Manager)/(Project Name).{build, prebuild}.
