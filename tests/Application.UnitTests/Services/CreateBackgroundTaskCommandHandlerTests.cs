using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MockQueryable.NSubstitute;

namespace K7.Server.Application.UnitTests.Services;

public class CreateBackgroundTaskCommandHandlerTests
{
    private IApplicationDbContext _context;
    private IBackgroundTaskQueue _taskQueue;
    private IBackgroundTaskNotifier _notifier;
    private ILogger<CreateBackgroundTaskCommandHandler> _logger;
    private CreateBackgroundTaskCommandHandler _handler;
    private List<BackgroundTask> _existingTasks;

    [SetUp]
    public void Setup()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _taskQueue = Substitute.For<IBackgroundTaskQueue>();
        _notifier = Substitute.For<IBackgroundTaskNotifier>();
        _logger = Substitute.For<ILogger<CreateBackgroundTaskCommandHandler>>();
        _existingTasks = [];

        var dbSet = _existingTasks.BuildMockDbSet();
        _context.BackgroundTasks.Returns(dbSet);

        _handler = new CreateBackgroundTaskCommandHandler(_context, _taskQueue, _notifier, _logger);
    }

    [Test]
    public async Task Handle_ShouldAddEntityToDbSet()
    {
        // Arrange
        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            Lane = BackgroundTaskLane.Probe,
            WorkClass = BackgroundTaskWorkClass.CriticalProbe,
            MaxAttempts = 3,
            TargetEntityTypeName = "TestEntity",
            TargetEntityId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Is<BackgroundTask>(t =>
            t.Status == BackgroundTaskStatus.Pending
            && t.Lane == BackgroundTaskLane.Probe
            && t.WorkClass == BackgroundTaskWorkClass.CriticalProbe
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
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
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
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 1
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert - should use FullName not AssemblyQualifiedName
        _context.BackgroundTasks.Received(1).Add(Arg.Is<BackgroundTask>(t =>
            !t.RequestType.Contains("Version=")
            && t.RequestType.Contains("DeleteBackgroundTaskCommand")
        ));
    }

    [Test]
    public async Task Handle_ShouldStoreLaneAndFederationPeerId()
    {
        // Arrange
        var peerId = Guid.NewGuid();
        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            MaxAttempts = 1,
            Lane = BackgroundTaskLane.Federation,
            FederationPeerId = peerId
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Is<BackgroundTask>(t =>
            t.Lane == BackgroundTaskLane.Federation
            && t.FederationPeerId == peerId
        ));
    }

    [Test]
    public async Task Handle_ShouldApplyInteractiveBoost_WhenTriggeredByUser()
    {
        // Arrange
        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            MaxAttempts = 1,
            TriggeredBy = BackgroundTaskTriggeredBy.User
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Is<BackgroundTask>(t =>
            t.TriggeredBy == BackgroundTaskTriggeredBy.User
            && t.Priority == BackgroundTaskScheduling.InteractiveBoost
        ));
    }

    [Test]
    public async Task Handle_ShouldLeavePriorityAtZero_WhenTriggeredBySystem()
    {
        // Arrange
        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            MaxAttempts = 1,
            TriggeredBy = BackgroundTaskTriggeredBy.System
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Is<BackgroundTask>(t =>
            t.TriggeredBy == BackgroundTaskTriggeredBy.System
            && t.Priority == 0
        ));
    }

    [Test]
    public async Task Handle_ShouldSkipCreation_WhenDuplicatePendingTaskExists()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var existingId = Guid.NewGuid();

        _existingTasks.Add(new BackgroundTask
        {
            Id = existingId,
            Name = nameof(DeleteBackgroundTaskCommand),
            RequestType = typeof(DeleteBackgroundTaskCommand).FullName!,
            RequestData = "{}",
            TargetEntityId = targetId,
            Status = BackgroundTaskStatus.Pending
        });

        var dbSet = _existingTasks.BuildMockDbSet();
        _context.BackgroundTasks.Returns(dbSet);

        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            TargetEntityId = targetId,
            MaxAttempts = 1
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(existingId);
        _context.BackgroundTasks.DidNotReceive().Add(Arg.Any<BackgroundTask>());
        await _context.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _taskQueue.DidNotReceive().Enqueue(Arg.Any<Guid>());
    }

    [Test]
    public async Task Handle_ShouldSkipCreation_WhenDuplicateInProgressTaskExists()
    {
        // Arrange
        var targetId = Guid.NewGuid();
        var existingId = Guid.NewGuid();

        _existingTasks.Add(new BackgroundTask
        {
            Id = existingId,
            Name = nameof(DeleteBackgroundTaskCommand),
            RequestType = typeof(DeleteBackgroundTaskCommand).FullName!,
            RequestData = "{}",
            TargetEntityId = targetId,
            Status = BackgroundTaskStatus.InProgress
        });

        var dbSet = _existingTasks.BuildMockDbSet();
        _context.BackgroundTasks.Returns(dbSet);

        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            TargetEntityId = targetId,
            MaxAttempts = 1
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().Be(existingId);
        _context.BackgroundTasks.DidNotReceive().Add(Arg.Any<BackgroundTask>());
    }

    [Test]
    public async Task Handle_ShouldCreate_WhenExistingTaskIsCompleted()
    {
        // Arrange
        var targetId = Guid.NewGuid();

        _existingTasks.Add(new BackgroundTask
        {
            Id = Guid.NewGuid(),
            Name = nameof(DeleteBackgroundTaskCommand),
            RequestType = typeof(DeleteBackgroundTaskCommand).FullName!,
            RequestData = "{}",
            TargetEntityId = targetId,
            Status = BackgroundTaskStatus.Completed
        });

        var dbSet = _existingTasks.BuildMockDbSet();
        _context.BackgroundTasks.Returns(dbSet);

        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            TargetEntityId = targetId,
            MaxAttempts = 1
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Any<BackgroundTask>());
        _taskQueue.Received(1).Enqueue(Arg.Any<Guid>());
    }

    [Test]
    public async Task Handle_ShouldCreate_WhenExistingTaskIsFailed()
    {
        // Arrange
        var targetId = Guid.NewGuid();

        _existingTasks.Add(new BackgroundTask
        {
            Id = Guid.NewGuid(),
            Name = nameof(DeleteBackgroundTaskCommand),
            RequestType = typeof(DeleteBackgroundTaskCommand).FullName!,
            RequestData = "{}",
            TargetEntityId = targetId,
            Status = BackgroundTaskStatus.Failed
        });

        var dbSet = _existingTasks.BuildMockDbSet();
        _context.BackgroundTasks.Returns(dbSet);

        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            TargetEntityId = targetId,
            MaxAttempts = 1
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Any<BackgroundTask>());
        _taskQueue.Received(1).Enqueue(Arg.Any<Guid>());
    }

    [Test]
    public async Task Handle_ShouldCreate_WhenSameNameButDifferentTarget()
    {
        // Arrange
        _existingTasks.Add(new BackgroundTask
        {
            Id = Guid.NewGuid(),
            Name = nameof(DeleteBackgroundTaskCommand),
            RequestType = typeof(DeleteBackgroundTaskCommand).FullName!,
            RequestData = "{}",
            TargetEntityId = Guid.NewGuid(),
            Status = BackgroundTaskStatus.Pending
        });

        var dbSet = _existingTasks.BuildMockDbSet();
        _context.BackgroundTasks.Returns(dbSet);

        var command = new CreateBackgroundTaskCommand
        {
            Request = new DeleteBackgroundTaskCommand(Guid.NewGuid()),
            MetadataProviderName = MetadataProviderNames.Local,
            Lane = BackgroundTaskLane.Metadata,
            WorkClass = BackgroundTaskWorkClass.Polish,
            TriggeredBy = BackgroundTaskTriggeredBy.System,
            TargetEntityId = Guid.NewGuid(),
            MaxAttempts = 1
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _context.BackgroundTasks.Received(1).Add(Arg.Any<BackgroundTask>());
        _taskQueue.Received(1).Enqueue(Arg.Any<Guid>());
    }
}
