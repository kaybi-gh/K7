using FluentValidation;
using FluentValidation.Results;
using K7.Server.Application.Common.Behaviours;
using MediatR;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.UnitTests.Common.Behaviours;

[TestFixture]
public class ValidationBehaviourTests
{
    private bool _nextCalled;

    [SetUp]
    public void SetUp() => _nextCalled = false;

    [Test]
    public async Task Handle_ShouldCallNext_WhenNoValidators()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, Unit>([]);

        await behaviour.Handle(new SampleRequest(), Next, CancellationToken.None);

        _nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldCallNext_WhenAllValidatorsPass()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, Unit>(
        [
            new PassingValidator(),
            new PassingValidator()
        ]);

        await behaviour.Handle(new SampleRequest(), Next, CancellationToken.None);

        _nextCalled.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldThrowValidationException_WhenAnyValidatorFails()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, Unit>(
        [
            new PassingValidator(),
            new FailingValidator("Name", "Name is required")
        ]);

        var act = () => behaviour.Handle(new SampleRequest(), Next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKey("Name");
        exception.Which.Errors["Name"].Should().Contain("Name is required");
        _nextCalled.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldAggregateFailures_FromMultipleValidators()
    {
        var behaviour = new ValidationBehaviour<SampleRequest, Unit>(
        [
            new FailingValidator("Name", "Name is required"),
            new FailingValidator("Path", "Path is required")
        ]);

        var act = () => behaviour.Handle(new SampleRequest(), Next, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<ValidationException>();
        exception.Which.Errors.Should().ContainKeys("Name", "Path");
        _nextCalled.Should().BeFalse();
    }

    private Task<Unit> Next()
    {
        _nextCalled = true;
        return Task.FromResult(Unit.Value);
    }

    private sealed class SampleRequest;

    private sealed class PassingValidator : AbstractValidator<SampleRequest>;

    private sealed class FailingValidator : AbstractValidator<SampleRequest>
    {
        public FailingValidator(string propertyName, string errorMessage)
        {
            RuleFor(x => x).Custom((_, context) =>
                context.AddFailure(new ValidationFailure(propertyName, errorMessage)));
        }
    }
}
