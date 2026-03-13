using FluentAssertions;
using Moq;
using ParcelTrack.ShipmentService.Application.Handler;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Queries;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Enums;
using ParcelTrack.ShipmentService.UnitTests.Application.Helpers;

namespace ParcelTrack.ShipmentService.UnitTests.Application.Handlers;

public sealed class GetShipmentsQueryHandlerTests
{
    private readonly Mock<IShipmentRepository> _repoMock;
    private readonly GetShipmentsQueryHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public GetShipmentsQueryHandlerTests()
    {
        _repoMock = new Mock<IShipmentRepository>();
        _handler = new GetShipmentsQueryHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_WithResults_ReturnsCorrectItemCount()
    {
        // Arrange
        var shipments = BuildShipmentList(3);
        SetupRepo(shipments, totalCount: 3);

        // Act
        var result = await _handler.Handle(BuildQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(3);
        result.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsEmptyPagedList()
    {
        // Arrange
        SetupRepo([], totalCount: 0);

        // Act
        var result = await _handler.Handle(BuildQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SecondPageOf25_ReturnsCorrectPaginationMetadata()
    {
        // Arrange: 25 total, page 2 of 3
        var shipments = BuildShipmentList(10);
        SetupRepo(shipments, totalCount: 25);

        // Act
        var result = await _handler.Handle(BuildQuery(page: 2, pageSize: 10), CancellationToken.None);

        // Assert
        result.TotalPages.Should().Be(3);
        result.HasNext.Should().BeTrue();
        result.HasPrev.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_FirstPage_HasNoPreviousPage()
    {
        // Arrange
        var shipments = BuildShipmentList(5);
        SetupRepo(shipments, totalCount: 5);

        // Act
        var result = await _handler.Handle(BuildQuery(page: 1), CancellationToken.None);

        // Assert
        result.HasPrev.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_LastPage_HasNoNextPage()
    {
        // Arrange: 5 total items, page size 10 — fits on one page
        var shipments = BuildShipmentList(5);
        SetupRepo(shipments, totalCount: 5);

        // Act
        var result = await _handler.Handle(BuildQuery(page: 1, pageSize: 10), CancellationToken.None);

        // Assert
        result.HasNext.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithStatusFilter_ForwardsFilterToRepository()
    {
        // Arrange
        SetupRepo([], totalCount: 0);

        // Act
        await _handler.Handle(
            BuildQuery(statusFilter: ShipmentStatus.InTransit),
            CancellationToken.None);

        // Assert — filter must be forwarded, not silently dropped
        _repoMock.Verify(
            r => r.GetPagedAsync(
                _tenantId,
                It.IsAny<int>(),
                It.IsAny<int>(),
                ShipmentStatus.InTransit,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoStatusFilter_PassesNullToRepository()
    {
        // Arrange
        SetupRepo([], totalCount: 0);

        // Act
        await _handler.Handle(BuildQuery(), CancellationToken.None);

        // Assert
        _repoMock.Verify(
            r => r.GetPagedAsync(
                _tenantId,
                It.IsAny<int>(),
                It.IsAny<int>(),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetupRepo(List<Shipment> shipments, int totalCount)
    {
        _repoMock
            .Setup(r => r.GetPagedAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<ShipmentStatus?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((shipments.AsReadOnly() as IReadOnlyList<Shipment>, totalCount));
    }

    private GetShipmentsQuery BuildQuery(
        int page = 1,
        int pageSize = 10,
        ShipmentStatus? statusFilter = null) => new()
        {
            TenantId = _tenantId,
            Page = page,
            PageSize = pageSize,
            StatusFilter = statusFilter
        };

    private List<Shipment> BuildShipmentList(int count)
        => Enumerable.Range(1, count)
                     .Select(i => ShipmentFactory.Create(
                         trackingNumber: $"STD-LIST-{i:D3}",
                         tenantId: _tenantId))
                     .ToList();
}