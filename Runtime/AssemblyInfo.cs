using System.Runtime.CompilerServices;

// Lets the EditMode test assembly drive the internal backend/dispatcher seams.
[assembly: InternalsVisibleTo("AppCat.Tests")]
