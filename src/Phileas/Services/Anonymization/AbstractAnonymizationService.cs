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
using Phileas.Data;

namespace Phileas.Services.Anonymization;

/// <summary>
///     Base class for anonymization services. Handles the shared dispatch across the FROM_LIST, UUID and
///     REALISTIC methods (each value is regenerated until it differs from the token); subclasses supply
///     the realistic replacement via <see cref="GenerateRealistic" />. Mirrors the Java
///     <c>AbstractAnonymizationService</c>.
/// </summary>
public abstract class AbstractAnonymizationService : IAnonymizationService
{
    private const string AlphanumericChars =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    /// <summary>The context service used for referential-integrity replacements.</summary>
    protected readonly IContextService ContextService;

    /// <summary>The random source.</summary>
    protected readonly Random Random;

    /// <summary>The candidate values used by the FROM_LIST method.</summary>
    protected readonly List<string> Candidates;

    /// <summary>The anonymization method in effect.</summary>
    protected readonly AnonymizationMethod Method;

    private DefaultDataGenerator? _dataGenerator;

    /// <summary>The lazily-created fake-data generator, shared by realistic replacements.</summary>
    protected DefaultDataGenerator DataGenerator => _dataGenerator ??= new DefaultDataGenerator(Random);

    /// <summary>Creates a REALISTIC service backed by a fresh random source.</summary>
    protected AbstractAnonymizationService(IContextService contextService)
        : this(contextService, new Random(), AnonymizationMethod.Realistic, new List<string>())
    {
    }

    /// <summary>Creates a REALISTIC service backed by the supplied random source.</summary>
    protected AbstractAnonymizationService(IContextService contextService, Random random)
        : this(contextService, random, AnonymizationMethod.Realistic, new List<string>())
    {
    }

    /// <summary>Creates a service using the given method.</summary>
    protected AbstractAnonymizationService(IContextService contextService, Random random,
        AnonymizationMethod method)
        : this(contextService, random, method, new List<string>())
    {
    }

    /// <summary>Creates a FROM_LIST service drawing from the given candidates.</summary>
    protected AbstractAnonymizationService(IContextService contextService, Random random,
        List<string> candidates)
        : this(contextService, random, AnonymizationMethod.FromList, candidates)
    {
    }

    private AbstractAnonymizationService(IContextService contextService, Random random,
        AnonymizationMethod method, List<string> candidates)
    {
        ContextService = contextService;
        Random = random;
        Method = method;
        Candidates = candidates;
    }

    /// <inheritdoc />
    public IContextService GetContextService() => ContextService;

    /// <inheritdoc />
    public string Anonymize(string token)
    {
        if (Method == AnonymizationMethod.FromList)
        {
            if (Candidates.Count > 0)
            {
                var anonymized = Candidates[Random.Next(Candidates.Count)];
                while (string.Equals(anonymized, token, StringComparison.OrdinalIgnoreCase))
                {
                    anonymized = Candidates[Random.Next(Candidates.Count)];
                }
                return anonymized;
            }

            return Guid.NewGuid().ToString();
        }

        if (Method == AnonymizationMethod.Uuid)
        {
            return Guid.NewGuid().ToString();
        }

        // REALISTIC
        var realistic = GenerateRealistic(token);
        while (string.Equals(realistic, token, StringComparison.OrdinalIgnoreCase))
        {
            realistic = GenerateRealistic(token);
        }
        return realistic;
    }

    /// <summary>Produces a single realistic replacement for <paramref name="token" />.</summary>
    protected abstract string GenerateRealistic(string token);

    /// <summary>Returns a random integer in <c>[min, max]</c> (inclusive).</summary>
    protected int GenerateInteger(int min, int max) => Random.Next(min, max + 1);

    /// <summary>
    ///     Returns a string of <paramref name="length" /> numeric characters. Mirrors the Java helper,
    ///     whose single-character range produces all zeros.
    /// </summary>
    protected string GenerateNumeric(int length) => new('0', length);

    /// <summary>Returns a random alphanumeric string of the given length.</summary>
    protected string GenerateAlphanumeric(int length)
    {
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            sb.Append(AlphanumericChars[Random.Next(AlphanumericChars.Length)]);
        }
        return sb.ToString();
    }
}
