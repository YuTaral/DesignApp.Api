using DesignApp.Domain.Entities;
using DesignApp.Domain.Exceptions;
using DesignApp.Domain.Objects;

using static DesignApp.Domain.Constants;

namespace DesignApp.UnitTests.Domain;

/// <summary>The room's status steps and its validation rules.</summary>
public class RoomTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private const string LabelMapKey = "rooms/labels.png";

    /// <summary>A valid detected sofa.</summary>
    /// <param name="index">Its label-map index.</param>
    internal static DetectedObject Sofa(int index = 1)
    {
        return new(index, "sofa", ObjectCategory.Furniture, new BoundingBox(0.1, 0.4, 0.5, 0.4), HexColor.Parse("#8a7f72"));
    }

    /// <summary>A room that has gone through every step to Ready, with two objects.</summary>
    /// <param name="ownerId">Who owns the room.</param>
    internal static Room ReadyRoom(string ownerId = "user-1")
    {
        var room = Room.Upload(Guid.CreateVersion7(), ownerId, "rooms/photo.jpg", 1600, 1200, Now);

        room.StartAnalysis();
        room.StartSegmentation();
        room.MarkReady([Sofa(1), Sofa(2) with { Label = "wall", Category = ObjectCategory.Wall }], LabelMapKey);

        return room;
    }

    [Fact]
    public void Upload_starts_in_uploaded_state()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        Assert.Equal(RoomStatus.Uploaded, room.Status);
        Assert.Empty(room.Objects);
        Assert.Equal(Now, room.CreatedAt);
    }

    [Fact]
    public void Upload_rejects_an_empty_id()
    {
        Assert.Throws<DomainException>(() => Room.Upload(Guid.Empty, "user-1", "rooms/photo.jpg", 1600, 1200, Now));
    }

    [Fact]
    public void Analysis_pipeline_moves_through_each_stage_to_ready()
    {
        var room = ReadyRoom();

        Assert.Equal(RoomStatus.Ready, room.Status);
        Assert.Equal([1, 2], room.Objects.Select(o => o.Index));
        Assert.Equal(LabelMapKey, room.LabelMapKey);
        Assert.Equal(1, room.AnalysisAttempts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(256)]
    public void Object_indexes_must_fit_the_label_map(int index)
    {
        var room = Segmenting();

        Assert.Throws<DomainException>(() => room.MarkReady([Sofa(index)], LabelMapKey));
    }

    [Fact]
    public void An_interrupted_analysis_can_restart()
    {
        var room = Segmenting();

        room.RestartAnalysis();
        room.StartAnalysis();

        Assert.Equal(RoomStatus.Analyzing, room.Status);
        Assert.Equal(2, room.AnalysisAttempts);
    }

    [Fact]
    public void Only_a_running_analysis_can_restart()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        Assert.Throws<DomainException>(room.RestartAnalysis);
        Assert.Throws<DomainException>(() => ReadyRoom().RestartAnalysis());
    }

    [Fact]
    public void A_waiting_room_is_analyzed_only_once_the_user_starts_it()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now, waitForUser: true);

        Assert.Equal(RoomStatus.Waiting, room.Status);
        Assert.Throws<DomainException>(room.StartAnalysis);
        room.QueueAnalysis();
        Assert.Equal(RoomStatus.Uploaded, room.Status);
        Assert.Throws<DomainException>(room.QueueAnalysis);
    }

    [Fact]
    public void Analysis_stops_after_the_maximum_attempts()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        for (var i = 0; i < DesignApp.Domain.Constants.Rooms.MaxAnalysisAttempts; i++)
        {
            room.StartAnalysis();
            room.RestartAnalysis();
        }

        Assert.Throws<DomainException>(room.StartAnalysis);
    }

    /// <summary>A room part-way through the analysis.</summary>
    private static Room Segmenting()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        room.StartAnalysis();
        room.StartSegmentation();

        return room;
    }

    [Fact]
    public void Cannot_skip_the_segmentation_stage()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        room.StartAnalysis();

        Assert.Throws<DomainException>(() => room.MarkReady([Sofa()], LabelMapKey));
    }

    [Fact]
    public void Ready_requires_unique_object_indexes()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        room.StartAnalysis();
        room.StartSegmentation();

        Assert.Throws<DomainException>(() => room.MarkReady([Sofa(1), Sofa(1)], LabelMapKey));
    }

    [Fact]
    public void Failing_records_the_reason()
    {
        var room = Room.Upload(Guid.CreateVersion7(), "user-1", "rooms/photo.jpg", 1600, 1200, Now);

        room.StartAnalysis();

        room.MarkFailed("Segmentation provider timed out");

        Assert.Equal(RoomStatus.Failed, room.Status);
        Assert.Equal("Segmentation provider timed out", room.FailureReason);
    }

    [Fact]
    public void A_ready_room_cannot_fail()
    {
        Assert.Throws<DomainException>(() => ReadyRoom().MarkFailed("late error"));
    }

    [Theory]
    [InlineData(-0.1, 0, 0.5, 0.5)]
    [InlineData(0, 0, 0, 0.5)]
    [InlineData(0.6, 0, 0.5, 0.5)]
    public void Bounding_box_must_fit_inside_the_image(double x, double y, double width, double height)
    {
        Assert.Throws<DomainException>(() => new BoundingBox(x, y, width, height));
    }
}
