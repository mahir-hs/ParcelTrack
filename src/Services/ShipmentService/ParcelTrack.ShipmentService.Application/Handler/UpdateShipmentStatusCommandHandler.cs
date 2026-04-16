using ParcelTrack.Shared.Contracts;
using ParcelTrack.Shared.Contracts.Events;
using ParcelTrack.ShipmentService.Application.Commands;
using ParcelTrack.ShipmentService.Application.DTOs;
using ParcelTrack.ShipmentService.Application.Interfaces;
using ParcelTrack.ShipmentService.Application.Mappings;
using ParcelTrack.ShipmentService.Domain.Exceptions;

namespace ParcelTrack.ShipmentService.Application.Handler;

public class UpdateShipmentStatusCommandHandler(
    IShipmentRepository repository,
    IEventProducer eventProducer,
    IUnitOfWork unitOfWork)
{
    private readonly IShipmentRepository _repository = repository;
    private readonly IEventProducer _eventProducer = eventProducer;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<ShipmentDto> Handle(
        UpdateShipmentStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Load shipment — scoped to tenant for security
        var shipment = await _repository.GetByIdAsync(
            command.ShipmentId,
            cancellationToken)
            ?? throw new ShipmentNotFoundException(command.ShipmentId);

        var previousStatus = shipment.Status;

        // 2. Delegate to domain — state machine + max attempt rule live here
        shipment.UpdateStatus(command.NewStatus, command.Description, command.Location);

        // 3. Publish event — Notification + Webhook Dispatch services consume this
        await _eventProducer.PublishAsync(
            Topics.ShipmentStatusChanged,
            new ShipmentStatusChangedEvent(
                shipment.Id,
                shipment.TrackingNumber,
                shipment.TenantId,
                shipment.UserId,
                previousStatus.ToString(),
                command.NewStatus.ToString(),
                command.Location,
                command.Description,
                DateTime.UtcNow),
            cancellationToken);

        Console.WriteLine($"Saving Shipment Id: {shipment.Id} | Status: {shipment.Status}");

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return shipment.ToDto();
    }
}
