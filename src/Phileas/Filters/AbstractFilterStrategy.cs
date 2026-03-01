/*
 * Copyright 2024 Philterd, LLC
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

using Phileas.Model.Filtering;
using Phileas.Policy;

namespace Phileas.Filters;

public abstract class AbstractFilterStrategy
{
    public const string Redact = "REDACT";
    public const string RandomReplace = "RANDOM_REPLACE";
    public const string StaticReplace = "STATIC_REPLACE";
    public const string CryptoReplace = "CRYPTO_REPLACE";
    public const string FpeEncryptReplace = "FPE_ENCRYPT_REPLACE";
    public const string HashSha256Replace = "HASH_SHA256_REPLACE";
    public const string Last4 = "LAST_4";
    public const string Mask = "MASK";
    public const string Same = "SAME";
    public const string Truncate = "TRUNCATE";
    public const string DefaultRedaction = "{{{REDACTED-%t}}}";

    public string Strategy { get; set; } = Redact;
    public string RedactionFormat { get; set; } = DefaultRedaction;
    public string StaticReplacement { get; set; } = string.Empty;
    public string MaskCharacter { get; set; } = "*";
    public string MaskLength { get; set; } = "same";
    public string? Condition { get; set; }
    public bool Salt { get; set; } = false;
    public IContextService? ContextService { get; set; }

    public abstract Replacement GetReplacement(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern, Crypto? crypto, Fpe? fpe);

    public abstract bool EvaluateCondition(string context, string token, string[] window, double confidence, string? classification, FilterPattern? filterPattern);

    protected string GetRedactedToken(string token, string? label, FilterType filterType)
    {
        var format = RedactionFormat ?? DefaultRedaction;
        var result = format.Replace("%t", filterType.GetFilterTypeName());
        if (label != null)
            result = result.Replace("%l", label);
        return result;
    }
}
