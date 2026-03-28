using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Services;

public class CreateBackgroundTaskCommandHandlerTests
{
    private IApplicationDbContext _context;
    private IBackgroundTaskQueue _taskQueue;
    private CreateBackgroundTaskCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _taskQueue = Substitute.For<IBackgroundTaskQueue>();

        var dbSet = Substitute.For<DbSet<BackgroundTask>>();
        _context.BackgroundTasks.Returns(dbSet);

        _handler = new CreateBackgroundTaskCommandHandler(_context, _taskQueue);
    }

    [Test]
    public async Task Handle_ShouldAddEntityToDbSet()
    {
        // Arrange
        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            Priority = BackgroundTaskPriority.High,
            MaxAttempts = 3,
            TargetEntityTypeName = "TestEntity",
            TargetEntityId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Is<BackgroundTask>(t =>
            t.Status == BackgroundTaskStatus.Pending
            && t.Priority == BackgroundTaskPriority.High
            && t.MaxAttempts == 3
            && t.TargetEntityType == "TestEntity"
        ));
        await _context.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldEnqueueToTaskQueue()
    {
        // Arrange
        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MaxAttempts = 1
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _taskQueue.Received(1).Enqueue(Arg.Any<Guid>());
    }

    [Test]
    public async Task Handle_ShouldStoreFullNameAsRequestType()
    {
        // Arrange
        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MaxAttempts = 1
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert — should use FullName not AssemblyQualifiedName
        _context.BackgroundTasks.Received(1).Add(Arg.Is<BackgroundTask>(t =>
            !t.RequestType.Contains("Version=")
            && t.RequestType.Contains("DeleteBackgroundTaskCommand")
        ));
    }
}
