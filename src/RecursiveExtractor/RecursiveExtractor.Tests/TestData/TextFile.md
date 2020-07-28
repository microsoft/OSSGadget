## RecursiveExtractor

RecursiveExtractor is a general-purpose file extractor.

### Format Support

RecursiveExtractor supports extracting the following types of archives:

* GNU AR
* BZip2
* [deb](https://en.wikipedia.org/wiki/Deb_(file_format))
* ISO
* tar
* VHD
* VHDX
* VMDK
* WIM
* XZip
* zip

## Using RecursiveExtractor

To use RecursiveExtractor, just instantiate an `Extractor` object and call the `ExtractFile`
method with either a filename or a byte array. This method will return an IEnumerable
of FileEntry objects, each one of which will contain the name of the file and its 
contents, plus some additional metadata. 

```
using Microsoft.CST.RecursiveExtractor;

...

// Initialize the RecursiveExtractor extractor
var extractor = new Extractor();

// Extract from an existing file
foreach (var fileEntry in extractor.ExtractFile("test.zip"))
{
    Console.WriteLine(fileEntry.FullPath);
}

// Extract from a byte array
byte[] bytes = ...;
// The "nonexistent.zip" name doesn't really matter, but is used as part of the
// FileEntry.FullPath string.
foreach (var fileEntry in extractor.ExtractFile("nonexistent.zip", bytes))
{
    Console.WriteLine(fileEntry.FullPath);
}
```

## Issues

If you find any issues with RecursiveExtractor, please [open an issue](https://github.com/Microsoft/OSSGadget/issues/new)
in the [Microsoft/OSSGadget](https://github.com/Microsoft/OSSGadget) repository.


