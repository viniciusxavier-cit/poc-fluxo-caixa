using FluentValidation.Results;

namespace SharedKernel.Behaviors;

public sealed class ValidationException : Exception
{
    public IReadOnlyList<ValidationFailure> Errors { get; }

    public ValidationException(IEnumerable<ValidationFailure> errors)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = errors.ToList().AsReadOnly();
    }
}
