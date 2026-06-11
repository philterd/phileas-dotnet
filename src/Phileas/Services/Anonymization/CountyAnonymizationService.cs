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

/// <summary>Anonymizes county tokens by selecting from a fixed pool of county names.</summary>
public class CountyAnonymizationService : AbstractAnonymizationService
{
    public CountyAnonymizationService(IContextService contextService) : base(contextService) { }

    public CountyAnonymizationService(IContextService contextService, Random random) : base(contextService, random) { }

    public CountyAnonymizationService(IContextService contextService, Random random, AnonymizationMethod method)
        : base(contextService, random, method) { }

    public CountyAnonymizationService(IContextService contextService, Random random, List<string> candidates)
        : base(contextService, random, candidates) { }

    private static readonly List<string> Counties = new()
    {
        "Beaver",
        "Ohio",
        "Tallahatchie",
        "Braxton",
        "Orange",
        "Lemhi",
        "Wagoner",
        "Osage",
        "Rensselaer",
        "Meeker",
        "Stark",
        "McCone",
        "Clarion",
        "Spotsylvania",
        "Accomack",
        "Dauphin",
        "Jim Hogg",
        "Prince Edward",
        "Greenville",
        "Tillman",
        "Ravalli",
        "Santa Rosa",
        "Wyandot",
        "Box Butte",
        "Milwaukee",
        "Trinity",
        "Kleberg",
        "Ritchie",
        "Rockland",
        "Miami-Dade",
        "Keya Paha",
        "McCulloch",
        "Meade",
        "Collin",
        "Utah",
        "Breathitt",
        "Allen Parish",
        "Refugio",
        "Jim Wells",
        "Torrance",
        "Lunenburg",
        "Otsego",
        "Bryan",
        "Nueces",
        "Decatur",
        "Sibley",
        "Candler",
        "Del Norte",
        "Aleutians East",
        "Humboldt",
        "Cheboygan",
        "Tom Green",
        "Hodgeman",
        "Benzie",
        "Kidder",
        "Burleigh",
        "Berrien",
        "St. Lucie",
        "Harnett",
        "Sublette",
        "Traverse",
        "Caldwell Parish",
        "Walworth",
        "Kalamazoo",
        "Hamilton",
        "Yellow Medicine",
        "Mora",
        "Sherman",
        "Bethel",
        "Charles City",
        "Daniels",
        "Washington",
        "Dearborn",
        "Solano",
        "Conejos",
        "Elk",
        "Harris",
        "Fremont",
        "Addison",
        "LaGrange",
        "Sarasota",
        "Schuyler",
        "Bacon",
        "Brookings",
        "Androscoggin",
        "Forrest",
        "Smith",
        "Milam",
        "McClain",
        "Labette",
        "Powhatan",
        "Musselshell",
        "Coosa",
        "Kootenai",
        "Parker",
        "Mitchell",
        "Niobrara",
        "Miller",
        "Bingham",
        "Borden"
    };

    protected override string GenerateRealistic(string token)
    {
        return Counties[GenerateInteger(0, Counties.Count - 1)];
    }
}
