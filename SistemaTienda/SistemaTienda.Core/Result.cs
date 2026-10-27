// Patrón Result/Either para operaciones que pueden fallar sin lanzar excepción
namespace SistemaTienda.Core;

public class ProcessResult
{
    public bool IsError { get; init; }
    public bool IsSuccess => !IsError;
    public string Message { get; init; } = string.Empty;

    public static ProcessResult OK() => new() { IsError = false };
    public static ProcessResult Error(string message) => new() { IsError = true, Message = message };
    public static ProcessResult Success(string message) => new() { IsError = false, Message = message };
}

public class ProcessResult<T>
{
    public bool IsError { get; init; }
    public bool IsSuccess => !IsError;
    public string Message { get; init; } = string.Empty;
    public T Value { get; init; }

    public static ProcessResult<T> OK(T value) => new() { Value = value };
    public static ProcessResult<T> OK(T value, string message) => new() { Value = value, Message = message };
    public static ProcessResult<T> Error(string message) => new() { IsError = true, Message = message };
}
