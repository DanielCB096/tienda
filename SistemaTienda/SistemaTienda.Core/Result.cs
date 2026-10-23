// Patrón Result/Either para operaciones

public class ProcessResult
{
    public bool IsError { get; set; }
    public bool IsSuccess => !IsError;
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public static ProcessResult OK() => new ProcessResult { IsError = false };
    public static ProcessResult Error(string message) => new ProcessResult { IsError = true, Message = message };
    public static ProcessResult Success(string message) => new ProcessResult { IsError = false, Message = message };
}