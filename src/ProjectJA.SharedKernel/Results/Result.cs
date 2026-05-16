// SPDX-License-Identifier: BUSL-1.1
namespace ProjectJA.SharedKernel.Results;

public readonly record struct Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

public readonly struct Result
{
    private Result(bool isSuccess, Error error) { IsSuccess = isSuccess; Error = error; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
}

public readonly struct Result<T>
{
    private Result(bool isSuccess, T? value, Error error) { IsSuccess = isSuccess; Value = value; Error = error; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Error Error { get; }

    public static Result<T> Success(T value) => new(true, value, Error.None);
    public static Result<T> Failure(Error error) => new(false, default, error);
}
