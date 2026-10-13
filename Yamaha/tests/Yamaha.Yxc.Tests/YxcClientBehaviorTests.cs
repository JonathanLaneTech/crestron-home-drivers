using System.Net;
using System.Net.Http;
using Xunit;

namespace Yamaha.Yxc.Tests;

/// <summary>
/// Unit tests for YxcClient. These run without a receiver: a fake HTTP handler
/// stands in for the R-N2000A and records every request the client sends.
/// </summary>
public class YxcClientBehaviorTests
{
    private const string Host = "192.168.1.50";

    // ---------- Construction ----------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankHost(string host)
    {
        Assert.Throws<ArgumentException>(() => new YxcClient(host));
    }

    [Fact]
    public void Constructor_BuildsYxcBaseUri()
    {
        using var client = new YxcClient(" " + Host + " ");
        Assert.Equal("http://" + Host + "/YamahaExtendedControl/", client.BaseUri.ToString());
    }

    [Fact]
    public void Constructor_UsesCustomPort()
    {
        using var client = new YxcClient(Host, port: 8080);
        Assert.Equal(8080, client.BaseUri.Port);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_BlankZoneDefaultsToMain(string? zone)
    {
        using var client = new YxcClient(Host, zone: zone!);
        Assert.Equal("main", client.Zone);
    }

    // ---------- Status parsing ----------

    [Fact]
    public async Task GetStatus_ParsesPowerVolumeMuteAndInput()
    {
        var json = "{\"response_code\":0,\"power\":\"" + YxcPower.On + "\",\"volume\":80,\"max_volume\":161," +
                   "\"mute\":true,\"input\":\"optical1\",\"input_text\":\"Optical 1\",\"sound_program\":\"straight\"}";
        var (client, handler) = Create(_ => Ok(json));

        var status = await client.GetStatusAsync();

        Assert.True(status.PowerOn);
        Assert.Equal(80, status.Volume);
        Assert.Equal(161, status.MaxVolume);
        Assert.True(status.Mute);
        Assert.Equal("optical1", status.Input);
        Assert.Equal("Optical 1", status.InputText);
        Assert.Equal("straight", status.SoundProgram);
        Assert.Equal("/YamahaExtendedControl/v1/main/getStatus", handler.Requests.Single().AbsolutePath);
    }

    [Fact]
    public async Task GetStatus_StandbyIsReportedAsPowerOff()
    {
        var (client, _) = Create(_ => Ok("{\"response_code\":0,\"power\":\"standby\",\"volume\":0,\"mute\":false}"));

        var status = await client.GetStatusAsync();

        Assert.False(status.PowerOn);
    }

    [Fact]
    public async Task Requests_IncludeMusicCastAppNameHeader()
    {
        var (client, handler) = Create(_ => Ok("{\"response_code\":0}"));

        await client.GetStatusAsync();

        Assert.Contains("MusicCast/1.0", handler.AppNameHeaders.Single());
    }

    [Fact]
    public async Task Requests_UseConfiguredZone()
    {
        var (client, handler) = Create(_ => Ok("{\"response_code\":0}"), zone: "zone2");

        await client.SetPowerAsync("on");

        Assert.Equal("/YamahaExtendedControl/v1/zone2/setPower", handler.Requests.Single().AbsolutePath);
    }

    // ---------- Commands ----------

    [Fact]
    public async Task SetVolume_SendsAbsoluteVolume()
    {
        var (client, handler) = Create(_ => Ok("{\"response_code\":0}"));

        await client.SetVolumeAsync(77);

        var uri = handler.Requests.Single();
        Assert.Equal("/YamahaExtendedControl/v1/main/setVolume", uri.AbsolutePath);
        Assert.Equal("?volume=77", uri.Query);
    }

    [Fact]
    public async Task NudgeVolume_SendsDirectionAndStep()
    {
        var (client, handler) = Create(_ => Ok("{\"response_code\":0}"));

        await client.NudgeVolumeAsync("up", 2);

        Assert.Equal("?volume=up&step=2", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task SetMute_SendsBooleanForm()
    {
        var (client, handler) = Create(_ => Ok("{\"response_code\":0}"));

        await client.SetMuteAsync(true);

        Assert.Equal("?enable=true", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task SetMute_FallsBackToNumericFormWhenReceiverRejectsBoolean()
    {
        var (client, handler) = Create(req =>
            req.Query.Contains("enable=true") ? Status(HttpStatusCode.BadRequest) : Ok("{\"response_code\":0}"));

        await client.SetMuteAsync(true);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("?enable=1", handler.Requests[1].Query);
    }

    [Fact]
    public async Task SetInput_PreparesThenSwitches()
    {
        var (client, handler) = Create(_ => Ok("{\"response_code\":0}"));

        await client.SetInputAsync("optical1");

        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("/prepareInputChange", handler.Requests[0].AbsolutePath);
        Assert.EndsWith("/setInput", handler.Requests[1].AbsolutePath);
        Assert.Equal("?input=optical1", handler.Requests[1].Query);
    }

    [Fact]
    public async Task SetInput_StillSwitchesWhenPrepareFails()
    {
        var (client, handler) = Create(req =>
            req.AbsolutePath.EndsWith("/prepareInputChange") ? Status(HttpStatusCode.NotFound) : Ok("{\"response_code\":0}"));

        await client.SetInputAsync("phono");

        Assert.EndsWith("/setInput", handler.Requests.Last().AbsolutePath);
    }

    [Fact]
    public async Task SetPlayback_TargetsNetUsb()
    {
        var (client, handler) = Create(_ => Ok("{\"response_code\":0}"));

        await client.SetPlaybackAsync("play");

        Assert.Equal("/YamahaExtendedControl/v1/netusb/setPlayback", handler.Requests.Single().AbsolutePath);
        Assert.Equal("?playback=play", handler.Requests.Single().Query);
    }

    // ---------- Features ----------

    [Fact]
    public async Task GetFeatures_DefaultsVolumeRangeWhenReceiverOmitsIt()
    {
        var (client, _) = Create(_ => Ok("{\"response_code\":0}"));

        var features = await client.GetFeaturesAsync();

        Assert.Equal(154, features.VolumeMax);
        Assert.Equal(1, features.VolumeStep);
        Assert.NotEmpty(features.InputIds);
    }

    // ---------- Error handling ----------

    [Fact]
    public async Task HttpErrorStatus_ThrowsYxcException()
    {
        var (client, _) = Create(_ => Status(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<YxcException>(() => client.GetStatusAsync());
    }

    [Fact]
    public async Task NetworkFailure_IsWrappedInYxcException()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("receiver offline"));

        var ex = await Assert.ThrowsAsync<YxcException>(() => client.GetStatusAsync());
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public void Dispose_DoesNotDisposeInjectedHttpClient()
    {
        var handler = new FakeHandler(_ => Ok("{\"response_code\":0}"));
        var http = new HttpClient(handler);

        new YxcClient(Host, httpClient: http).Dispose();

        // Still usable, so the caller keeps ownership of an HttpClient it passed in.
        var response = http.GetAsync("http://" + Host + "/").GetAwaiter().GetResult();
        Assert.True(response.IsSuccessStatusCode);
    }

    // ---------- Helpers ----------

    private static (YxcClient Client, FakeHandler Handler) Create(Func<Uri, HttpResponseMessage> respond, string zone = "main")
    {
        var handler = new FakeHandler(respond);
        return (new YxcClient(Host, zone: zone, httpClient: new HttpClient(handler)), handler);
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private static HttpResponseMessage Status(HttpStatusCode code) =>
        new(code) { Content = new StringContent("{\"response_code\":4}") };

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage> _respond;

        public FakeHandler(Func<Uri, HttpResponseMessage> respond) => _respond = respond;

        public List<Uri> Requests { get; } = new();

        public List<string> AppNameHeaders { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (request.Headers.TryGetValues("X-AppName", out var values))
            {
                AppNameHeaders.Add(string.Join(",", values));
            }

            return Task.FromResult(_respond(request.RequestUri!));
        }
    }
}
