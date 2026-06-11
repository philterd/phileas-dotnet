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

namespace Phileas.Data.Generators;

/// <summary>Generates random identifiers matching a pattern: digits become digits, letters become
/// random letters of the same case, and other characters are preserved.</summary>
public class CustomIdGenerator : AbstractGenerator<string>
{
    private readonly Random _random;
    private readonly string? _pattern;

    public CustomIdGenerator(string? pattern) : this(new Random(), pattern) { }

    public CustomIdGenerator(Random random, string? pattern)
    {
        _random = random;
        _pattern = pattern;
    }

    public override string Random()
    {
        if (_pattern == null) return null!;

        var sb = new System.Text.StringBuilder();
        foreach (var c in _pattern)
        {
            if (char.IsDigit(c))
            {
                sb.Append(_random.Next(10));
            }
            else if (char.IsLetter(c))
            {
                sb.Append(char.IsUpper(c) ? (char)('A' + _random.Next(26)) : (char)('a' + _random.Next(26)));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public override long PoolSize()
    {
        if (_pattern == null) return 0;

        double poolSize = 1;
        foreach (var c in _pattern)
        {
            if (char.IsDigit(c)) poolSize *= 10;
            else if (char.IsLetter(c)) poolSize *= 26;
        }
        return (long)poolSize;
    }
}
