// Copyright 2026 The Open Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using com.google.apps.peltzer.client.entitlement;
using NUnit.Framework;

[TestFixture]
public class OAuthRequestSecurityTest
{
    private const string ApiBaseUrl = "https://api.icosa.example/v1";

    [TestCase("https://api.icosa.example/assets/123")]
    [TestCase("https://API.ICOSA.EXAMPLE:443/assets/123")]
    [TestCase("https://api.icosa.example/other-path")]
    public void IsSameOrigin_AcceptsExactOrigin(string requestUrl)
    {
        Assert.True(OAuthRequestSecurity.IsSameOrigin(requestUrl, ApiBaseUrl));
    }

    [TestCase("https://api.icosa.example.attacker.test/resource")]
    [TestCase("https://api.icosa.example@attacker.test/resource")]
    [TestCase("https://cdn.api.icosa.example/resource")]
    [TestCase("https://api.icosa.example:444/resource")]
    [TestCase("http://api.icosa.example/resource")]
    [TestCase("file:///api.icosa.example/resource")]
    [TestCase("not-a-url")]
    [TestCase(null)]
    public void IsSameOrigin_RejectsDifferentOrInvalidOrigin(string requestUrl)
    {
        Assert.False(OAuthRequestSecurity.IsSameOrigin(requestUrl, ApiBaseUrl));
    }

    [Test]
    public void IsSameOrigin_RejectsInvalidTrustedBaseUrl()
    {
        Assert.False(OAuthRequestSecurity.IsSameOrigin(
            "https://api.icosa.example/resource", "not-a-url"));
    }
}
