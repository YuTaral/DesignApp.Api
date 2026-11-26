using DesignApp.Domain.Objects;
using DesignApp.Infrastructure.Database;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Builder;

/// <summary>How a room's finish is stored: walls in their own looks, and rows stored with one accent wall before them.</summary>
public class RoomFinishJsonTests
{
    [Fact]
    public void A_finish_stored_with_an_accent_wall_reads_as_that_walls_own_look_and_a_new_one_round_trips()
    {
        const string older = """{"walls":"Plaster","accentWall":2,"accentColor":"#B5654A","accentFinish":"Brick","ceilingColor":"#FAFAF7","trimColor":"#F4F1EA","ceilingLight":"Pendant","timeOfDay":"Midday"}""";
        const string none = """{"walls":"Paint","accentWall":null,"accentColor":"#8A9A84","accentFinish":"Paint","ceilingColor":"#FAFAF7","trimColor":"#F4F1EA","ceilingLight":"Pendant","timeOfDay":"Midday"}""";
        var finish = new RoomFinish(WallFinish.Paint, [new WallLook(0, WallFinish.Brick, HexColor.Parse("#B5654A")), new WallLook(3, WallFinish.Tiles, HexColor.Parse("#FFFFFF"))],
            HexColor.Parse("#FAFAF7"), HexColor.Parse("#F4F1EA"), TimeOfDay.Evening, LightTone.Neutral, PlanWall.Left);

        var read = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(older)!;
        var plain = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(none)!;
        var json = (string)RoomFinishJson.Converter.ConvertToProvider(finish)!;
        var back = (RoomFinish)RoomFinishJson.Converter.ConvertFromProvider(json)!;

        Assert.Equal((2, WallFinish.Brick, "#B5654A"), (read.OwnWalls[0].Wall, read.OwnWalls[0].Finish, read.OwnWalls[0].Color.Value));
        Assert.Single(read.OwnWalls);
        Assert.Empty(plain.OwnWalls);
        Assert.Equal(finish, back);
        // A row from before had no tone or north (and a ceiling light, now an object): warm light, north at the back.
        Assert.Equal((LightTone.Warm, PlanWall.Back), (read.Tone, read.North));
        Assert.DoesNotContain("accent", json, StringComparison.OrdinalIgnoreCase);
    }
}
