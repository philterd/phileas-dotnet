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

using System.Text.Json;
using Phileas.Filters.Rules.Regex.RegexFilters;
using Phileas.Filters.Strategies.Rules;
using Phileas.Model;
using Phileas.Policy.Filters;
using Phileas.Services.Validators;
using Xunit;
using static Phileas.Tests.IdentifierSectionTestSupport;

namespace Phileas.Tests;

public class IdentifierValidatorTests
{
    // A throwaway validator used to exercise the dispatch without a real built-in validator.
    private sealed class StubValidator(Func<Span, bool> predicate) : ISpanValidator
    {
        public bool Validate(Span span) => predicate(span);
    }

    private static IdentifierFilter NumberFilter(ISpanValidator? validator) =>
        new(Config(new IdentifierFilterStrategy()), "num", @"\b\d+\b", true, 0, validator);

    // --- Dispatch through the filter ---

    [Fact]
    public void Validator_keeps_only_passing_matches()
    {
        var even = new StubValidator(span => int.Parse(span.Text) % 2 == 0);
        var filtered = NumberFilter(even).Filter(GetPolicy(), "context", Piece, "values 4 and 5 here");
        Assert.Single(filtered.Spans);
        Assert.Equal("4", filtered.Spans[0].Text);
    }

    [Fact]
    public void Validator_dropping_all_yields_no_spans()
    {
        var even = new StubValidator(span => int.Parse(span.Text) % 2 == 0);
        var filtered = NumberFilter(even).Filter(GetPolicy(), "context", Piece, "values 3 and 7 here");
        Assert.Empty(filtered.Spans);
    }

    [Fact]
    public void No_validator_keeps_every_match()
    {
        var filtered = NumberFilter(null).Filter(GetPolicy(), "context", Piece, "values 3 and 4 here");
        Assert.Equal(2, filtered.Spans.Count);
    }

    // --- Registry (IdentifierValidators.FromPolicy) ---

    [Fact]
    public void FromPolicy_null_returns_null()
    {
        Assert.Null(IdentifierValidators.FromPolicy(null));
    }

    [Fact]
    public void FromPolicy_unknown_name_throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => IdentifierValidators.FromPolicy(new Validator("does-not-exist")));
        Assert.Contains("does-not-exist", ex.Message);
    }

    [Fact]
    public void FromPolicy_empty_name_throws()
    {
        Assert.Throws<ArgumentException>(() => IdentifierValidators.FromPolicy(new Validator("   ")));
    }

    // --- Deserialization of the validator field (string and object forms) ---

    [Fact]
    public void Deserializes_string_form()
    {
        var identifier = JsonSerializer.Deserialize<Identifier>("{\"validator\": \"luhn\"}");
        Assert.NotNull(identifier!.Validator);
        Assert.Equal("luhn", identifier.Validator!.Name);
        Assert.Null(identifier.Validator.Params);
    }

    [Fact]
    public void Deserializes_object_form_with_params()
    {
        var identifier = JsonSerializer.Deserialize<Identifier>(
            "{\"validator\": {\"name\": \"mod11\", \"params\": {\"variant\": \"cpf\"}}}");
        Assert.Equal("mod11", identifier!.Validator!.Name);
        Assert.NotNull(identifier.Validator.Params);
        Assert.Equal("cpf", identifier.Validator.Params!["variant"].GetString());
    }

    [Fact]
    public void Deserializes_object_form_without_params()
    {
        var identifier = JsonSerializer.Deserialize<Identifier>("{\"validator\": {\"name\": \"luhn\"}}");
        Assert.Equal("luhn", identifier!.Validator!.Name);
    }

    [Fact]
    public void Absent_validator_is_null()
    {
        var identifier = JsonSerializer.Deserialize<Identifier>("{\"pattern\": \"\\\\d+\"}");
        Assert.Null(identifier!.Validator);
    }

    [Fact]
    public void Round_trips_string_form()
    {
        var identifier = JsonSerializer.Deserialize<Identifier>("{\"validator\": \"luhn\"}");
        var json = JsonSerializer.Serialize(identifier);
        Assert.Contains("\"validator\":\"luhn\"", json);
    }
}
