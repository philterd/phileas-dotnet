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

namespace Phileas.Services.Split;

/// <summary>
///     A piece of a split document together with the absolute character offset in the original input
///     at which it begins, so spans detected in the piece can be mapped back to the input.
/// </summary>
/// <param name="Text">The piece's text.</param>
/// <param name="Offset">The piece's start offset in the original input.</param>
public readonly record struct TextSplit(string Text, int Offset);
