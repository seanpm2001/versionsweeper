﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace DotNet.IO;

public sealed class SolutionFileReader : ISolutionFileReader
{
    static readonly RegexOptions _options =
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture;
    static readonly Regex _solutionProjectsExpression =
        new("^Project\\(\"{(?<TypeId>[A-F0-9-]+)}\"\\) = " +
            "\"(?<Name>.*?)\", " +
            "\"(?<Path>.*?)\", " +
            "\"{(?<Id>[A-F0-9-]+)}\"" +
            @"(?<Sections>(.|\n|\r)*?)" +
            @"EndProject(\n|\r)",
            _options);

    readonly IProjectFileReader _projectFileReader;

    public SolutionFileReader(IProjectFileReader projectFileReader) =>
        _projectFileReader = projectFileReader;

    public async ValueTask<Solution> ReadSolutionAsync(string solutionPath)
    {
        Solution solution = new();

        if (SystemFile.Exists(solutionPath))
        {
            solution.FullPath = Path.GetFullPath(solutionPath);
            string? solutionDirectory = Path.GetDirectoryName(solution.FullPath);
            string solutionText = await SystemFile.ReadAllTextAsync(solution.FullPath);
            MatchCollection matches = _solutionProjectsExpression.Matches(solutionText);

            foreach (Match match in matches)
            {
                string path = match.Groups["Path"].Value;
                string fullPath = Path.Combine(solutionDirectory!, path);

                if (SystemFile.Exists(fullPath) is false ||
                    SystemFile.GetAttributes(fullPath).HasFlag(FileAttributes.Directory))
                {
                    continue;
                }

                Project project = await _projectFileReader.ReadProjectAsync(fullPath);
                solution.Projects.Add(project);
            }
        }

        return solution;
    }
}
