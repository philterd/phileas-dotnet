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

/// <summary>Anonymizes PassportNumber tokens.</summary>
public class PassportNumberAnonymizationService : AbstractAnonymizationService
{
    public PassportNumberAnonymizationService(IContextService contextService) : base(contextService) { }

    public PassportNumberAnonymizationService(IContextService contextService, Random random) : base(contextService, random) { }

    public PassportNumberAnonymizationService(IContextService contextService, Random random, AnonymizationMethod method)
        : base(contextService, random, method) { }

    public PassportNumberAnonymizationService(IContextService contextService, Random random, List<string> candidates)
        : base(contextService, random, candidates) { }

    private string AnonymizedMacAddress()
    {
        var mac = new byte[6];
        Random.NextBytes(mac);
        var sb = new StringBuilder(18);
        foreach (var b in mac)
        {
            if (sb.Length > 0) sb.Append(':');
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }

    protected override string GenerateRealistic(string token)
    {
        return AnonymizedMacAddress();
    }
}
