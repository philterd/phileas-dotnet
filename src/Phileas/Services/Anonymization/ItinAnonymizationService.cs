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

namespace Phileas.Services.Anonymization;

/// <summary>
///     Anonymizes Itin tokens with an ITIN in an IRS-issued range, written in the token's own format: each digit of
///     the token is replaced in order, and its separators are kept.
/// </summary>
public class ItinAnonymizationService : AbstractAnonymizationService
{
    public ItinAnonymizationService(IContextService contextService) : base(contextService) { }

    public ItinAnonymizationService(IContextService contextService, Random random) : base(contextService, random) { }

    public ItinAnonymizationService(IContextService contextService, Random random, AnonymizationMethod method)
        : base(contextService, random, method) { }

    public ItinAnonymizationService(IContextService contextService, Random random, List<string> candidates)
        : base(contextService, random, candidates) { }

    protected override string GenerateRealistic(string token)
    {
        return InTokenFormat(token, DataGenerator.Itin().Random());
    }
}
