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

/// <summary>
///     Generates random realistic values of type <typeparamref name="T" /> for synthetic data and for
///     the <c>RANDOM_REPLACE</c> anonymization strategy.
/// </summary>
/// <typeparam name="T">The type of value produced.</typeparam>
public interface IGenerator<out T>
{
    /// <summary>Returns a single random value.</summary>
    T Random();

    /// <summary>Returns the size of the value pool this generator draws from.</summary>
    long PoolSize();
}
