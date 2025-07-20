using System.Diagnostics.CodeAnalysis;

namespace GitWho2Blame.Models.Responses;

public record Response<T>
{
    private Response(T? value, string? errorMessage)
    {
        Value = value;
        ErrorMessage = errorMessage;
    }
    
    public T? Value { get; init; }
    
    public string? ErrorMessage { get; init; }
    
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => ErrorMessage is null;

    public static Response<T> Success(T value)
        => new(value, null);

    public static Response<T> Failure(string errorMessage)
        => new(default, errorMessage);
}