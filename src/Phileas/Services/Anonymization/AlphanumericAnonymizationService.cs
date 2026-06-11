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
namespace Phileas.Services.Anonymization;

/// <summary>Anonymizes Alphanumeric tokens.</summary>
public class AlphanumericAnonymizationService : AbstractAnonymizationService
{
    public AlphanumericAnonymizationService(IContextService contextService) : base(contextService) { }

    public AlphanumericAnonymizationService(IContextService contextService, Random random) : base(contextService, random) { }

    public AlphanumericAnonymizationService(IContextService contextService, Random random, AnonymizationMethod method)
        : base(contextService, random, method) { }

    public AlphanumericAnonymizationService(IContextService contextService, Random random, List<string> candidates)
        : base(contextService, random, candidates) { }

    protected override string GenerateRealistic(string token)
    {
        var sb = new StringBuilder();
        foreach (var c in token)
        {
            if (char.IsDigit(c)) sb.Append(Random.Next(10));
            else if (char.IsLetter(c)) sb.Append(GenerateAlphanumeric(1));
            else if (c == ' ') sb.Append(' ');
            else if (c == '_') sb.Append('_');
            else if (c == '-') sb.Append('-');
            else if (c == '.') sb.Append('.');
            else sb.Append(Random.Next(10));
        }
        return sb.ToString();
    }
}
