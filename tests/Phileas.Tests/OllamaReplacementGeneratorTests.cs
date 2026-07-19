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

using System.Net;
using System.Text;
using Phileas.Policy;
using Phileas.Services.Generators;
using Xunit;

namespace Phileas.Tests;

public class OllamaReplacementGeneratorTests
{
    private static Generator NewGenerator(string? prompt = "Rewrite {{token}} (a {{label}}).")
    {
        return new Generator
        {
            Type = Generator.TypeOllama,
            Endpoint = "http://localhost:11434",
            Model = "llama3.1",
            Prompt = prompt,
            TimeoutMs = 5000
        };
    }

    [Fact]
    public void Generate_ReturnsTrimmedResponseField()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{\"response\": \"  Joe's Ice Cream Shop \\n\"}");
        var generator = new OllamaReplacementGenerator(NewGenerator(), new HttpClient(handler));

        Assert.Equal("Joe's Ice Cream Shop", generator.Generate("Jon's Ice Cream Shop", "business"));
    }

    [Fact]
    public void Generate_SubstitutesTokenAndLabelIntoPrompt()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{\"response\": \"ok\"}");
        var generator = new OllamaReplacementGenerator(NewGenerator(), new HttpClient(handler));

        generator.Generate("Jane Doe", "name");

        Assert.Contains("Rewrite Jane Doe (a name).", handler.LastRequestBody);
        Assert.Contains("\"stream\":false", handler.LastRequestBody.Replace(" ", ""));
    }

    [Fact]
    public void Generate_NonSuccessStatus_Throws()
    {
        var handler = new FakeHandler(HttpStatusCode.InternalServerError, "error");
        var generator = new OllamaReplacementGenerator(NewGenerator(), new HttpClient(handler));

        Assert.ThrowsAny<Exception>(() => generator.Generate("x", "y"));
    }

    [Fact]
    public void Generate_MissingResponseField_Throws()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{\"done\": true}");
        var generator = new OllamaReplacementGenerator(NewGenerator(), new HttpClient(handler));

        Assert.ThrowsAny<Exception>(() => generator.Generate("x", "y"));
    }

    [Fact]
    public void Generate_BlankResponse_Throws()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, "{\"response\": \"   \"}");
        var generator = new OllamaReplacementGenerator(NewGenerator(), new HttpClient(handler));

        Assert.ThrowsAny<Exception>(() => generator.Generate("x", "y"));
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public string LastRequestBody { get; private set; } = string.Empty;

        public FakeHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content != null)
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
