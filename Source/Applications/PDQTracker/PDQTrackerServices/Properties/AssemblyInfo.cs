using System.Reflection;
using System.Runtime.InteropServices;

// Assembly identity attributes.
[assembly: AssemblyVersion("1.5.13.0")]

// Informational attributes.
[assembly: AssemblyCompany("Grid Protection Alliance")]
[assembly: AssemblyCopyright("Copyright © 2010.  All Rights Reserved.")]
[assembly: AssemblyProduct("PDQTracker")]

// Assembly manifest attributes.
#if DEBUG
[assembly: AssemblyConfiguration("Debug Build")]
#else
[assembly: AssemblyConfiguration("Release Build")]
#endif
[assembly: AssemblyDescription("Web services used by PDQTracker.")]
[assembly: AssemblyTitle("PDQTracker Web Services")]

// Other configuration attributes.
[assembly: ComVisible(false)]
[assembly: Guid("08996eaa-cea3-492f-b3d4-12af2277f17c")]
