using System.Net;
using System.Text;
using System.Text.Json;
using DesignApp.Application.Analysis;
using DesignApp.Domain.Common;
using DesignApp.Domain.Rooms;
using DesignApp.Infrastructure.Ai;
using DesignApp.Infrastructure.Ai.Claude;
using DesignApp.Infrastructure.Ai.Fal;
using Microsoft.Extensions.Options;
using SkiaSharp;

namespace DesignApp.UnitTests.Analysis;

/// <summary>
/// The real providers' own logic without calling the services: reading Claude's JSON answer, and
/// the requests and responses of the SAM 3 client against a stubbed fal.ai.
/// </summary>
public class AiProviderTests
{
    [Fact]
    public void Claude_answers_become_inventory_items()
    {
        var result = InventoryJson.Parse("""
            {"isRoom": true, "objects": [
              {"label": " grey sofa ", "category": "Furniture", "box": {"x": 0.2, "y": 0.4, "width": 0.5, "height": 0.3}, "color": "#8a8a8a"}
            ]}
            """);

        var item = Assert.Single(result.Value);
        Assert.Equal(("grey sofa", ObjectCategory.Furniture, "#8A8A8A"), (item.Label, item.Category, item.Color.Value));
        Assert.Equal(new BoundingBox(0.2, 0.4, 0.5, 0.3), item.Box);
    }

    [Fact]
    public void Boxes_reaching_outside_the_image_are_clipped_and_bad_entries_skipped()
    {
        var result = InventoryJson.Parse("""
            {"isRoom": true, "objects": [
              {"label": "rug", "category": "Textile", "box": {"x": 0.6, "y": 0.8, "width": 0.6, "height": 0.4}, "color": "red"},
              {"label": "", "category": "Decor", "box": {"x": 0, "y": 0, "width": 0.1, "height": 0.1}, "color": "#000000"},
              {"label": "ghost", "category": "Decor", "box": {"x": 1.2, "y": 0, "width": 0.1, "height": 0.1}, "color": "#000000"}
            ]}
            """);

        var rug = Assert.Single(result.Value);
        Assert.Equal(0.4, rug.Box.Width, 6);
        Assert.Equal(0.2, rug.Box.Height, 6);
        Assert.Equal("#808080", rug.Color.Value); // an unreadable color falls back to gray
    }

    [Fact]
    public void A_photo_that_is_not_a_room_is_refused()
    {
        var result = InventoryJson.Parse("""{"isRoom": false, "objects": []}""");

        Assert.Equal(InventoryJson.NotARoom, result.Error);
    }

    [Fact]
    public void An_unknown_category_is_a_contract_error()
    {
        Assert.Throws<JsonException>(() => InventoryJson.Parse("""
            {"isRoom": true, "objects": [{"label": "x", "category": "Spaceship", "box": {"x": 0, "y": 0, "width": 1, "height": 1}, "color": "#000000"}]}
            """));
    }

    [Fact]
    public async Task Sam_gets_one_request_per_object_with_its_label_and_pixel_box()
    {
        var fal = new StubFal(MaskPng(4, 2));
        var provider = Provider(fal);
        var image = new AnalysisImage([0xFF, 0xD8], 400, 200);
        InventoryItem[] items =
        [
            new("sofa", ObjectCategory.Furniture, new BoundingBox(0.25, 0.5, 0.5, 0.25), HexColor.Parse("#000000")),
            new("lamp", ObjectCategory.Lighting, new BoundingBox(0.1, 0.1, 0.1, 0.1), HexColor.Parse("#000000")),
        ];

        var masks = await provider.SegmentAsync(image, items, CancellationToken.None);

        Assert.Equal(2, masks.Count);
        Assert.All(masks, Assert.NotNull);
        Assert.All(fal.Requests, r => Assert.Equal("Key test-key", r.Authorization));
        var sofa = fal.Bodies.Single(b => b.GetProperty("prompt").GetString() == "sofa");
        var box = sofa.GetProperty("box_prompts")[0];
        Assert.Equal((100, 100, 300, 150), (box.GetProperty("x_min").GetInt32(), box.GetProperty("y_min").GetInt32(),
            box.GetProperty("x_max").GetInt32(), box.GetProperty("y_max").GetInt32()));
        Assert.StartsWith("data:image/jpeg;base64,", sofa.GetProperty("image_url").GetString(), StringComparison.Ordinal);
        Assert.False(sofa.GetProperty("apply_mask").GetBoolean());
    }

    [Fact]
    public async Task An_empty_sam_answer_means_no_mask()
    {
        var provider = Provider(new StubFal(null));

        var masks = await provider.SegmentAsync(new AnalysisImage([0xFF, 0xD8], 100, 100),
            [new("sofa", ObjectCategory.Furniture, new BoundingBox(0, 0, 1, 1), HexColor.Parse("#000000"))], CancellationToken.None);

        Assert.Null(Assert.Single(masks));
    }

    [Fact]
    public void Mask_images_decode_to_coverage()
    {
        var mask = FalSegmentationProvider.Decode(MaskPng(4, 2));

        Assert.NotNull(mask);
        Assert.Equal((4, 2), (mask.Width, mask.Height));
        Assert.Equal([255, 255, 0, 0, 255, 255, 0, 0], mask.Coverage);
    }

    /// <summary>The SAM 3 client wired to a stubbed fal.ai.</summary>
    /// <param name="fal">The stub.</param>
    private static FalSegmentationProvider Provider(StubFal fal)
    {
        var http = new HttpClient(fal);
        http.DefaultRequestHeaders.Authorization = new("Key", "test-key");
        return new FalSegmentationProvider(http, Options.Create(new AiOptions()));
    }

    /// <summary>A mask PNG: white on the left half, black on the right.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    private static byte[] MaskPng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Black);
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width / 2; x++)
                bitmap.SetPixel(x, y, SKColors.White);
        using var png = SKImage.FromBitmap(bitmap).Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    /// <summary>Answers like fal.ai's SAM 3 endpoint in sync mode, recording each request.</summary>
    /// <param name="mask">The mask to return as a data URI; <c>null</c> returns no masks.</param>
    private sealed class StubFal(byte[]? mask) : HttpMessageHandler
    {
        private readonly Lock _gate = new();

        /// <summary>The requests' auth headers.</summary>
        public List<(string? Authorization, string Path)> Requests { get; } = [];
        /// <summary>The parsed request bodies.</summary>
        public List<JsonElement> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
            lock (_gate)
            {
                Requests.Add((request.Headers.Authorization?.ToString(), request.RequestUri!.AbsolutePath));
                Bodies.Add(body);
            }
            var masks = mask is null ? "[]" : $$"""[{"url": "data:image/png;base64,{{Convert.ToBase64String(mask)}}"}]""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"masks": {{masks}}}""", Encoding.UTF8, "application/json"),
            };
        }
    }
}
