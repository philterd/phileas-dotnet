/*
 * Copyright 2026 Philterd, LLC
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Text;
using Phileas.Services.Split;
using Xunit;

namespace Phileas.Tests.Split;

public class SplitServiceTests
{
    private static string ReadResource(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Resources", fileName));
    }

    [Fact]
    public void NewLine_SimpleTest()
    {
        var splits = new NewLineSplitService().Split(ReadResource("simple-test.txt"));
        Assert.NotEmpty(splits);
        Assert.All(splits, s => Assert.False(string.IsNullOrWhiteSpace(s)));
    }

    [Fact]
    public void NewLine_Alice()
    {
        var splits = new NewLineSplitService().Split(ReadResource("alice29.txt"));
        Assert.Equal(2732, splits.Count);
    }

    [Fact]
    public void NewLine_AliceFormatted()
    {
        var splits = new NewLineSplitService().Split(ReadResource("alice29-formatted.txt"));
        Assert.Equal(6, splits.Count);
    }

    [Fact]
    public void LineWidth_SplitsAndReassembles()
    {
        const int splitLength = 384;
        var input = ReadResource("simple-test.txt");
        var splitService = new LineWidthSplitService(splitLength);

        var splits = splitService.Split(input);

        Assert.All(splits, s => Assert.True(s.Length <= splitLength));

        var sb = new StringBuilder();
        foreach (var split in splits)
        {
            sb.Append(split).Append(splitService.GetSeparator());
        }

        Assert.Equal(input.Trim(), sb.ToString().Trim());
    }

    [Fact]
    public void CharacterCount_SplitsAndReassembles()
    {
        const int splitLength = 250;
        var input = ReadResource("simple-test.txt");
        var splitService = new CharacterCountSplitService(splitLength);

        var splits = splitService.Split(input);

        Assert.All(splits, s => Assert.True(s.Length <= splitLength));

        var sb = new StringBuilder();
        foreach (var split in splits)
        {
            sb.Append(split).Append(splitService.GetSeparator());
        }

        Assert.Equal(input.Trim(), sb.ToString().Trim());
    }

    [Fact]
    public void Factory_SelectsServiceByMethod()
    {
        Assert.IsType<NewLineSplitService>(SplitFactory.GetSplitService("newline", 100));
        Assert.IsType<LineWidthSplitService>(SplitFactory.GetSplitService("width", 100));
        Assert.IsType<CharacterCountSplitService>(SplitFactory.GetSplitService("characters", 100));
        Assert.IsType<NewLineSplitService>(SplitFactory.GetSplitService("unknown", 100));
    }
}
