﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace DotNet.IO;

public sealed class DockerfileReader : IDockerfileReader
{
    static readonly RegexOptions _options =
        RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.ExplicitCapture;
    static readonly Regex _fromInstructionRegex =
        new(@"FROM (?<image>.+?dotnet.+?):(?<tag>.+?)[\s|\n]", _options);
    static readonly Regex _copyInstructionRegex =
        new(@"COPY --from=(?<image>.+?dotnet.+?):(?<tag>.+?)[\s|\n]", _options);

    public async ValueTask<Dockerfile> ReadDockerfileAsync(string dockerfilePath)
    {
        Dockerfile dockerfile = new()
        {
            FullPath = dockerfilePath
        };
        
        if (SystemFile.Exists(dockerfilePath))
        {
            string dockerfileContent = await SystemFile.ReadAllTextAsync(dockerfilePath);
            MatchCollection fromMatches = _fromInstructionRegex.Matches(dockerfileContent);
            foreach (Match match in fromMatches)
            {
                dockerfile = ParseMatch(dockerfile, dockerfileContent, match);
            }

            MatchCollection copyMatches = _copyInstructionRegex.Matches(dockerfileContent);
            foreach (Match match in copyMatches)
            {
                dockerfile = ParseMatch(dockerfile, dockerfileContent, match);
            }
        }

        return dockerfile;

        static Dockerfile ParseMatch(Dockerfile dockerfile, string dockerfileContent, Match match)
        {
            Group group = match.Groups["tag"];
            (int index, string tag) = (group.Index, group.Value);
            string? image = match.Groups["image"].Value;
            if (image is not null && tag is not null)
            {
                if (dockerfile.ImageDetails is null)
                {
                    dockerfile = dockerfile with
                    {
                        ImageDetails = new HashSet<ImageDetails>()
                    };
                }

                int lineNumber = GetLineNumberFromIndex(dockerfileContent, index);
                bool isFramework = image.Contains("framework");
                tag = tag.Contains('-') ? tag.Split("-")[0] : tag;
                int firstNumber = int.TryParse(tag[0].ToString(), out int number) ? number : -1;
                string tfm = isFramework switch
                {
                    true => $"net{tag.Replace(".", "")}",
                    false when firstNumber < 4 => $"netcoreapp{tag}",
                    _ => $"net{tag}"
                };

                dockerfile.ImageDetails.Add(
                    new ImageDetails(image, tag, tfm, lineNumber));
            }

            return dockerfile;
        }
    }

    static int GetLineNumberFromIndex(string content, int index)
    {
        int lineNumber = 1;
        for (int i = 0; i < index; ++i)
        {
            if (content[i] == '\n') ++lineNumber;
        }
        return lineNumber;
    }
}
