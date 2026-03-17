using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Mappings;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.Application.Handler;

public sealed class CancelShipmentCommandHandler(
    IShipmentRepository repository,
    IEventProducer eventProducer,
    IUnitOfWork unitOfWork)
{
    private readonly IShipmentRepository _repository = repository;
    private readonly IEventProducer _eventProducer = eventProducer;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ShipmentDto> Handle(
        CancelShipmentCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Load — tenant scoped
        var shipment = await _repository.GetByIdAsync(
            command.ShipmentId,
            cancellationToken)
            ?? throw new ShipmentNotFoundException(command.ShipmentId);

        var previousStatus = shipment.Status;

        // 3. Cancel — domain enforces terminal state rule
        shipment.Cancel(command.Reason);

        // 4. Publish so downstream services know it is cancelled
        await _eventProducer.PublishAsync(
            Topics.ShipmentStatusChanged,
            new ShipmentStatusChangedEvent(
                shipment.Id,
                shipment.TrackingNumber,
                shipment.TenantId,
                shipment.UserId,
                previousStatus.ToString(),
                "Cancelled",
                null,
                command.Reason,
                DateTime.UtcNow),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return shipment.ToDto();
    }
}