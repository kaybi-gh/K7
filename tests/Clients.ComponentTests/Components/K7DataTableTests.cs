using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K7.Clients.ComponentTests.Components;

[TestFixture]
public class K7DataTableTests
{
    [Test]
    public async Task ColumnPicker_ShouldKeepTheColumnHidden_WhenTheTableRendersAgain()
    {
        using var ctx = CreateContext();
        var cut = RenderTable(ctx);
        cut.FindAll("th").Should().HaveCount(2);

        await cut.InvokeAsync(() => cut.Find(".k7-data-table-column-picker input").Change(false));

        cut.FindAll("th").Should().ContainSingle();

        cut.Render();

        cut.FindAll("th").Should().ContainSingle();
    }

    [Test]
    public async Task ColumnPicker_ShouldStoreTheHiddenColumnKeys_WhenAPersistenceKeyIsSet()
    {
        using var ctx = CreateContext();
        var storage = ctx.Services.GetRequiredService<IDeviceStorageService>();
        var cut = RenderTable(ctx, persistenceKey: "tracks");

        await cut.InvokeAsync(() => cut.Find(".k7-data-table-column-picker input").Change(false));

        storage.Received(1).Set(PreferenceKeys.DATA_TABLE_HIDDEN_COLUMNS, """{"tracks":["artist"]}""");
    }

    [Test]
    public void Render_ShouldHideTheStoredColumns_WhenAPersistenceKeyIsSet()
    {
        using var ctx = CreateContext();
        ctx.Services.GetRequiredService<IDeviceStorageService>()
            .Get(PreferenceKeys.DATA_TABLE_HIDDEN_COLUMNS, Arg.Any<string?>())
            .Returns("""{"tracks":["artist"],"other":["title"]}""");

        var cut = RenderTable(ctx, persistenceKey: "tracks");

        cut.FindAll("th").Should().ContainSingle();
    }

    [Test]
    public async Task ColumnPicker_ShouldNotStoreAColumn_WhenItsVisibleParameterHidesIt()
    {
        using var ctx = CreateContext();
        var storage = ctx.Services.GetRequiredService<IDeviceStorageService>();
        var cut = ctx.Render<K7DataTable<string>>(p => p
            .Add(t => t.Items, new List<string> { "row" })
            .Add(t => t.PersistenceKey, "tracks")
            .Add(t => t.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<K7DataColumn<string>>(0);
                builder.AddComponentParameter(1, nameof(K7DataColumn<string>.Title), "Artist");
                builder.AddComponentParameter(2, nameof(K7DataColumn<string>.Key), "artist");
                builder.CloseComponent();
                builder.OpenComponent<K7DataColumn<string>>(3);
                builder.AddComponentParameter(4, nameof(K7DataColumn<string>.Title), "Owner");
                builder.AddComponentParameter(5, nameof(K7DataColumn<string>.Key), "owner");
                builder.AddComponentParameter(6, nameof(K7DataColumn<string>.Visible), false);
                builder.CloseComponent();
            })));

        await cut.InvokeAsync(() => cut.Find(".k7-data-table-column-picker input").Change(false));

        storage.Received(1).Set(PreferenceKeys.DATA_TABLE_HIDDEN_COLUMNS, """{"tracks":["artist"]}""");
    }

    private static IRenderedComponent<K7DataTable<string>> RenderTable(BunitContext ctx, string? persistenceKey = null) =>
        ctx.Render<K7DataTable<string>>(p => p
            .Add(t => t.Items, new List<string> { "row" })
            .Add(t => t.PersistenceKey, persistenceKey)
            .Add(t => t.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<K7DataColumn<string>>(0);
                builder.AddComponentParameter(1, nameof(K7DataColumn<string>.Title), "Title");
                builder.AddComponentParameter(2, nameof(K7DataColumn<string>.Hideable), false);
                builder.CloseComponent();
                builder.OpenComponent<K7DataColumn<string>>(3);
                builder.AddComponentParameter(4, nameof(K7DataColumn<string>.Title), "Artist");
                builder.AddComponentParameter(5, nameof(K7DataColumn<string>.Key), "artist");
                builder.CloseComponent();
            })));

    private static BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.Services.AddSingleton(Substitute.For<ISpatialNavService>());
        ctx.Services.AddSingleton(Substitute.For<IDeviceStorageService>());
        ctx.Services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.JSInterop.SetupModule("./_content/K7.Clients.Shared.UI/js/browseView.js").Mode = JSRuntimeMode.Loose;

        var localizer = Substitute.For<IStringLocalizer<SharedResource>>();
        localizer[Arg.Any<string>()].Returns(call =>
            new LocalizedString(call.Arg<string>(), call.Arg<string>()));
        ctx.Services.AddSingleton(localizer);

        return ctx;
    }
}
