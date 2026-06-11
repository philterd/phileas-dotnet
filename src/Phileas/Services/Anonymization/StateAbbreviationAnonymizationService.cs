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

/// <summary>Anonymizes state-abbreviation tokens by selecting from the fixed pool of US states.</summary>
public class StateAbbreviationAnonymizationService : AbstractAnonymizationService
{
    public StateAbbreviationAnonymizationService(IContextService contextService) : base(contextService) { }

    public StateAbbreviationAnonymizationService(IContextService contextService, Random random) : base(contextService, random) { }

    public StateAbbreviationAnonymizationService(IContextService contextService, Random random, AnonymizationMethod method)
        : base(contextService, random, method) { }

    public StateAbbreviationAnonymizationService(IContextService contextService, Random random, List<string> candidates)
        : base(contextService, random, candidates) { }

    private static readonly List<string> States = new()
    {
        "AL",
        "AK",
        "AZ",
        "AR",
        "CA",
        "CO",
        "CT",
        "DE",
        "FL",
        "GA",
        "HI",
        "ID",
        "IL",
        "IN",
        "IA",
        "KS",
        "KY",
        "LA",
        "ME",
        "MD",
        "MA",
        "MI",
        "MN",
        "MS",
        "MO",
        "MT",
        "NE",
        "NV",
        "NH",
        "NJ",
        "NM",
        "NY",
        "NC",
        "ND",
        "OH",
        "OK",
        "OR",
        "PA",
        "RI",
        "SC",
        "SD",
        "TN",
        "TX",
        "UT",
        "VT",
        "VA",
        "WA",
        "WV",
        "WI",
        "WY"
    };

    protected override string GenerateRealistic(string token)
    {
        return States[GenerateInteger(0, States.Count - 1)];
    }
}
