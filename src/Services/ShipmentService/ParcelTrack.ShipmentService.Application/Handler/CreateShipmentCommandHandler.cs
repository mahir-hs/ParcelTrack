using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Mappings;
using ParcelTrack.ShipmentService.Domain.Entities;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.Application.Handler;

public sealed class CreateShipmentCommandHandler(
    IShipmentRepository repository,
    IEventProducer eventProducer)
{
    private readonly IShipmentRepository _repository = repository;
    private readonly IEventProducer _eventProducer = eventProducer;

    public async Task<ShipmentDto> Handle(
        CreateShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Duplicate check — same tracking number within the same tenant
        var existing = await _repository.GetByTrackingNumberAsync(
            command.TrackingNumber,
            command.TenantId,
            cancellationToken);

        if (existing is not null)
            throw new DuplicateTrackingNumberException(command.TrackingNumber);

        // 2. Create the aggregate via factory method
        var shipment = Shipment.Create(
            command.TrackingNumber,
            command.CarrierType,
            command.BuyerEmail,
            command.UserId,
            command.TenantId);

        // 3. Persist
        await _repository.AddAsync(shipment, cancellationToken);

        // 4. Publish Kafka event so Tracking Service starts watching
        await _eventProducer.PublishAsync(
            Topics.ShipmentCreated,
            new ShipmentCreatedEvent(
                shipment.Id,
                shipment.TrackingNumber,
                shipment.CarrierType.ToString(),
                shipment.UserId,
                shipment.TenantId,
                shipment.BuyerEmail,
                shipment.CreatedAt),
            cancellationToken);

        // 5. Map to DTO and return
        return shipment.ToDto();
    }
}