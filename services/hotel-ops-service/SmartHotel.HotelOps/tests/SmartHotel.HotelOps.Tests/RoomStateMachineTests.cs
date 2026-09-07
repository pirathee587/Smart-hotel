using FluentAssertions;
using SmartHotel.HotelOps.Domain.Entities;
using SmartHotel.HotelOps.Domain.Enums;
using SmartHotel.HotelOps.Domain.Exceptions;
using Xunit;

namespace SmartHotel.HotelOps.Tests;

public class RoomStateMachineTests
{
    private Room CreateRoom(RoomStatus initialStatus = RoomStatus.Available)
    {
        return new Room
        {
            Id = Guid.NewGuid(),
            HotelId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            RoomNumber = "101",
            Floor = 1,
            Status = initialStatus
        };
    }

    [Theory]
    [InlineData(RoomStatus.Available, RoomStatus.Occupied)]
    [InlineData(RoomStatus.Occupied, RoomStatus.Dirty)]
    [InlineData(RoomStatus.Dirty, RoomStatus.InCleaning)]
    [InlineData(RoomStatus.InCleaning, RoomStatus.Inspected)]
    [InlineData(RoomStatus.Inspected, RoomStatus.Available)]
    [InlineData(RoomStatus.OutOfOrder, RoomStatus.Dirty)]
    [InlineData(RoomStatus.OutOfOrder, RoomStatus.Inspected)]
    public void ValidTransitions_ShouldSucceed(RoomStatus current, RoomStatus target)
    {
        var room = CreateRoom(current);

        room.UpdateStatus(target);

        room.Status.Should().Be(target);
    }

    [Fact]
    public void TransitionToOutOfOrder_WithValidReason_ShouldSucceed()
    {
        var room = CreateRoom(RoomStatus.Available);

        room.UpdateStatus(RoomStatus.OutOfOrder, "Pipe burst in bathroom");

        room.Status.Should().Be(RoomStatus.OutOfOrder);
        room.OutOfOrderReason.Should().Be("Pipe burst in bathroom");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TransitionToOutOfOrder_WithoutReason_ShouldThrowInvalidRoomStatusTransitionException(string? reason)
    {
        var room = CreateRoom(RoomStatus.Available);

        var act = () => room.UpdateStatus(RoomStatus.OutOfOrder, reason);

        act.Should().Throw<InvalidRoomStatusTransitionException>()
            .WithMessage("*reason is mandatory*");
    }

    [Theory]
    [InlineData(RoomStatus.Available, RoomStatus.Inspected)] // Cannot skip cleaning/inspection
    [InlineData(RoomStatus.Dirty, RoomStatus.Available)]    // Cannot jump from dirty to available
    [InlineData(RoomStatus.InCleaning, RoomStatus.Available)] // Must be inspected before available
    [InlineData(RoomStatus.Occupied, RoomStatus.Available)] // Must go to Dirty first upon guest checkout
    [InlineData(RoomStatus.InCleaning, RoomStatus.Dirty)]   // Must follow forward cleaning cycle
    [InlineData(RoomStatus.Inspected, RoomStatus.Dirty)]    // Must follow forward cleaning cycle
    public void InvalidTransitions_ShouldThrowInvalidRoomStatusTransitionException(RoomStatus current, RoomStatus target)
    {
        var room = CreateRoom(current);

        var act = () => room.UpdateStatus(target);

        act.Should().Throw<InvalidRoomStatusTransitionException>()
            .WithMessage($"*{current}*{target}*");
    }

    [Fact]
    public void SameStatus_ShouldBeNoOp()
    {
        var room = CreateRoom(RoomStatus.Available);

        room.UpdateStatus(RoomStatus.Available);

        room.Status.Should().Be(RoomStatus.Available);
    }
}
