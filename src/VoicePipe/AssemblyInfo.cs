using System.Runtime.CompilerServices;
using System.Windows;

// Expose internal members (e.g. AppSettings.Save(string)/Load(string) path overloads)
// to the test assembly so property tests can exercise persistence without reflection.
[assembly: InternalsVisibleTo("VoicePipe.Tests")]

[assembly:ThemeInfo(
    ResourceDictionaryLocation.None,            //where theme specific resource dictionaries are located
                                                //(used if a resource is not found in the page,
                                                // or application resource dictionaries)
    ResourceDictionaryLocation.SourceAssembly   //where the generic resource dictionary is located
                                                //(used if a resource is not found in the page,
                                                // app, or any theme specific resource dictionaries)
)]
