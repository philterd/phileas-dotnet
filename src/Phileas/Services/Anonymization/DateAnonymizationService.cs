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

using System.Globalization;
using System.Text.RegularExpressions;

namespace Phileas.Services.Anonymization;

/// <summary>Anonymizes date tokens by generating a random date in the same format as the input.</summary>
public class DateAnonymizationService : AbstractAnonymizationService
{
    private static readonly Regex DateYyyyMmDd = new(@"\b\d{4}-\d{2}-\d{2}\b");
    private static readonly Regex DateMmDdYyyy = new(@"\b\d{2}-\d{2}-\d{4}\b");
    private static readonly Regex DateMdYyyy = new(@"\b\d{1,2}-\d{1,2}-\d{2,4}\b");
    private static readonly Regex DateMonth = new(
        @"(?i)(\b\d{1,2}\D{0,3})?\b(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|(Nov|Dec)(?:ember)?)\D?(\d{1,2}(\D?(st|nd|rd|th))?\D?)(\D?((19[7-9]\d|20\d{2})|\d{2}))?\b",
        RegexOptions.IgnoreCase);

    public DateAnonymizationService(IContextService contextService) : base(contextService) { }

    public DateAnonymizationService(IContextService contextService, Random random) : base(contextService, random) { }

    public DateAnonymizationService(IContextService contextService, Random random, AnonymizationMethod method)
        : base(contextService, random, method) { }

    public DateAnonymizationService(IContextService contextService, Random random, List<string> candidates)
        : base(contextService, random, candidates) { }

    protected override string GenerateRealistic(string token)
    {
        var date = GetRandomDate();
        if (DateYyyyMmDd.IsMatch(token)) return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (DateMmDdYyyy.IsMatch(token)) return date.ToString("MM-dd-yyyy", CultureInfo.InvariantCulture);
        if (DateMdYyyy.IsMatch(token)) return date.ToString("M-d-yyyy", CultureInfo.InvariantCulture);
        if (DateMonth.IsMatch(token)) return date.ToString("MMMM dd, yyyy", CultureInfo.InvariantCulture);
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private DateOnly GetRandomDate()
    {
        var minDay = new DateOnly(1900, 1, 1).DayNumber;
        var maxDay = new DateOnly(DateTime.Today.Year, 1, 1).DayNumber;
        return DateOnly.FromDayNumber(minDay + Random.Next(maxDay - minDay));
    }
}
