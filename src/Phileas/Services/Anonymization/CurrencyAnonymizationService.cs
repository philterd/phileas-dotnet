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

/// <summary>Anonymizes Currency tokens.</summary>
public class CurrencyAnonymizationService : AbstractAnonymizationService
{
    public CurrencyAnonymizationService(IContextService contextService) : base(contextService) { }

    public CurrencyAnonymizationService(IContextService contextService, Random random) : base(contextService, random) { }

    public CurrencyAnonymizationService(IContextService contextService, Random random, AnonymizationMethod method)
        : base(contextService, random, method) { }

    public CurrencyAnonymizationService(IContextService contextService, Random random, List<string> candidates)
        : base(contextService, random, candidates) { }

    protected override string GenerateRealistic(string token)
    {
        var sb = new StringBuilder();
        foreach (var c in token)
        {
            if (char.IsDigit(c)) sb.Append(Random.Next(10));
            else sb.Append(c);
        }
        return sb.ToString().StartsWith("$") ? sb.ToString() : "$" + sb;
    }
}
