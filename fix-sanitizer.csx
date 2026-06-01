#!/usr/bin/env dotnet-script

var path = "src/Modules/Music/DotNetCloud.Modules.Music.Data/Services/MusicBrainzClient.cs";
var lines = File.ReadAllLines(path).ToList();

// Find and remove the Lucene escape block (currently too early)
int luceneIdx = -1;
for (int i = 0; i < lines.Count; i++)
{
    if (lines[i].Contains("Escape Lucene special characters"))
    {
        luceneIdx = i;
        break;
    }
}

if (luceneIdx < 0)
{
    Console.Error.WriteLine("Could not find Lucene escape block");
    return;
}

// Remove the 3 lines (comment, comment, code)
var luceneBlock = new[] { lines[luceneIdx], lines[luceneIdx + 1], lines[luceneIdx + 2] };
lines.RemoveRange(luceneIdx, 3);

// Find where to insert it: after TrailingVolumeNumber, before "return cleaned;"
int insertIdx = -1;
for (int i = 0; i < lines.Count; i++)
{
    if (lines[i].Contains("return cleaned;"))
    {
        insertIdx = i;
        break;
    }
}

if (insertIdx < 0)
{
    Console.Error.WriteLine("Could not find return cleaned;");
    return;
}

// Update the comment
luceneBlock[0] = "        // Escape Lucene special characters LAST — stripping must happen first so";
luceneBlock[1] = "        // hyphens and periods in volume numbers aren't escaped before regex matching.";
lines.InsertRange(insertIdx, luceneBlock);

File.WriteAllLines(path, lines);
Console.WriteLine("Done. Sanitizer order fixed.");
