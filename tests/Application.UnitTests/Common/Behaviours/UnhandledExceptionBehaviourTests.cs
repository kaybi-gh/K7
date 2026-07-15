using K7.Server.Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Common.Behaviours;

[TestFixture]
public class UnhandledExceptionBehaviourTests
{
    private ILogger<SampleRequest> _logger = null!;

    [SetUp]
    public void SetUp() => _logger = Substitute.For<ILogger<SampleRequest>>();

    [Test]
    public async Task Handle_ShouldReturnResponse_WhenNextSucceeds()
    {
        var behaviour = new UnhandledExceptionBehaviour<SampleRequest, Unit>(_logger);

        var result = await behaviour.Handle(new SampleRequest(), () => Task.FromResult(Unit.Value), CancellationToken.None);

        result.Should().Be(Unit.Value);
    }

    [Test]
    public async Task Handle_ShouldRethrow_WhenNextThrows()
    {
        var behaviour = new UnhandledExceptionBehaviour<SampleRequest, Unit>(_logger);

        var act = () => behaviour.Handle(
            new SampleRequest(),
            () => Task.FromException<Unit>(new InvalidOperationException("boom")),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Test]
    public async Task Handle_ShouldRethrow_WhenRequestCancelled()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var behaviour = new UnhandledExceptionBehaviour<SampleRequest, Unit>(_logger);

        var act = () => behaviour.Handle(
            new SampleRequest(),
            () => Task.FromCanceled<Unit>(cts.Token),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    public sealed class SampleRequest;
}
