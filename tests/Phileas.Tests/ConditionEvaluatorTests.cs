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

using Phileas.Filters.Conditions;
using Phileas.Services;
using Xunit;

namespace Phileas.Tests;

public class ConditionEvaluatorTests
{
    [Fact]
    public void EmptyCondition_ReturnsTrue()
    {
        var result = ConditionEvaluator.Evaluate("", "ctx", "token", 0.9, null);
        Assert.True(result);
    }

    [Fact]
    public void NullCondition_ReturnsTrue()
    {
        var result = ConditionEvaluator.Evaluate(null!, "ctx", "token", 0.9, null);
        Assert.True(result);
    }

    [Theory]
    [InlineData("token == \"test\"", "test", true)]
    [InlineData("token == \"test\"", "other", false)]
    [InlineData("token is \"test\"", "test", true)]
    [InlineData("token is \"test\"", "other", false)]
    [InlineData("token != \"test\"", "test", false)]
    [InlineData("token != \"test\"", "other", true)]
    [InlineData("token is not \"test\"", "test", false)]
    [InlineData("token is not \"test\"", "other", true)]
    public void TokenEquality_EvaluatesCorrectly(string condition, string token, bool expected)
    {
        var result = ConditionEvaluator.Evaluate(condition, "ctx", token, 0.9, null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("token startswith \"test\"", "test123", true)]
    [InlineData("token startswith \"test\"", "123test", false)]
    [InlineData("token startswith \"TEST\"", "test123", true)]
    [InlineData("token startswith \"abc\"", "test", false)]
    public void TokenStartsWith_EvaluatesCorrectly(string condition, string token, bool expected)
    {
        var result = ConditionEvaluator.Evaluate(condition, "ctx", token, 0.9, null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("confidence > 0.8", 0.9, true)]
    [InlineData("confidence > 0.8", 0.7, false)]
    [InlineData("confidence < 0.5", 0.4, true)]
    [InlineData("confidence < 0.5", 0.6, false)]
    [InlineData("confidence >= 0.9", 0.9, true)]
    [InlineData("confidence >= 0.9", 0.89, false)]
    [InlineData("confidence <= 0.5", 0.5, true)]
    [InlineData("confidence <= 0.5", 0.51, false)]
    [InlineData("confidence == 0.95", 0.95, true)]
    [InlineData("confidence == 0.95", 0.94, false)]
    [InlineData("confidence != 0.5", 0.4, true)]
    [InlineData("confidence != 0.5", 0.5, false)]
    public void ConfidenceComparison_EvaluatesCorrectly(string condition, double confidence, bool expected)
    {
        var result = ConditionEvaluator.Evaluate(condition, "ctx", "token", confidence, null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("context == \"test-ctx\"", "test-ctx", true)]
    [InlineData("context == \"test-ctx\"", "other-ctx", false)]
    [InlineData("context is \"test-ctx\"", "test-ctx", true)]
    [InlineData("context is \"test-ctx\"", "other-ctx", false)]
    [InlineData("context != \"test-ctx\"", "other-ctx", true)]
    [InlineData("context != \"test-ctx\"", "test-ctx", false)]
    [InlineData("context startswith \"test\"", "test-ctx", true)]
    [InlineData("context startswith \"test\"", "other-ctx", false)]
    public void ContextComparison_EvaluatesCorrectly(string condition, string context, bool expected)
    {
        var result = ConditionEvaluator.Evaluate(condition, context, "token", 0.9, null);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("type == \"PER\"", "PER", true)]
    [InlineData("type == \"PER\"", "LOC", false)]
    [InlineData("type == \"per\"", "PER", true)]
    [InlineData("type is \"LOC\"", "LOC", true)]
    [InlineData("type is \"LOC\"", "PER", false)]
    [InlineData("type != \"PER\"", "LOC", true)]
    [InlineData("type != \"PER\"", "PER", false)]
    [InlineData("type is not \"PER\"", "LOC", true)]
    [InlineData("type is not \"PER\"", "PER", false)]
    public void TypeComparison_EvaluatesCorrectly(string condition, string? classification, bool expected)
    {
        var result = ConditionEvaluator.Evaluate(condition, "ctx", "token", 0.9, classification);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TypeComparison_WithNullClassification_HandlesCorrectly()
    {
        var result = ConditionEvaluator.Evaluate("type != \"PER\"", "ctx", "token", 0.9, null);
        Assert.True(result);

        result = ConditionEvaluator.Evaluate("type == \"PER\"", "ctx", "token", 0.9, null);
        Assert.False(result);
    }

    [Theory]
    [InlineData("confidence > 0.8 and token == \"test\"", 0.9, "test", true)]
    [InlineData("confidence > 0.8 and token == \"test\"", 0.7, "test", false)]
    [InlineData("confidence > 0.8 and token == \"test\"", 0.9, "other", false)]
    [InlineData("confidence > 0.8 and token == \"test\"", 0.7, "other", false)]
    public void AndCondition_EvaluatesCorrectly(string condition, double confidence, string token, bool expected)
    {
        var result = ConditionEvaluator.Evaluate(condition, "ctx", token, confidence, null);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ComplexCondition_MultipleAnd_EvaluatesCorrectly()
    {
        var condition = "confidence > 0.5 and token startswith \"test\" and context == \"ctx1\"";

        var result = ConditionEvaluator.Evaluate(condition, "ctx1", "test123", 0.9, null);
        Assert.True(result);

        result = ConditionEvaluator.Evaluate(condition, "ctx2", "test123", 0.9, null);
        Assert.False(result);

        result = ConditionEvaluator.Evaluate(condition, "ctx1", "other", 0.9, null);
        Assert.False(result);

        result = ConditionEvaluator.Evaluate(condition, "ctx1", "test123", 0.3, null);
        Assert.False(result);
    }

    [Fact]
    public void Population_AlwaysReturnsTrue()
    {
        // Population is not supported in phileas-net
        var result = ConditionEvaluator.Evaluate("population > 20000", "ctx", "12345", 0.9, null);
        Assert.True(result);

        result = ConditionEvaluator.Evaluate("population < 1000", "ctx", "12345", 0.9, null);
        Assert.True(result);
    }

    [Theory]
    [InlineData("CONFIDENCE > 0.8")]
    [InlineData("TOKEN == \"test\"")]
    [InlineData("Context startswith \"test\"")]
    [InlineData("confidence > 0.5 AND token == \"test\"")]
    public void CaseInsensitive_EvaluatesCorrectly(string condition)
    {
        // All keywords should be case-insensitive
        var result = ConditionEvaluator.Evaluate(condition, "test-ctx", "test", 0.9, null);
        Assert.True(result);
    }

    [Theory]
    [InlineData("invalid condition")]
    [InlineData("token")]
    [InlineData("== \"test\"")]
    [InlineData("token == ")]
    public void InvalidCondition_ReturnsTrue(string condition)
    {
        // Invalid conditions should default to true
        var result = ConditionEvaluator.Evaluate(condition, "ctx", "token", 0.9, null);
        Assert.True(result);
    }
}
