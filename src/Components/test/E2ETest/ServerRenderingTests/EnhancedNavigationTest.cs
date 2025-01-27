// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures;
using Microsoft.AspNetCore.Components.E2ETest.Infrastructure;
using Microsoft.AspNetCore.E2ETesting;
using TestServer;
using Xunit.Abstractions;
using Components.TestServer.RazorComponents;
using OpenQA.Selenium;
using System.Globalization;

namespace Microsoft.AspNetCore.Components.E2ETests.ServerRenderingTests;

public class EnhancedNavigationTest : ServerTestBase<BasicTestAppServerSiteFixture<RazorComponentEndpointsStartup<App>>>
{
    public EnhancedNavigationTest(
        BrowserFixture browserFixture,
        BasicTestAppServerSiteFixture<RazorComponentEndpointsStartup<App>> serverFixture,
        ITestOutputHelper output)
        : base(browserFixture, serverFixture, output)
    {
    }

    // One of the tests here makes use of the streaming rendering page, which uses global state
    // so we can't run at the same time as other such tests
    public override Task InitializeAsync()
        => InitializeAsync(BrowserFixture.StreamingContext);

    [Fact]
    public void CanNavigateToAnotherPageWhilePreservingCommonDOMElements()
    {
        Navigate(ServerPathBase);

        var h1Elem = Browser.Exists(By.TagName("h1"));
        Browser.Equal("Hello", () => h1Elem.Text);
        
        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Streaming")).Click();

        // Important: we're checking the *same* <h1> element as earlier, showing that we got to the
        // destination, and it's done so without a page load, and it preserved the element
        Browser.Equal("Streaming Rendering", () => h1Elem.Text);

        // We have to make the response finish otherwise the test will fail when it tries to dispose the server
        Browser.FindElement(By.Id("end-response-link")).Click();
    }

    [Fact]
    public void CanNavigateToAnHtmlPageWithAnErrorStatus()
    {
        Navigate(ServerPathBase);
        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Error page with 404 content")).Click();
        Browser.Equal("404", () => Browser.Exists(By.TagName("h1")).Text);
    }

    [Fact]
    public void DisplaysStatusCodeIfResponseIsErrorWithNoContent()
    {
        Navigate(ServerPathBase);
        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Error page with no content")).Click();
        Browser.Equal("Error: 404 Not Found", () => Browser.Exists(By.TagName("body")).Text);
    }

    [Fact]
    public void CanNavigateToNonHtmlResponse()
    {
        Navigate(ServerPathBase);
        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Non-HTML page")).Click();
        Browser.Equal("Hello, this is plain text", () => Browser.Exists(By.TagName("body")).Text);
    }

    [Fact]
    public void ScrollsToHashWithContentAddedAsynchronously()
    {
        Navigate(ServerPathBase);
        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Scroll to hash")).Click();
        Assert.Equal(0, BrowserScrollY);

        var asyncContentHeader = Browser.Exists(By.Id("some-content"));
        Browser.Equal("Some content", () => asyncContentHeader.Text);
        Browser.True(() => BrowserScrollY > 500);
    }

    [Fact]
    public void CanFollowSynchronousRedirection()
    {
        Navigate(ServerPathBase);

        var h1Elem = Browser.Exists(By.TagName("h1"));
        Browser.Equal("Hello", () => h1Elem.Text);

        // Click a link and show we redirected, preserving elements, and updating the URL
        // Note that in this specific case we can't preserve the hash part of the URL, as it
        // gets lost when the browser follows a 'fetch' redirection. If we decide it's important
        // to support this later, we'd have to change the server not to do a real redirection
        // here and instead use the same protocol it uses for external redirections.
        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Redirect")).Click();
        Browser.Equal("Scroll to hash", () => h1Elem.Text);
        Assert.EndsWith("/subdir/scroll-to-hash", Browser.Url);

        // See that 'back' takes you to the place from before the redirection
        Browser.Navigate().Back();
        Browser.Equal("Hello", () => h1Elem.Text);
        Assert.EndsWith("/subdir", Browser.Url);
    }

    [Fact]
    public void CanFollowAsynchronousRedirectionWhileStreaming()
    {
        Navigate(ServerPathBase);

        var h1Elem = Browser.Exists(By.TagName("h1"));
        Browser.Equal("Hello", () => h1Elem.Text);

        // Click a link and show we redirected, preserving elements, scrolling to hash, and updating the URL
        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Redirect while streaming")).Click();
        Browser.Equal("Scroll to hash", () => h1Elem.Text);
        Browser.True(() => BrowserScrollY > 500);
        Assert.EndsWith("/subdir/scroll-to-hash#some-content", Browser.Url);

        // See that 'back' takes you to the place from before the redirection
        Browser.Navigate().Back();
        Browser.Equal("Hello", () => h1Elem.Text);
        Assert.EndsWith("/subdir", Browser.Url);
    }

    [Fact]
    public void CanFollowSynchronousExternalRedirection()
    {
        Navigate(ServerPathBase);
        Browser.Equal("Hello", () => Browser.Exists(By.TagName("h1")).Text);

        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Redirect external")).Click();
        Browser.Contains("microsoft.com", () => Browser.Url);
    }

    [Fact]
    public void CanFollowAsynchronousExternalRedirectionWhileStreaming()
    {
        Navigate(ServerPathBase);
        Browser.Equal("Hello", () => Browser.Exists(By.TagName("h1")).Text);

        Browser.Exists(By.TagName("nav")).FindElement(By.LinkText("Redirect external while streaming")).Click();
        Browser.Contains("microsoft.com", () => Browser.Url);
    }

    private long BrowserScrollY
    {
        get => Convert.ToInt64(((IJavaScriptExecutor)Browser).ExecuteScript("return window.scrollY"), CultureInfo.CurrentCulture);
        set => ((IJavaScriptExecutor)Browser).ExecuteScript($"window.scrollTo(0, {value})");
    }
}
